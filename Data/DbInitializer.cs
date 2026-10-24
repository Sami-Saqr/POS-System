using Microsoft.EntityFrameworkCore;
using CarPartManager.Models;
using CarPartManager.Services;

namespace CarPartManager.Data
{
    /// <summary>
    /// مسؤول تهيئة قاعدة البيانات والتأكد من وجود الجداول وتغذيتها ببيانات أولية إن كانت فارغة.
    /// يحافظ بالكامل على بيانات المستخدم ولا يقوم بحذف أو تصفير أي بيانات موجودة مسبقاً.
    /// </summary>
    public static class DbInitializer
    {
        public static List<string> DuplicatePhonesReport { get; } = new();
        public static bool IsUniquePhoneIndexApplied { get; private set; }

        public static async Task InitializeAsync(CarPartsDbContext context)
        {
            // 1. إنشاء قاعدة البيانات إن لم تكن موجودة
            await context.Database.EnsureCreatedAsync();

            // 2. ضمان وجود الجداول الجديدة بأمان دون حذف الجداول السابقة (Migration Safe Script)
            await EnsureTablesCreatedAsync(context);

            // 3. تغذية الأصناف الأولية فقط في حال كان جدول الأصناف فارغاً تماماً
            if (!await context.Products.AnyAsync())
            {
                await SeedInitialProductsAsync(context);
            }

            // 4. تغذية الموردين الأوليين فقط في حال كان جدول الموردين فارغاً تماماً
            if (!await context.Suppliers.AnyAsync())
            {
                await SeedInitialSuppliersAsync(context);
            }
        }

