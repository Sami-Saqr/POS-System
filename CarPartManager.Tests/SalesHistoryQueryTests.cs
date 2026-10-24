using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using CarPartManager.Data;
using CarPartManager.Models;
using CarPartManager.Services;
using Xunit;

namespace CarPartManager.Tests;

/// <summary>
/// اختبارات مسار سجل فواتير المبيعات على SQLite في الذاكرة (مزود علائقي حقيقي وليس EF InMemory):
/// (1) الحفظ الفعلي عبر PostInvoiceAsync (عميل نقدي + عميل مسجل) ثم قراءته عبر GetAllInvoicesAsync.
/// (2) ترقية قاعدة بيانات قديمة (Schema قديم بدون CustomerId/CustomerAddress) عند بدء التشغيل.
/// </summary>
public sealed class SalesHistoryQueryTests
{
    /// <summary>قاعدة بيانات SQLite مؤقتة في الذاكرة، يُبقى الاتصال مفتوحاً طوال الاختبار.</summary>
    private sealed class TestDb : IDisposable
    {
        public SqliteConnection Connection { get; }
        public DbContextOptions<CarPartsDbContext> Options { get; }

        public TestDb()
        {
            Connection = new SqliteConnection("DataSource=:memory:");
            Connection.Open();
            Options = new DbContextOptionsBuilder<CarPartsDbContext>()
                .UseSqlite(Connection)
                .Options;
            using var ctx = new CarPartsDbContext(Options);
            ctx.Database.EnsureCreated();
        }

        public CarPartsDbContext NewContext() => new CarPartsDbContext(Options);
        public void Dispose() => Connection.Dispose();
    }

    /// <summary>نسخة من الخدمة توجّه السياق إلى اتصال SQLite في الذاكرة المشترك.</summary>
    private sealed class TestableSalesInvoiceService : SqliteSalesInvoiceService
    {
        private readonly DbContextOptions<CarPartsDbContext> _options;
        public TestableSalesInvoiceService(DbContextOptions<CarPartsDbContext> options) => _options = options;
        protected override CarPartsDbContext CreateContext() => new CarPartsDbContext(_options);
    }

    private static ProductModel SeedProduct(TestDb db, string partNumber, int qty, string barcode)
    {
        using var ctx = db.NewContext();
        var p = new ProductModel
        {
            PartNumber = partNumber,
            Name = partNumber,
            Barcode = barcode,
            Quantity = qty,
            PurchasePrice = 50m,
            SellingPrice = 80m
        };
        ctx.Products.Add(p);
        ctx.SaveChanges();
        return p;
    }

    /// <summary>بناء فاتورة بيع بنفس شكل ما يبنيه نقطة البيع (POS) عند الاعتماد.</summary>
    private static SalesInvoiceModel BuildSale(
        string invoiceNumber, ProductModel product, string? customerName, int? customerId, string phone, DateTime date)
    {
        return new SalesInvoiceModel
        {
            InvoiceNumber = invoiceNumber,
            InvoiceDate = date,
            CustomerId = customerId,
            CustomerName = string.IsNullOrWhiteSpace(customerName) ? "عميل نقدي" : customerName,
            CustomerPhone = phone,
            CustomerAddress = string.Empty,
            Status = "مسودة",
            PaymentMethod = "نقدي",
            Notes = string.Empty,
            Items = new List<SalesInvoiceItemModel>
            {
                new SalesInvoiceItemModel
                {
                    ProductId = product.Id,
                    PartNumber = product.PartNumber,
                    ProductName = product.Name,
                    Quantity = 1,
                    UnitPrice = 80m
                }
            }
        };
    }

