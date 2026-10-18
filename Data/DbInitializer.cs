using Microsoft.EntityFrameworkCore;
using CarPartManager.Models;

namespace CarPartManager.Data
{
    /// <summary>
    /// مسؤول تهيئة قاعدة البيانات والتأكد من وجود الجداول وتغذيتها ببيانات أولية إن كانت فارغة.
    /// يحافظ بالكامل على بيانات المستخدم ولا يقوم بحذف أو تصفير أي بيانات موجودة مسبقاً.
    /// </summary>
    public static class DbInitializer
    {
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
                    CONSTRAINT ""FK_CustomerPayments_Customers_CustomerId""
                        FOREIGN KEY (""CustomerId"") REFERENCES ""Customers"" (""Id"") ON DELETE RESTRICT
                );
                CREATE INDEX IF NOT EXISTS ""IX_CustomerPayments_CustomerId"" ON ""CustomerPayments"" (""CustomerId"");
            ");

            // إضافة عمود CustomerId إلى SalesInvoices بأمان (nullable لعدم المساس بالفواتير القديمة)
            try { await context.Database.ExecuteSqlRawAsync(@"ALTER TABLE ""SalesInvoices"" ADD COLUMN ""CustomerId"" INTEGER NULL;"); } catch { }
            try { await context.Database.ExecuteSqlRawAsync(@"CREATE INDEX IF NOT EXISTS ""IX_SalesInvoices_CustomerId"" ON ""SalesInvoices"" (""CustomerId"") WHERE ""CustomerId"" IS NOT NULL;"); } catch { }
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
    }
}