        private static async Task EnsureTablesCreatedAsync(CarPartsDbContext context)
        {
            // إنشاء جدول الموردين إن لم يكن موجوداً
            await context.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS ""Suppliers"" (
                    ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_Suppliers"" PRIMARY KEY AUTOINCREMENT,
                    ""Name"" TEXT NOT NULL,
                    ""Phone"" TEXT NULL,
                    ""Address"" TEXT NULL,
                    ""CompanyName"" TEXT NULL,
                    ""Notes"" TEXT NULL,
                    ""IsActive"" INTEGER NOT NULL DEFAULT 1
                );
                CREATE INDEX IF NOT EXISTS ""IX_Suppliers_Name"" ON ""Suppliers"" (""Name"");
            ");

            // إنشاء جدول فواتير المشتريات إن لم يكن موجوداً
            await context.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS ""PurchaseInvoices"" (
                    ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_PurchaseInvoices"" PRIMARY KEY AUTOINCREMENT,
                    ""InvoiceNumber"" TEXT NOT NULL,
                    ""InvoiceDate"" TEXT NOT NULL,
                    ""SupplierId"" INTEGER NULL,
                    ""SupplierName"" TEXT NULL,
                    ""Status"" TEXT NOT NULL DEFAULT 'مسودة',
                    ""PostedDate"" TEXT NULL,
                    ""TotalAmount"" TEXT NOT NULL,
                    ""Notes"" TEXT NULL
                );
                CREATE UNIQUE INDEX IF NOT EXISTS ""IX_PurchaseInvoices_InvoiceNumber"" ON ""PurchaseInvoices"" (""InvoiceNumber"");
            ");

            // إنشاء جدول بنود فواتير المشتريات إن لم يكن موجوداً
            await context.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS ""PurchaseInvoiceItems"" (
                    ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_PurchaseInvoiceItems"" PRIMARY KEY AUTOINCREMENT,
                    ""PurchaseInvoiceId"" INTEGER NOT NULL,
                    ""ProductId"" INTEGER NOT NULL,
                    ""PartNumber"" TEXT NULL,
                    ""ProductName"" TEXT NULL,
                    ""Quantity"" INTEGER NOT NULL,
                    ""UnitPrice"" TEXT NOT NULL,
                    CONSTRAINT ""FK_PurchaseInvoiceItems_PurchaseInvoices_PurchaseInvoiceId"" 
                        FOREIGN KEY (""PurchaseInvoiceId"") REFERENCES ""PurchaseInvoices"" (""Id"") ON DELETE CASCADE
                );
                CREATE INDEX IF NOT EXISTS ""IX_PurchaseInvoiceItems_PurchaseInvoiceId"" ON ""PurchaseInvoiceItems"" (""PurchaseInvoiceId"");
            ");

            // إنشاء جدول حركات المخزون إن لم يكن موجوداً
            await context.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS ""InventoryMovements"" (
                    ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_InventoryMovements"" PRIMARY KEY AUTOINCREMENT,
                    ""ProductId"" INTEGER NOT NULL,
                    ""PartNumber"" TEXT NULL,
                    ""ProductName"" TEXT NULL,
                    ""MovementType"" TEXT NULL,
                    ""QuantityChange"" INTEGER NOT NULL,
                    ""PreviousQuantity"" INTEGER NOT NULL,
                    ""NewQuantity"" INTEGER NOT NULL,
                    ""MovementDate"" TEXT NOT NULL,
                    ""ReferenceId"" INTEGER NULL,
                    ""ReferenceNumber"" TEXT NULL,
                    ""Notes"" TEXT NULL
                );
                CREATE INDEX IF NOT EXISTS ""IX_InventoryMovements_ProductId"" ON ""InventoryMovements"" (""ProductId"");
                CREATE INDEX IF NOT EXISTS ""IX_InventoryMovements_MovementDate"" ON ""InventoryMovements"" (""MovementDate"");
            ");

            // إنشاء جدول فواتير المبيعات إن لم يكن موجوداً
            await context.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS ""SalesInvoices"" (
                    ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_SalesInvoices"" PRIMARY KEY AUTOINCREMENT,
                    ""InvoiceNumber"" TEXT NOT NULL,
                    ""InvoiceDate"" TEXT NOT NULL,
                    ""CustomerName"" TEXT NULL,
                    ""CustomerPhone"" TEXT NULL,
                    ""Status"" TEXT NOT NULL DEFAULT 'مسودة',
                    ""SubTotal"" TEXT NOT NULL DEFAULT '0',
                    ""Discount"" TEXT NOT NULL DEFAULT '0',
                    ""GrandTotal"" TEXT NOT NULL DEFAULT '0',
                    ""PaymentMethod"" TEXT NOT NULL DEFAULT 'نقدي',
                    ""PaidAmount"" TEXT NOT NULL DEFAULT '0',
                    ""Notes"" TEXT NULL,
                    ""PostedDate"" TEXT NULL
                );
                CREATE UNIQUE INDEX IF NOT EXISTS ""IX_SalesInvoices_InvoiceNumber"" ON ""SalesInvoices"" (""InvoiceNumber"");
            ");

            // إنشاء جدول بنود فواتير المبيعات إن لم يكن موجوداً
            await context.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS ""SalesInvoiceItems"" (
                    ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_SalesInvoiceItems"" PRIMARY KEY AUTOINCREMENT,
                    ""SalesInvoiceId"" INTEGER NOT NULL,
                    ""ProductId"" INTEGER NOT NULL,
                    ""PartNumber"" TEXT NULL,
                    ""ProductName"" TEXT NULL,
                    ""Quantity"" INTEGER NOT NULL,
                    ""UnitPrice"" TEXT NOT NULL,
                    CONSTRAINT ""FK_SalesInvoiceItems_SalesInvoices_SalesInvoiceId"" 
                        FOREIGN KEY (""SalesInvoiceId"") REFERENCES ""SalesInvoices"" (""Id"") ON DELETE CASCADE
                );
                CREATE INDEX IF NOT EXISTS ""IX_SalesInvoiceItems_SalesInvoiceId"" ON ""SalesInvoiceItems"" (""SalesInvoiceId"");
            ");

            // إنشاء جدول مرتجعات المبيعات إن لم يكن موجوداً
            await context.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS ""SalesReturns"" (
                    ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_SalesReturns"" PRIMARY KEY AUTOINCREMENT,
                    ""ReturnNumber"" TEXT NOT NULL,
                    ""ReturnDate"" TEXT NOT NULL,
                    ""SalesInvoiceId"" INTEGER NOT NULL,
                    ""SalesInvoiceNumber"" TEXT NOT NULL,
                    ""CustomerName"" TEXT NULL,
                    ""Status"" TEXT NOT NULL DEFAULT 'معتمدة',
                    ""TotalAmount"" TEXT NOT NULL DEFAULT '0',
                    ""Reason"" TEXT NULL,
                    ""Notes"" TEXT NULL,
                    ""PostedDate"" TEXT NULL,
                    CONSTRAINT ""FK_SalesReturns_SalesInvoices_SalesInvoiceId""
                        FOREIGN KEY (""SalesInvoiceId"") REFERENCES ""SalesInvoices"" (""Id"") ON DELETE CASCADE
                );
                CREATE UNIQUE INDEX IF NOT EXISTS ""IX_SalesReturns_ReturnNumber"" ON ""SalesReturns"" (""ReturnNumber"");
                CREATE INDEX IF NOT EXISTS ""IX_SalesReturns_SalesInvoiceId"" ON ""SalesReturns"" (""SalesInvoiceId"");
            ");

            // إنشاء جدول بنود مرتجعات المبيعات إن لم يكن موجوداً
            await context.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS ""SalesReturnItems"" (
                    ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_SalesReturnItems"" PRIMARY KEY AUTOINCREMENT,
                    ""SalesReturnId"" INTEGER NOT NULL,
                    ""SalesInvoiceItemId"" INTEGER NOT NULL,
                    ""ProductId"" INTEGER NOT NULL,
                    ""PartNumber"" TEXT NULL,
                    ""ProductName"" TEXT NULL,
                    ""OriginalQuantity"" INTEGER NOT NULL,
                    ""PreviouslyReturnedQuantity"" INTEGER NOT NULL DEFAULT 0,
                    ""Quantity"" INTEGER NOT NULL,
                    ""UnitPrice"" TEXT NOT NULL,
                    ""ItemCondition"" TEXT NOT NULL DEFAULT 'صالح للبيع',
                    CONSTRAINT ""FK_SalesReturnItems_SalesReturns_SalesReturnId"" 
                        FOREIGN KEY (""SalesReturnId"") REFERENCES ""SalesReturns"" (""Id"") ON DELETE CASCADE
                );
                CREATE INDEX IF NOT EXISTS ""IX_SalesReturnItems_SalesReturnId"" ON ""SalesReturnItems"" (""SalesReturnId"");
            ");

            // إنشاء جدول مرتجعات المشتريات إن لم يكن موجوداً
            await context.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS ""PurchaseReturns"" (
                    ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_PurchaseReturns"" PRIMARY KEY AUTOINCREMENT,
                    ""ReturnNumber"" TEXT NOT NULL,
                    ""ReturnDate"" TEXT NOT NULL,
                    ""PurchaseInvoiceId"" INTEGER NOT NULL,
                    ""PurchaseInvoiceNumber"" TEXT NOT NULL,
                    ""SupplierId"" INTEGER NULL,
                    ""SupplierName"" TEXT NULL,
                    ""Status"" TEXT NOT NULL DEFAULT 'معتمدة',
                    ""TotalAmount"" TEXT NOT NULL DEFAULT '0',
                    ""Reason"" TEXT NULL,
                    ""Notes"" TEXT NULL,
                    ""PostedDate"" TEXT NULL,
                    CONSTRAINT ""FK_PurchaseReturns_PurchaseInvoices_PurchaseInvoiceId""
                        FOREIGN KEY (""PurchaseInvoiceId"") REFERENCES ""PurchaseInvoices"" (""Id"") ON DELETE CASCADE
                );
                CREATE UNIQUE INDEX IF NOT EXISTS ""IX_PurchaseReturns_ReturnNumber"" ON ""PurchaseReturns"" (""ReturnNumber"");
                CREATE INDEX IF NOT EXISTS ""IX_PurchaseReturns_PurchaseInvoiceId"" ON ""PurchaseReturns"" (""PurchaseInvoiceId"");
            ");

            // إنشاء جدول بنود مرتجعات المشتريات إن لم يكن موجوداً
            await context.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS ""PurchaseReturnItems"" (
                    ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_PurchaseReturnItems"" PRIMARY KEY AUTOINCREMENT,
                    ""PurchaseReturnId"" INTEGER NOT NULL,
                    ""PurchaseInvoiceItemId"" INTEGER NOT NULL,
                    ""ProductId"" INTEGER NOT NULL,
                    ""PartNumber"" TEXT NULL,
                    ""ProductName"" TEXT NULL,
                    ""OriginalQuantity"" INTEGER NOT NULL,
                    ""PreviouslyReturnedQuantity"" INTEGER NOT NULL DEFAULT 0,
                    ""Quantity"" INTEGER NOT NULL,
                    ""UnitPrice"" TEXT NOT NULL,
                    CONSTRAINT ""FK_PurchaseReturnItems_PurchaseReturns_PurchaseReturnId"" 
                        FOREIGN KEY (""PurchaseReturnId"") REFERENCES ""PurchaseReturns"" (""Id"") ON DELETE CASCADE
                );
                CREATE INDEX IF NOT EXISTS ""IX_PurchaseReturnItems_PurchaseReturnId"" ON ""PurchaseReturnItems"" (""PurchaseReturnId"");
            ");

            // ترقية آمنة للجداول الموجودة للحفاظ على البيانات وضمان وجود الأعمدة المضافة حديثاً
            try { await context.Database.ExecuteSqlRawAsync(@"ALTER TABLE ""Products"" ADD COLUMN ""DamagedQuantity"" INTEGER NOT NULL DEFAULT 0;"); } catch { }
            try { await context.Database.ExecuteSqlRawAsync(@"ALTER TABLE ""Products"" ADD COLUMN ""Barcode"" TEXT NULL;"); } catch { }
            try { await context.Database.ExecuteSqlRawAsync(@"CREATE UNIQUE INDEX IF NOT EXISTS ""IX_Products_Barcode_Unique"" ON ""Products"" (""Barcode"") WHERE ""Barcode"" IS NOT NULL AND ""Barcode"" != '';"); } catch { }
            try { await context.Database.ExecuteSqlRawAsync(@"ALTER TABLE ""PurchaseInvoices"" ADD COLUMN ""PostedDate"" TEXT NULL;"); } catch { }
            try { await context.Database.ExecuteSqlRawAsync(@"ALTER TABLE ""PurchaseInvoices"" ADD COLUMN ""CancellationDate"" TEXT NULL;"); } catch { }
            try { await context.Database.ExecuteSqlRawAsync(@"ALTER TABLE ""PurchaseInvoices"" ADD COLUMN ""CancellationReason"" TEXT NULL;"); } catch { }
            try { await context.Database.ExecuteSqlRawAsync(@"ALTER TABLE ""SalesInvoices"" ADD COLUMN ""CancellationDate"" TEXT NULL;"); } catch { }
            try { await context.Database.ExecuteSqlRawAsync(@"ALTER TABLE ""SalesInvoices"" ADD COLUMN ""CancellationReason"" TEXT NULL;"); } catch { }
            try { await context.Database.ExecuteSqlRawAsync(@"ALTER TABLE ""InventoryMovements"" ADD COLUMN ""ReferenceId"" INTEGER NULL;"); } catch { }

            // ── ترحيل وحدة العملاء (AddCustomersModule) ─────────────────────────────
            // إنشاء جدول العملاء إن لم يكن موجوداً
            await context.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS ""Customers"" (
                    ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_Customers"" PRIMARY KEY AUTOINCREMENT,
                    ""Name"" TEXT NOT NULL,
                    ""Phone"" TEXT NULL,
                    ""Address"" TEXT NULL,
                    ""Notes"" TEXT NULL,
                    ""OpeningBalance"" TEXT NOT NULL DEFAULT '0',
                    ""IsActive"" INTEGER NOT NULL DEFAULT 1,
                    ""CreatedDate"" TEXT NOT NULL DEFAULT (datetime('now'))
                );
                CREATE INDEX IF NOT EXISTS ""IX_Customers_Name"" ON ""Customers"" (""Name"");
                CREATE INDEX IF NOT EXISTS ""IX_Customers_Phone"" ON ""Customers"" (""Phone"");
            ");

            // إنشاء جدول دفعات العملاء إن لم يكن موجوداً
            await context.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS ""CustomerPayments"" (
                    ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_CustomerPayments"" PRIMARY KEY AUTOINCREMENT,
                    ""CustomerId"" INTEGER NOT NULL,
                    ""Amount"" TEXT NOT NULL,
                    ""PaymentDate"" TEXT NOT NULL,
                    ""PaymentMethod"" TEXT NOT NULL DEFAULT 'نقدي',
                    ""Notes"" TEXT NULL,
                    ""CreatedDate"" TEXT NOT NULL DEFAULT (datetime('now')),
                    ""IsCancelled"" INTEGER NOT NULL DEFAULT 0,
                    ""CancellationDate"" TEXT NULL,
                    ""CancellationReason"" TEXT NULL,
                    CONSTRAINT ""FK_CustomerPayments_Customers_CustomerId""
                        FOREIGN KEY (""CustomerId"") REFERENCES ""Customers"" (""Id"") ON DELETE RESTRICT
                );
                CREATE INDEX IF NOT EXISTS ""IX_CustomerPayments_CustomerId"" ON ""CustomerPayments"" (""CustomerId"");
            ");

            // ترقية جدول CustomerPayments للأعمدة الإضافية
            try { await context.Database.ExecuteSqlRawAsync(@"ALTER TABLE ""CustomerPayments"" ADD COLUMN ""IsCancelled"" INTEGER NOT NULL DEFAULT 0;"); } catch { }
            try { await context.Database.ExecuteSqlRawAsync(@"ALTER TABLE ""CustomerPayments"" ADD COLUMN ""CancellationDate"" TEXT NULL;"); } catch { }
            try { await context.Database.ExecuteSqlRawAsync(@"ALTER TABLE ""CustomerPayments"" ADD COLUMN ""CancellationReason"" TEXT NULL;"); } catch { }

            // إضافة عمود CustomerId و CustomerAddress إلى SalesInvoices بأمان (nullable لعدم المساس بالفواتير القديمة)
            try { await context.Database.ExecuteSqlRawAsync(@"ALTER TABLE ""SalesInvoices"" ADD COLUMN ""CustomerId"" INTEGER NULL;"); } catch { }
            try { await context.Database.ExecuteSqlRawAsync(@"ALTER TABLE ""SalesInvoices"" ADD COLUMN ""CustomerAddress"" TEXT NULL;"); } catch { }
            try { await context.Database.ExecuteSqlRawAsync(@"CREATE INDEX IF NOT EXISTS ""IX_SalesInvoices_CustomerId"" ON ""SalesInvoices"" (""CustomerId"") WHERE ""CustomerId"" IS NOT NULL;"); } catch { }

            // ── ترحيل الفهرس الفريد لهواتف العملاء بعد فحص التكرارات (UniqueCustomerPhoneMigration) ───
            await MigrateCustomerPhoneIndexAsync(context);

            // ── ترحيل وحدة المصروفات (AddExpensesModule) ───────────────────────────
            await EnsureExpenseTablesCreatedAsync(context);
            await SeedDefaultExpenseCategoriesAsync(context);

            // ── ترحيل حركات المخزون الافتتاحية (OpeningStockMovementsMigration) ───
            await BackfillOpeningStockMovementsAsync(context);
        }

