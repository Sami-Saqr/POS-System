using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using CarPartManager.Data;
using CarPartManager.Models;
using CarPartManager.Services;
using Xunit;

namespace CarPartManager.Tests;

/// <summary>
/// اختبارات تثبيت تكلفة الوحدة وقت البيع على بنود فواتير المبيعات واستخدامها في التقارير،
/// على SQLite في الذاكرة (مزود علائقي حقيقي وليس EF InMemory):
/// (أ) قاعدة بيانات قديمة بلا عمود UnitCostAtSale تُرقّى بأمان عند بدء التشغيل.
/// (ب) الفاتورة المعتمدة تُثبّت تكلفة الوحدة وقت البيع.
/// (ج) تغيير سعر تكلفة الصنف لاحقاً لا يغيّر تكلفة البضاعة المبيعة تاريخياً.
/// (د) البنود القديمة (NULL) تستخدم السعر الحالي كتقدير وتُظهر تنبيه "التكلفة تقديرية" بعددها، والحديثة لا.
/// (هـ) المرتجع الصالح للبيع يعكس التكلفة المخزَّنة في بند البيع الأصلي.
/// </summary>
public sealed class CostAtSaleTests
{
    private static readonly DateTime From = new(2026, 3, 1);
    private static readonly DateTime To = new(2026, 4, 1);

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

    private sealed class TestableSalesInvoiceService : SqliteSalesInvoiceService
    {
        private readonly DbContextOptions<CarPartsDbContext> _options;
        public TestableSalesInvoiceService(DbContextOptions<CarPartsDbContext> options) => _options = options;
        protected override CarPartsDbContext CreateContext() => new CarPartsDbContext(_options);
    }

    private sealed class TestableReportsService : SqliteReportsService
    {
        private readonly DbContextOptions<CarPartsDbContext> _options;
        public TestableReportsService(DbContextOptions<CarPartsDbContext> options) => _options = options;
        protected override CarPartsDbContext CreateContext() => new CarPartsDbContext(_options);
    }

    private static ProductModel SeedProduct(TestDb db, string partNumber, int qty, decimal purchasePrice)
    {
        using var ctx = db.NewContext();
        var p = new ProductModel
        {
            PartNumber = partNumber,
            Name = partNumber,
            Barcode = partNumber,
            Quantity = qty,
            PurchasePrice = purchasePrice,
            SellingPrice = 80m
        };
        ctx.Products.Add(p);
        ctx.SaveChanges();
        return p;
    }

    private static SalesInvoiceModel BuildSale(
        string invoiceNumber, ProductModel product, int quantity, decimal unitPrice, DateTime invoiceDate)
    {
        return new SalesInvoiceModel
        {
            InvoiceNumber = invoiceNumber,
            InvoiceDate = invoiceDate,
            CustomerId = null,
            CustomerName = "عميل نقدي",
            CustomerPhone = string.Empty,
            CustomerAddress = string.Empty,
            Status = "مسودة",
            PaymentMethod = "نقدي",
            Notes = string.Empty,
            Items = new List<SalesInvoiceItemModel>
            {
                new()
                {
                    ProductId = product.Id,
                    PartNumber = product.PartNumber,
                    ProductName = product.Name,
                    Quantity = quantity,
                    UnitPrice = unitPrice
                }
            }
        };
    }

    private static List<string> GetColumns(DbContextOptions<CarPartsDbContext> options, string table)
    {
        using var ctx = new CarPartsDbContext(options);
        var conn = ctx.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open) conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info(\"{table}\");";
        using var reader = cmd.ExecuteReader();
        var columns = new List<string>();
        while (reader.Read()) columns.Add(reader.GetString(1));
        return columns;
    }

    private static void SetProductPurchasePrice(TestDb db, int productId, decimal price)
    {
        using var ctx = db.NewContext();
        var product = ctx.Products.Single(p => p.Id == productId);
        product.PurchasePrice = price;
        ctx.SaveChanges();
    }

    // (أ) ترقية قاعدة بيانات قديمة تنقصها تكلفة الوحدة وقت البيع.
    [Fact]
    public async Task Startup_adds_UnitCostAtSale_column_to_a_legacy_database()
    {
        using var db = new TestDb();

        // قاعدة بيانات قديمة: العمود غير موجود أصلاً
        using (var legacy = db.NewContext())
        {
            await legacy.Database.ExecuteSqlRawAsync(@"ALTER TABLE ""SalesInvoiceItems"" DROP COLUMN ""UnitCostAtSale"";");
        }

        Assert.DoesNotContain("UnitCostAtSale", GetColumns(db.Options, "SalesInvoiceItems"));

        // نفس مسار بدء التشغيل الحقيقي (App.OnStartup → DbInitializer)
        using (var startup = db.NewContext())
        {
            await DbInitializer.InitializeAsync(startup);
        }

        Assert.Contains("UnitCostAtSale", GetColumns(db.Options, "SalesInvoiceItems"));

        // تشغيل ثانٍ: العملية إضافية وآمنة (لا فشل عند وجود العمود)
        using (var startupAgain = db.NewContext())
        {
            await DbInitializer.InitializeAsync(startupAgain);
        }

        Assert.Contains("UnitCostAtSale", GetColumns(db.Options, "SalesInvoiceItems"));
    }