    private static List<string> GetColumns(DbContextOptions<CarPartsDbContext> options, string table)
    {
        using var ctx = new CarPartsDbContext(options);
        var columns = new List<string>();
        var conn = ctx.Database.GetDbConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info(\"{table}\");";
        if (conn.State != System.Data.ConnectionState.Open) conn.Open();
        using var reader = cmd.ExecuteReader();
        while (reader.Read()) columns.Add(reader.GetString(1));
        return columns;
    }

    // (1) الحفظ الحقيقي لفاتورة عميل نقدي وفاتورة عميل مسجل ثم ظهورهما في استعلام السجل.
    [Fact]
    public async Task History_lists_walkin_and_customer_sales_saved_via_real_path()
    {
        using var db = new TestDb();
        var product = SeedProduct(db, "P-SALE-1", 10, "SRC-SALE-1");
        var svc = new TestableSalesInvoiceService(db.Options);

        // عميل نقدي (Walk-in) بدون معرف عميل
        var walkIn = await svc.PostInvoiceAsync(
            BuildSale("INV-T-0001", product, null, null, string.Empty, DateTime.Now.AddMinutes(-5)));
        Assert.Null(walkIn.CustomerId);

        // عميل مسجل بالاسم
        var customerSale = await svc.PostInvoiceAsync(
            BuildSale("INV-T-0002", product, "شركة الأمل", null, "01000000001", DateTime.Now));
        Assert.NotNull(customerSale.CustomerId);

        var history = (await svc.GetAllInvoicesAsync()).ToList();

        Assert.Equal(2, history.Count);
        Assert.All(history, i => Assert.Equal("معتمدة", i.Status));
        Assert.Equal("INV-T-0002", history[0].InvoiceNumber); // الأحدث أولاً
        Assert.Contains(history, i => i.InvoiceNumber == "INV-T-0001" && i.CustomerId == null && i.CustomerName == "عميل نقدي");
        Assert.Contains(history, i => i.InvoiceNumber == "INV-T-0002" && i.CustomerName == "شركة الأمل");
        Assert.All(history, i => Assert.Single(i.Items));
    }

    // (2) محاكاة قاعدة بيانات قديمة تنقصها أعمدة العملاء/الإلغاء في SalesInvoices،
    //     ثم تشغيل نفس مسار بدء التشغيل والتأكد من ترقية الأعمدة ونجاح استعلام السجل.
    [Fact]
    public async Task Legacy_database_missing_customer_columns_is_upgraded_at_startup()
    {
        using var db = new TestDb();
        var service = new TestableSalesInvoiceService(db.Options);

        // 1. قاعدة بيانات قديمة: إزالة الأعمدة التي أُضيفت لاحقاً من SalesInvoices
        using (var legacy = db.NewContext())
        {
            await legacy.Database.ExecuteSqlRawAsync(@"DROP INDEX IF EXISTS ""IX_SalesInvoices_CustomerId"";");
            await legacy.Database.ExecuteSqlRawAsync(@"ALTER TABLE ""SalesInvoices"" DROP COLUMN ""CancellationReason"";");
            await legacy.Database.ExecuteSqlRawAsync(@"ALTER TABLE ""SalesInvoices"" DROP COLUMN ""CancellationDate"";");
            await legacy.Database.ExecuteSqlRawAsync(@"ALTER TABLE ""SalesInvoices"" DROP COLUMN ""CustomerAddress"";");
            await legacy.Database.ExecuteSqlRawAsync(@"ALTER TABLE ""SalesInvoices"" DROP COLUMN ""CustomerId"";");
            await legacy.Database.ExecuteSqlRawAsync(@"
                INSERT INTO ""SalesInvoices"" (""InvoiceNumber"",""InvoiceDate"",""CustomerName"",""CustomerPhone"",""Status"",""SubTotal"",""Discount"",""GrandTotal"",""PaymentMethod"",""PaidAmount"",""Notes"")
                VALUES ('INV-2026-0001','2026-01-01 10:00:00','','','معتمدة','100','0','100','نقدي','100','');");
        }

        var legacyColumns = GetColumns(db.Options, "SalesInvoices");
        Assert.DoesNotContain("CustomerId", legacyColumns);
        Assert.DoesNotContain("CustomerAddress", legacyColumns);

        // 2. بدء التشغيل الحقيقي (نفس مسار App.OnStartup → DbInitializer)
        using (var startup = db.NewContext())
        {
            await DbInitializer.InitializeAsync(startup);
        }

        // 3. الأعمدة الحديثة يجب أن تكون موجودة الآن (وإلا يفشل استعلام السجل بصمت)
        var columns = GetColumns(db.Options, "SalesInvoices");
        Assert.Contains("CustomerId", columns);
        Assert.Contains("CustomerAddress", columns);
        Assert.Contains("CancellationDate", columns);
        Assert.Contains("CancellationReason", columns);

        // 4. الفاتورة القديمة يجب أن تظهر في السجل بعد الترقية
        var history = (await service.GetAllInvoicesAsync()).ToList();
        Assert.Single(history);
        Assert.Equal("INV-2026-0001", history[0].InvoiceNumber);
        Assert.Null(history[0].CustomerId);
    }
}