        /// <summary>
        /// إنشاء جداول وحدة المصروفات بشكل تراكمي وآمن (CREATE TABLE IF NOT EXISTS)
        /// لا تُحذف ولا تُعدَّل أي بيانات أو جداول موجودة مسبقاً.
        /// </summary>
        private static async Task EnsureExpenseTablesCreatedAsync(CarPartsDbContext context)
        {
            // إنشاء جدول تصنيفات المصروفات إن لم يكن موجوداً
            await context.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS ""ExpenseCategories"" (
                    ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_ExpenseCategories"" PRIMARY KEY AUTOINCREMENT,
                    ""Name"" TEXT NOT NULL,
                    ""IsActive"" INTEGER NOT NULL DEFAULT 1,
                    ""CreatedDate"" TEXT NOT NULL
                );
                CREATE INDEX IF NOT EXISTS ""IX_ExpenseCategories_Name"" ON ""ExpenseCategories"" (""Name"");
            ");

            // إنشاء جدول المصروفات إن لم يكن موجوداً
            await context.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS ""Expenses"" (
                    ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_Expenses"" PRIMARY KEY AUTOINCREMENT,
                    ""ExpenseDate"" TEXT NOT NULL,
                    ""CategoryId"" INTEGER NOT NULL,
                    ""Description"" TEXT NULL,
                    ""Amount"" TEXT NOT NULL,
                    ""PaymentMethod"" TEXT NOT NULL DEFAULT 'نقدي',
                    ""ReferenceNumber"" TEXT NULL,
                    ""Notes"" TEXT NULL,
                    ""IsCancelled"" INTEGER NOT NULL DEFAULT 0,
                    ""CancellationDate"" TEXT NULL,
                    ""CancellationReason"" TEXT NULL,
                    ""CreatedAt"" TEXT NOT NULL,
                    ""CreatedBy"" TEXT NULL,
                    ""ModifiedAt"" TEXT NULL,
                    ""ModifiedBy"" TEXT NULL,
                    CONSTRAINT ""FK_Expenses_ExpenseCategories_CategoryId""
                        FOREIGN KEY (""CategoryId"") REFERENCES ""ExpenseCategories"" (""Id"") ON DELETE RESTRICT
                );
                CREATE INDEX IF NOT EXISTS ""IX_Expenses_ExpenseDate"" ON ""Expenses"" (""ExpenseDate"");
                CREATE INDEX IF NOT EXISTS ""IX_Expenses_CategoryId"" ON ""Expenses"" (""CategoryId"");
                CREATE INDEX IF NOT EXISTS ""IX_Expenses_IsCancelled"" ON ""Expenses"" (""IsCancelled"");
            ");

            // إنشاء جدول سجل تدقيق المصروفات إن لم يكن موجوداً
            await context.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS ""ExpenseHistories"" (
                    ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_ExpenseHistories"" PRIMARY KEY AUTOINCREMENT,
                    ""ExpenseId"" INTEGER NOT NULL,
                    ""ChangedAt"" TEXT NOT NULL,
                    ""ChangedBy"" TEXT NULL,
                    ""ChangeType"" TEXT NOT NULL DEFAULT 'تعديل',
                    ""PreviousAmount"" TEXT NULL,
                    ""NewAmount"" TEXT NULL,
                    ""PreviousCategoryName"" TEXT NULL,
                    ""NewCategoryName"" TEXT NULL,
                    ""PreviousExpenseDate"" TEXT NULL,
                    ""NewExpenseDate"" TEXT NULL,
                    ""Reason"" TEXT NULL,
                    CONSTRAINT ""FK_ExpenseHistories_Expenses_ExpenseId""
                        FOREIGN KEY (""ExpenseId"") REFERENCES ""Expenses"" (""Id"") ON DELETE CASCADE
                );
                CREATE INDEX IF NOT EXISTS ""IX_ExpenseHistories_ExpenseId"" ON ""ExpenseHistories"" (""ExpenseId"");
                CREATE INDEX IF NOT EXISTS ""IX_ExpenseHistories_ChangedAt"" ON ""ExpenseHistories"" (""ChangedAt"");
            ");
        }