    // (ب) الفاتورة المعتمدة تُثبّت تكلفة الوحدة وقت البيع، والمسودة لا تُثبّتها.
    [Fact]
    public async Task Posted_invoice_stores_the_product_cost_and_drafts_do_not()
    {
        using var db = new TestDb();
        var product = SeedProduct(db, "P-COST-1", 10, 50m);
        var sales = new TestableSalesInvoiceService(db.Options);

        // مسودة: تبقى بلا تكلفة مثبَّتة
        var draft = await sales.SaveDraftInvoiceAsync(
            BuildSale("INV-C-DRAFT", product, 1, 80m, From.AddHours(1)));

        using (var ctx = db.NewContext())
        {
            var draftItem = ctx.SalesInvoiceItems.Single(i => i.SalesInvoiceId == draft.Id);
            Assert.Null(draftItem.UnitCostAtSale);
        }

        // اعتماد: تُثبّت التكلفة
        var posted = await sales.PostInvoiceAsync(
            BuildSale("INV-C-0001", product, 2, 80m, From.AddHours(2)));

        using (var ctx = db.NewContext())
        {
            var line = ctx.SalesInvoiceItems.Single(i => i.SalesInvoiceId == posted.Id);
            Assert.Equal(50m, line.UnitCostAtSale);
            Assert.Equal(2, line.Quantity);
        }

        // اعتماد مسودة: تُثبّت التكلفة أيضاً
        var completed = await sales.PostInvoiceAsync(draft);

        using (var ctx = db.NewContext())
        {
            var line = ctx.SalesInvoiceItems.Single(i => i.SalesInvoiceId == completed.Id);
            Assert.Equal(50m, line.UnitCostAtSale);
        }
    }

    // (ج) تغيير سعر التكلفة لاحقاً لا يغيّر تكلفة البضاعة المبيعة للفترة السابقة.
    [Fact]
    public async Task Changing_product_cost_later_does_not_change_historical_cogs()
    {
        using var db = new TestDb();
        var product = SeedProduct(db, "P-COST-2", 10, 50m);
        var sales = new TestableSalesInvoiceService(db.Options);

        await sales.PostInvoiceAsync(BuildSale("INV-C-0002", product, 2, 80m, From.AddDays(1)));

        // كما يفعل اعتماد فاتورة شراء جديدة: تحديث سعر الشراء الأخير للصنف
        SetProductPurchasePrice(db, product.Id, 999m);

        var reports = new TestableReportsService(db.Options);
        var summary = await reports.GetProfitSummaryAsync(From, To, includePreviousComparison: false);

        Assert.Equal(100m, summary.CogsBeforeReturns); // 2 × 50 المثبَّتة وقت البيع (لا 999)
        Assert.False(summary.IsCogsEstimated);
        Assert.Equal(0, summary.EstimatedLineCount);
        Assert.Equal(0, summary.EstimatedInvoiceCount);

        // نفس التكلفة في تبويب ربحية الأصناف (اتساق التحقق المتقاطع)
        var itemProfit = await reports.GetItemProfitabilityAsync(From, To);
        Assert.Equal(100m, itemProfit.TotalCost);
        Assert.False(itemProfit.IsCogsEstimated);
        Assert.Equal(0, itemProfit.EstimatedLineCount);
    }

