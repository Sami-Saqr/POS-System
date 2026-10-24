using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using CarPartManager.Data;
using CarPartManager.Models;
using CarPartManager.Services;
using Xunit;

namespace CarPartManager.Tests;

/// <summary>
/// اختبارات منطق اعتماد الجرد باستخدام SQLite في الذاكرة (مزود علائقي حقيقي وليس EF InMemory)،
/// لكشف سلوك المفاتيح الأجنبية الظلية (shadow FK) والحركات العلائقية بدقة.
/// </summary>
public sealed class StocktakeApprovalTests
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
    private sealed class TestableStocktakeService : SqliteStocktakeService
    {
        private readonly DbContextOptions<CarPartsDbContext> _options;
        public TestableStocktakeService(DbContextOptions<CarPartsDbContext> options) => _options = options;
        protected override CarPartsDbContext CreateContext() => new CarPartsDbContext(_options);
    }

    private static ProductModel SeedProduct(TestDb db, string partNumber, int qty, decimal cost = 10m, string barcode = "B")
    {
        using var ctx = db.NewContext();
        var p = new ProductModel
        {
            PartNumber = partNumber,
            Name = partNumber,
            Barcode = barcode,
            Quantity = qty,
            PurchasePrice = cost,
            SellingPrice = cost + 5m
        };
        ctx.Products.Add(p);
        ctx.SaveChanges();
        return p;
    }

    private static void SimulateSale(TestDb db, int productId, int sold)
    {
        using var ctx = db.NewContext();
        var prod = ctx.Products.First(x => x.Id == productId);
        prod.Quantity -= sold;
        ctx.SaveChanges();
    }

    // (a) Include(Lines) loads lines via the real SessionId column (not a shadow FK).
    [Fact]
    public async Task Include_Lines_loads_via_SessionId()
    {
        using var db = new TestDb();
        var p = SeedProduct(db, "P-A", 10, barcode: "BA");

        var svc = new TestableStocktakeService(db.Options);
        var session = await svc.CreateSessionAsync("s", "كامل");

        using var q = db.NewContext();
        var loaded = await q.StocktakeSessions.Include(s => s.Lines).FirstAsync(s => s.Id == session.Id);

        Assert.NotEmpty(loaded.Lines);
        Assert.Single(loaded.Lines);
        Assert.All(loaded.Lines, l => Assert.Equal(session.Id, l.SessionId));
        Assert.Equal(10, loaded.Lines.First().BookQuantity);
    }

    // (b) A sale between the count and approval is preserved: final = counted - sold.
    [Fact]
    public async Task Sale_between_count_and_approval_preserves_sale()
    {
        using var db = new TestDb();
        var p = SeedProduct(db, "P-B", 10, barcode: "BB");

        var svc = new TestableStocktakeService(db.Options);
        var session = await svc.CreateSessionAsync("s", "كامل");
        await svc.StartCountingAsync(session.Id);
        await svc.CountProductAsync(session.Id, p.Id, 8); // book refreshed to 10, diff = -2
        SimulateSale(db, p.Id, 3); // 10 -> 7 after the count
        await svc.SubmitForReviewAsync(session.Id);
        await svc.ApproveSessionAsync(session.Id);

        using var check = db.NewContext();
        var final = check.Products.First(x => x.Id == p.Id);
        // counted(8) - sold(3) = 5
        Assert.Equal(5, final.Quantity);
    }

    // (c) A sale between snapshot and count is NOT double counted: final == counted.
    [Fact]
    public async Task Sale_between_snapshot_and_count_not_double_counted()
    {
        using var db = new TestDb();
        var p = SeedProduct(db, "P-C", 10, barcode: "BC");

        var svc = new TestableStocktakeService(db.Options);
        var session = await svc.CreateSessionAsync("s", "كامل"); // creation snapshot = 10
        await svc.StartCountingAsync(session.Id);
        SimulateSale(db, p.Id, 3); // 10 -> 7 before the count
        await svc.CountProductAsync(session.Id, p.Id, 8); // book refreshed to 7, diff = +1
        await svc.SubmitForReviewAsync(session.Id);
        await svc.ApproveSessionAsync(session.Id);

        using var check = db.NewContext();
        var final = check.Products.First(x => x.Id == p.Id);
        // current(7) + diff(+1) = 8 == counted (NOT 5 = counted - sold)
        Assert.Equal(8, final.Quantity);
    }

    // (d) Approving twice is blocked.
    [Fact]
    public async Task Approving_twice_is_blocked()
    {
        using var db = new TestDb();
        var p = SeedProduct(db, "P-D", 10, barcode: "BD");

        var svc = new TestableStocktakeService(db.Options);
        var session = await svc.CreateSessionAsync("s", "كامل");
        await svc.StartCountingAsync(session.Id);
        await svc.CountProductAsync(session.Id, p.Id, 8);
        await svc.SubmitForReviewAsync(session.Id);

        Assert.True(await svc.ApproveSessionAsync(session.Id));

        // status is now معتمد, so a second approval must throw.
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.ApproveSessionAsync(session.Id));
    }

    // (e) Uncounted lines are untouched on approval.
    [Fact]
    public async Task Uncounted_lines_unchanged()
    {
        using var db = new TestDb();
        var p = SeedProduct(db, "P-E", 10, barcode: "BE");

        var svc = new TestableStocktakeService(db.Options);
        var session = await svc.CreateSessionAsync("s", "كامل");
        await svc.StartCountingAsync(session.Id);
        // never counted -> CountedQuantity stays null
        await svc.SubmitForReviewAsync(session.Id);
        await svc.ApproveSessionAsync(session.Id);

        using var check = db.NewContext();
        Assert.Equal(10, check.Products.First(x => x.Id == p.Id).Quantity);
        Assert.Empty(check.InventoryMovements.Where(m => m.MovementType == "تعديل جرد"));
    }

    // (f) A guard failure rolls back the whole approval (no partial changes).
    [Fact]
    public async Task Guard_failure_rolls_back_everything()
    {
        using var db = new TestDb();
        var pa = SeedProduct(db, "P-F1", 10, barcode: "BF1");
        var pb = SeedProduct(db, "P-F2", 2, barcode: "BF2");

        var svc = new TestableStocktakeService(db.Options);
        var session = await svc.CreateSessionAsync("s", "كامل");
        await svc.StartCountingAsync(session.Id);
        await svc.CountProductAsync(session.Id, pa.Id, 8); // diff -2 (would set A to 8)
        await svc.CountProductAsync(session.Id, pb.Id, 1); // diff -1
        SimulateSale(db, pb.Id, 2); // B 2 -> 0 after the count; approve would set B to -1 -> guard
        await svc.SubmitForReviewAsync(session.Id);

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.ApproveSessionAsync(session.Id));

        using var check = db.NewContext();
        Assert.Equal(10, check.Products.First(x => x.Id == pa.Id).Quantity); // A rolled back, NOT 8
        Assert.Equal(0, check.Products.First(x => x.Id == pb.Id).Quantity);  // B is the sale result, NOT -1
        Assert.Equal("مراجعة", check.StocktakeSessions.First(s => s.Id == session.Id).Status); // not معتمد
        Assert.Empty(check.InventoryMovements.Where(m => m.MovementType == "تعديل جرد"));
    }

    // (g) Movements sum equals the quantity changes.
    [Fact]
    public async Task Movements_sum_equals_quantity_changes()
    {
        using var db = new TestDb();
        var p1 = SeedProduct(db, "P-G1", 10, barcode: "BG1");
        var p2 = SeedProduct(db, "P-G2", 4, barcode: "BG2");
        var p3 = SeedProduct(db, "P-G3", 6, barcode: "BG3"); // counted equal -> diff 0

        var svc = new TestableStocktakeService(db.Options);
        var session = await svc.CreateSessionAsync("s", "كامل");
        await svc.StartCountingAsync(session.Id);
        await svc.CountProductAsync(session.Id, p1.Id, 7); // diff -3
        await svc.CountProductAsync(session.Id, p2.Id, 9); // diff +5
        await svc.CountProductAsync(session.Id, p3.Id, 6); // diff 0 -> no movement
        await svc.SubmitForReviewAsync(session.Id);
        await svc.ApproveSessionAsync(session.Id);

        using var check = db.NewContext();
        var movements = check.InventoryMovements.Where(m => m.MovementType == "تعديل جرد").ToList();
        Assert.Equal(2, movements.Count);
        Assert.Equal(2, movements.Sum(m => m.QuantityChange)); // -3 + 5 = 2
        Assert.Equal(7, check.Products.First(x => x.Id == p1.Id).Quantity);
        Assert.Equal(9, check.Products.First(x => x.Id == p2.Id).Quantity);
        Assert.Equal(6, check.Products.First(x => x.Id == p3.Id).Quantity);
        Assert.Contains(movements, m => m.QuantityChange == -3 && m.PreviousQuantity == 10 && m.NewQuantity == 7);
        Assert.Contains(movements, m => m.QuantityChange == 5 && m.PreviousQuantity == 4 && m.NewQuantity == 9);
    }
}