        /// <summary>
        /// التصنيفات الافتراضية للمصروفات (تُضاف مرة واحدة فقط وبشكل تراكمي ثابت)
        /// </summary>
        private static readonly string[] DefaultExpenseCategoryNames =
        {
            "إيجار",
            "كهرباء ومياه",
            "مرتبات",
            "نقل وشحن",
            "صيانة",
            "تسويق",
            "مستلزمات",
            "أخرى"
        };

        /// <summary>
        /// تغذية التصنيفات الافتراضية للمصروفات (Idempotent Seed)
        /// لا يُضاف تصنيف موجود مسبقاً بنفس الاسم بعد توحيده (تقليم المسافات وتقليصها وتجاهل حالة الأحرف).
        /// </summary>
        private static async Task SeedDefaultExpenseCategoriesAsync(CarPartsDbContext context)
        {
            try
            {
                var existingNames = await context.ExpenseCategories
                    .AsNoTracking()
                    .Select(c => c.Name)
                    .ToListAsync();

                var existingNormalized = new HashSet<string>(
                    existingNames.Select(NameNormalizer.Normalize),
                    StringComparer.OrdinalIgnoreCase);

                var missing = DefaultExpenseCategoryNames
                    .Where(n => !existingNormalized.Contains(NameNormalizer.Normalize(n)))
                    .Select(n => new ExpenseCategoryModel
                    {
                        Name = n,
                        IsActive = true,
                        CreatedDate = DateTime.Now
                    })
                    .ToList();

                if (missing.Count == 0) return;

                await context.ExpenseCategories.AddRangeAsync(missing);
                await context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SeedDefaultExpenseCategories] {ex.Message}");
            }
        }