    // (د) البنود القديمة (NULL) تستخدم السعر الحالي كتقدير وتُظهر تنبيه "التكلفة تقديرية" بعددها.
    [Fact]
    public async Task Legacy_null_cost_lines_fall_back_to_current_cost_and_report_the_estimate_count()
    {
        using var db = new TestDb();
        var product = SeedProduct(db, "P-COST-3", 10, 50m);

        // فاتورة معتمدة قديمة ببند بلا تكلفة مثبَّتة (كما في البيانات السابقة للترقية)
        using (var ctx = db.NewContext())
        {
            ctx.SalesInvoices.Add(new SalesInvoiceModel
            {
                InvoiceNumber = "INV-C-LEGACY",
                InvoiceDate = From.AddDays(1),
                CustomerName = "عميل نقدي",
                Status = "معتمدة",
                PostedDate = From.AddDays(1),
                PaymentMethod = "نقدي",
                Notes = string.Empty,
                SubTotal = 240m,
                GrandTotal = 240m,
                Items = new List<SalesInvoiceItemModel>
                {
                    new()
                    {
                        ProductId = product.Id,
                        PartNumber = product.PartNumber,
                        ProductName = product.Name,
                        Quantity = 3,
                        UnitPrice = 80m
                    }
                }
            });
            ctx.SaveChanges();
        }

        var reports = new TestableReportsService(db.Options);
        var summary = await reports.GetProfitSummaryAsync(From, To, includePreviousComparison: false);

        Assert.Equal(150m, summary.CogsBeforeReturns); // 3 × 50 السعر الحالي كتقدير
        Assert.True(summary.IsCogsEstimated);
        Assert.Equal(1, summary.EstimatedLineCount);
        Assert.Equal(1, summary.EstimatedInvoiceCount);

        var itemProfit = await reports.GetItemProfitabilityAsync(From, To);
        Assert.True(itemProfit.IsCogsEstimated);
        Assert.Equal(1, itemProfit.EstimatedLineCount);
        Assert.Equal(1, itemProfit.EstimatedInvoiceCount);

        // فترة بلا بنود قديمة: لا تنبيه تقديري
        var cleanSummary = await reports.GetProfitSummaryAsync(
            new DateTime(2026, 1, 1), new DateTime(2026, 2, 1), includePreviousComparison: false);

        Assert.False(cleanSummary.IsCogsEstimated);
        Assert.Equal(0, cleanSummary.EstimatedLineCount);
    }

    // (هـ) المرتجع الصالح للبيع يعكس التكلفة المخزَّنة في بند البيع الأصلي (لا السعر الحالي).
    [Fact]
    public async Task Sellable_return_reverses_the_original_line_stored_cost()
    {
        using var db = new TestDb();
        var product = SeedProduct(db, "P-COST-4", 10, 50m);
        var sales = new TestableSalesInvoiceService(db.Options);

        var invoice = await sales.PostInvoiceAsync(BuildSale("INV-C-0004", product, 2, 80m, From.AddDays(1)));

        // تغيير التكلفة الحالية بعد البيع لإثبات أن العكس يستخدم التكلفة المخزَّنة في البند الأصلي
        SetProductPurchasePrice(db, product.Id, 999m);

        using (var ctx = db.NewContext())
        {
            var originalItemId = ctx.SalesInvoiceItems.Single(i => i.SalesInvoiceId == invoice.Id).Id;

            ctx.SalesReturns.Add(new SalesReturnModel
            {
                ReturnNumber = "SRT-C-0001",
                ReturnDate = From.AddDays(2),
                SalesInvoiceId = invoice.Id,
                SalesInvoiceNumber = invoice.InvoiceNumber,
                CustomerName = "عميل نقدي",
                TotalAmount = 80m,
                Status = "معتمدة",
                PostedDate = From.AddDays(2),
                Notes = string.Empty,
                Items = new List<SalesReturnItemModel>
                {
                    new()
                    {
                        SalesInvoiceItemId = originalItemId,
                        ProductId = product.Id,
                        PartNumber = product.PartNumber,
                        ProductName = product.Name,
                        OriginalQuantity = 2,
                        Quantity = 1,
                        UnitPrice = 80m,
                        ItemCondition = "صالح للبيع"
                    }
                }
            });
            ctx.SaveChanges();
        }

        var reports = new TestableReportsService(db.Options);
        var summary = await reports.GetProfitSummaryAsync(From, To, includePreviousComparison: false);

        Assert.Equal(100m, summary.CogsBeforeReturns);    // 2 × 50
        Assert.Equal(50m, summary.ReturnedCogsReduction); // 1 × 50 المخزَّنة، لا 999
        Assert.False(summary.IsCogsEstimated);
        Assert.Equal(0, summary.EstimatedLineCount);

        // ربحية الأصناف: تكلفة صافية = 100 (مبيعات) − 50 (مرتجع) = 50 — متسقة مع ملخص الأرباح
        var itemProfit = await reports.GetItemProfitabilityAsync(From, To);
        Assert.Equal(50m, itemProfit.TotalCost);
        Assert.False(itemProfit.IsCogsEstimated);
    }

    // عملية شراء لا علاقة لها بالتقارير: التأكد من عدم تأثر الإجماليات بالمخزون بعد الاعتماد.
    [Fact]
    public async Task Posting_an_invoice_still_deducts_stock_and_keeps_totals()
    {
        using var db = new TestDb();
        var product = SeedProduct(db, "P-COST-5", 10, 50m);
        var sales = new TestableSalesInvoiceService(db.Options);

        var posted = await sales.PostInvoiceAsync(BuildSale("INV-C-0005", product, 4, 80m, From.AddDays(1)));

        using var ctx = db.NewContext();
        Assert.Equal(6, ctx.Products.Single(p => p.Id == product.Id).Quantity);
        Assert.Equal(320m, posted.SubTotal);
        Assert.Equal(320m, posted.GrandTotal);
    }
}