        private static async Task MigrateCustomerPhoneIndexAsync(CarPartsDbContext context)
        {
            try
            {
                var customersWithPhone = await context.Customers
                    .AsNoTracking()
                    .Where(c => c.Phone != null && c.Phone != "")
                    .Select(c => new { c.Id, c.Name, c.Phone })
                    .ToListAsync();

                var grouped = customersWithPhone
                    .Select(c => new { c.Id, c.Name, RawPhone = c.Phone, Normalized = PhoneHelper.Normalize(c.Phone) })
                    .Where(x => !string.IsNullOrEmpty(x.Normalized))
                    .GroupBy(x => x.Normalized)
                    .Where(g => g.Count() > 1)
                    .ToList();

                if (grouped.Any())
                {
                    DuplicatePhonesReport.Clear();
                    foreach (var g in grouped)
                    {
                        var details = string.Join("، ", g.Select(x => $"[ID: {x.Id}, الاسم: {x.Name}, الأصل: {x.RawPhone}]"));
                        DuplicatePhonesReport.Add($"الهاتف الموحد ({g.Key}) مكرر في: {details}");
                    }
                    // في حال وجود تكرار: لا نحذف ولا ندمج، ونستخدم فهرساً غير فريد
                    await context.Database.ExecuteSqlRawAsync(@"CREATE INDEX IF NOT EXISTS ""IX_Customers_Phone"" ON ""Customers"" (""Phone"");");
                    IsUniquePhoneIndexApplied = false;
                }
                else
                {
                    // لا توجد تكرارات: تحديث الأرقام غير الموحدة إلى شكلها الموحد
                    foreach (var c in customersWithPhone)
                    {
                        var norm = PhoneHelper.Normalize(c.Phone);
                        if (!string.IsNullOrEmpty(norm) && norm != c.Phone)
                        {
                            await context.Database.ExecuteSqlRawAsync(
                                @"UPDATE ""Customers"" SET ""Phone"" = {0} WHERE ""Id"" = {1}", norm, c.Id);
                        }
                    }

                    // تطبيق الفهرس الفريد المشروط على الأرقام غير الفارغة
                    await context.Database.ExecuteSqlRawAsync(@"
                        CREATE UNIQUE INDEX IF NOT EXISTS ""IX_Customers_Phone_Unique"" 
                        ON ""Customers"" (""Phone"") 
                        WHERE ""Phone"" IS NOT NULL AND ""Phone"" != '';
                    ");
                    IsUniquePhoneIndexApplied = true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MigrateCustomerPhoneIndex] {ex.Message}");
                try { await context.Database.ExecuteSqlRawAsync(@"CREATE INDEX IF NOT EXISTS ""IX_Customers_Phone"" ON ""Customers"" (""Phone"");"); } catch { }
            }
        }

        private static async Task SeedInitialProductsAsync(CarPartsDbContext context)
        {
            var initialProducts = new List<ProductModel>
            {
                new ProductModel
                {
                    PartNumber = "04465-02220",
                    Barcode = "622100100201",
                    Name = "فحمات فرامل أمامية سيراميك",
                    CompatibleVehicles = "تويوتا كورولا (2014 - 2021)",
                    Category = "مكابح وفرامل",
                    PurchasePrice = 130.00m,
                    SellingPrice = 185.00m,
                    Quantity = 14,
                    MinimumStock = 10,
                    Notes = "صناعة يابانية أصلية - رف A-12"
                },
                new ProductModel
                {
                    PartNumber = "28113-1R100",
                    Barcode = "622100100202",
                    Name = "فلتر هواء محرك أصلي",
                    CompatibleVehicles = "هيونداي إلنترا / أكسنت (2012 - 2020)",
                    Category = "فلاتر وزيوت",
                    PurchasePrice = 28.00m,
                    SellingPrice = 45.00m,
                    Quantity = 22,
                    MinimumStock = 15,
                    Notes = "وكالة موبيس كوريا - رف B-04"
                },
                new ProductModel
                {
                    PartNumber = "90919-01253",
                    Barcode = "622100100203",
                    Name = "بواجي إيريديوم ليزر (طقم 4 قطع)",
                    CompatibleVehicles = "تويوتا كامري / راف فور (2.5L)",
                    Category = "كهرباء وإشعال",
                    PurchasePrice = 110.00m,
                    SellingPrice = 160.00m,
                    Quantity = 8,
                    MinimumStock = 12,
                    Notes = "دينسو دنسو أصلي ياباني - رف C-01"
                },
                new ProductModel
                {
                    PartNumber = "15208-65F0A",
                    Barcode = "622100100204",
                    Name = "سيفون وفلتر زيت محرك",
                    CompatibleVehicles = "نيسان التيما / صني / باترول / إكس تريل",
                    Category = "فلاتر وزيوت",
                    PurchasePrice = 14.00m,
                    SellingPrice = 25.00m,
                    Quantity = 35,
                    MinimumStock = 20,
                    Notes = "نيسان أصلي مع جوان نحاس - رف B-02"
                },
                new ProductModel
                {
                    PartNumber = "54611-3X000",
                    Barcode = "622100100205",
                    Name = "مساعد أمامي غاز (يمين)",
                    CompatibleVehicles = "كيا سيراتو / هيونداي إلنترا MD",
                    Category = "مساعدات ونظام تعليق",
                    PurchasePrice = 210.00m,
                    SellingPrice = 290.00m,
                    Quantity = 4,
                    MinimumStock = 6,
                    Notes = "ماندو كوري أصلي - مستودع الأرضي"
                },
                new ProductModel
                {
                    PartNumber = "13568-19046",
                    Barcode = "622100100206",
                    Name = "سير تيمن كاتينا توقيت",
                    CompatibleVehicles = "تويوتا هايلوكس / برادو ديزل",
                    Category = "سيور وبكرات",
                    PurchasePrice = 95.00m,
                    SellingPrice = 145.00m,
                    Quantity = 9,
                    MinimumStock = 8,
                    Notes = "ميتسوبوشي ياباني مع ضمان 100 ألف كم"
                },
                new ProductModel
                {
                    PartNumber = "16400-0T030",
                    Barcode = "622100100207",
                    Name = "رديتر تبريد ماء ألمنيوم",
                    CompatibleVehicles = "تويوتا كورولا (2008 - 2013) أوتوماتيك",
                    Category = "تبريد وتكييف",
                    PurchasePrice = 380.00m,
                    SellingPrice = 520.00m,
                    Quantity = 3,
                    MinimumStock = 5,
                    Notes = "دينسو كويو ياباني جودة عالية"
                },
                new ProductModel
                {
                    PartNumber = "31210-0K040",
                    Barcode = "622100100208",
                    Name = "دزق وصحن كلتش ومجموعة فصل",
                    CompatibleVehicles = "تويوتا هايلوكس / فورتشنر ديزل 2.5/3.0",
                    Category = "كلتش وجيربوكس",
                    PurchasePrice = 420.00m,
                    SellingPrice = 590.00m,
                    Quantity = 5,
                    MinimumStock = 4,
                    Notes = "آيسين AISIN ياباني أصلي"
                }
            };

            await context.Products.AddRangeAsync(initialProducts);
            await context.SaveChangesAsync();
        }

        private static async Task SeedInitialSuppliersAsync(CarPartsDbContext context)
        {
            var initialSuppliers = new List<SupplierModel>
            {
                new SupplierModel
                {
                    Name = "شركة النيل لقطع غيار السيارات",
                    Phone = "01012345678",
                    Address = "القاهرة - سوق التوفيقية لقطع الغيار",
                    CompanyName = "شركة النيل للتجارة والاستيراد",
                    Notes = "وكيل معتمد لقطع غيار تويوتا وهوندا",
                    IsActive = true
                },
                new SupplierModel
                {
                    Name = "المركز المصري لمساعدات وبودي السيارات",
                    Phone = "01123456789",
                    Address = "القاهرة - شارع دمشق / مصر الجديدة",
                    CompanyName = "المركز المصري للتوريدات",
                    Notes = "موزع مساعدات ماندو وسيراتو وإلنترا",
                    IsActive = true
                },
                new SupplierModel
                {
                    Name = "مؤسسة الأهرام لفلاتر وزيوت المحركات",
                    Phone = "01234567890",
                    Address = "الجيزة - شارع فيصل الرئيسي",
                    CompanyName = "مؤسسة الأهرام التجارية",
                    Notes = "توريد فلاتر وبواجي أصلية ومطابقة",
                    IsActive = true
                }
            };

            await context.Suppliers.AddRangeAsync(initialSuppliers);
            await context.SaveChangesAsync();
        }

        /// <summary>
        /// ترحيل حركات المخزون الافتتاحية (Backfill Opening Stock Movements)
        /// ينشئ حركة "رصيد افتتاحي" لكل صنف ليس له حركة افتتاحية مسجلة مسبقاً
        /// هذه العملية ثابتة (idempotent) - يمكن تشغيلها عدة مرات دون تكرار
        /// </summary>
        private static async Task BackfillOpeningStockMovementsAsync(CarPartsDbContext context)
        {
            try
            {
                // Get all products
                var products = await context.Products.AsNoTracking().ToListAsync();

                foreach (var product in products)
                {
                    // Check if this product already has an opening movement
                    bool hasOpeningMovement = await context.InventoryMovements
                        .AsNoTracking()
                        .AnyAsync(m => m.ProductId == product.Id && m.MovementType == "رصيد افتتاحي");

                    if (!hasOpeningMovement && product.Quantity > 0)
                    {
                        // Create opening movement for current quantity
                        var openingMovement = new InventoryMovementModel
                        {
                            ProductId = product.Id,
                            PartNumber = product.PartNumber,
                            ProductName = product.Name,
                            MovementType = "رصيد افتتاحي",
                            QuantityChange = product.Quantity,
                            PreviousQuantity = 0,
                            NewQuantity = product.Quantity,
                            MovementDate = DateTime.Now,
                            Notes = "رصيد افتتاحي تم ترحيله من المخزون الحالي"
                        };

                        await context.InventoryMovements.AddAsync(openingMovement);
                    }
                }

                await context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[BackfillOpeningStockMovements] {ex.Message}");
            }

            // ── ترحيل وحدة الجرد (AddStocktakeModule) ─────────────────────────────
            await EnsureStocktakeTablesCreatedAsync(context);
        }

        private static async Task EnsureStocktakeTablesCreatedAsync(CarPartsDbContext context)
        {
            // إنشاء جدول جلسات الجرد إن لم يكن موجوداً
            await context.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS ""StocktakeSessions"" (
                    ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_StocktakeSessions"" PRIMARY KEY AUTOINCREMENT,
                    ""SessionNumber"" TEXT NOT NULL,
                    ""Name"" TEXT NOT NULL,
                    ""Notes"" TEXT NULL,
                    ""Status"" TEXT NOT NULL DEFAULT 'مسودة',
                    ""SessionDate"" TEXT NOT NULL,
                    ""StartDate"" TEXT NULL,
                    ""ApprovedDate"" TEXT NULL,
                    ""CancelledDate"" TEXT NULL,
                    ""CancellationReason"" TEXT NULL,
                    ""ScopeType"" TEXT NOT NULL DEFAULT 'كامل',
                    ""ScopeFilter"" TEXT NULL
                );
                CREATE UNIQUE INDEX IF NOT EXISTS ""IX_StocktakeSessions_SessionNumber"" ON ""StocktakeSessions"" (""SessionNumber"");
                CREATE INDEX IF NOT EXISTS ""IX_StocktakeSessions_Status"" ON ""StocktakeSessions"" (""Status"");
                CREATE INDEX IF NOT EXISTS ""IX_StocktakeSessions_SessionDate"" ON ""StocktakeSessions"" (""SessionDate"");
            ");

            // إنشاء جدول بنود الجرد إن لم يكن موجوداً
            await context.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS ""StocktakeLines"" (
                    ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_StocktakeLines"" PRIMARY KEY AUTOINCREMENT,
                    ""SessionId"" INTEGER NOT NULL,
                    ""ProductId"" INTEGER NOT NULL,
                    ""PartNumber"" TEXT NULL,
                    ""ProductName"" TEXT NULL,
                    ""Barcode"" TEXT NULL,
                    ""Category"" TEXT NULL,
                    ""BookQuantity"" INTEGER NOT NULL,
                    ""CountedQuantity"" INTEGER NULL,
                    ""CostPrice"" TEXT NOT NULL,
                    ""Notes"" TEXT NULL,
                    CONSTRAINT ""FK_StocktakeLines_StocktakeSessions_SessionId""
                        FOREIGN KEY (""SessionId"") REFERENCES ""StocktakeSessions"" (""Id"") ON DELETE CASCADE
                );
                CREATE INDEX IF NOT EXISTS ""IX_StocktakeLines_SessionId"" ON ""StocktakeLines"" (""SessionId"");
                CREATE INDEX IF NOT EXISTS ""IX_StocktakeLines_ProductId"" ON ""StocktakeLines"" (""ProductId"");
                CREATE INDEX IF NOT EXISTS ""IX_StocktakeLines_Barcode"" ON ""StocktakeLines"" (""Barcode"");
            ");
        }
    }
}
