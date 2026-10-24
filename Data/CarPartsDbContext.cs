using System.IO;
using Microsoft.EntityFrameworkCore;
using CarPartManager.Models;

namespace CarPartManager.Data
{
    /// <summary>
    /// سياق قاعدة البيانات المحلي (Entity Framework Core DbContext).
    /// يدير الاتصال بقاعدة بيانات SQLite وجداول الأصناف، الموردين، فواتير المشتريات، وحركات المخزون.
    /// </summary>
    public class CarPartsDbContext : DbContext
    {
        public DbSet<ProductModel> Products => Set<ProductModel>();
        public DbSet<SupplierModel> Suppliers => Set<SupplierModel>();
        public DbSet<PurchaseInvoiceModel> PurchaseInvoices => Set<PurchaseInvoiceModel>();
        public DbSet<PurchaseInvoiceItemModel> PurchaseInvoiceItems => Set<PurchaseInvoiceItemModel>();
        public DbSet<SalesInvoiceModel> SalesInvoices => Set<SalesInvoiceModel>();
        public DbSet<SalesInvoiceItemModel> SalesInvoiceItems => Set<SalesInvoiceItemModel>();
        public DbSet<SalesReturnModel> SalesReturns => Set<SalesReturnModel>();
        public DbSet<SalesReturnItemModel> SalesReturnItems => Set<SalesReturnItemModel>();
        public DbSet<PurchaseReturnModel> PurchaseReturns => Set<PurchaseReturnModel>();
        public DbSet<PurchaseReturnItemModel> PurchaseReturnItems => Set<PurchaseReturnItemModel>();
        public DbSet<InventoryMovementModel> InventoryMovements => Set<InventoryMovementModel>();

        // ── جداول العملاء (Customers Module) ──────────────────────────────────
        public DbSet<CustomerModel> Customers => Set<CustomerModel>();
        public DbSet<CustomerPaymentModel> CustomerPayments => Set<CustomerPaymentModel>();

        // ── جداول المصروفات (Expenses Module) ──────────────────────────────────
        public DbSet<ExpenseModel> Expenses => Set<ExpenseModel>();
        public DbSet<ExpenseCategoryModel> ExpenseCategories => Set<ExpenseCategoryModel>();
        public DbSet<ExpenseHistoryModel> ExpenseHistories => Set<ExpenseHistoryModel>();

        /// <summary>
        /// الحصول على المسار الآمن والدائم لملف قاعدة البيانات في LocalApplicationData
        /// لتجنب حفظها في bin/obj أو ربطها بمسار ثابت يحتوي اسم مستخدم محدد.
        /// </summary>
        public static string GetDatabaseFilePath()
        {
            var folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CarPartManager"
            );

            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            return Path.Combine(folder, "carparts.db");
        }

        public CarPartsDbContext()
        {
        }

        public CarPartsDbContext(DbContextOptions<CarPartsDbContext> options) : base(options)
        {
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                var dbPath = GetDatabaseFilePath();
                optionsBuilder.UseSqlite($"Data Source={dbPath}");
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // 1. تكوين جدول قطع الغيار والأصناف (Products)
            modelBuilder.Entity<ProductModel>(entity =>
            {
                entity.ToTable("Products");

                entity.HasKey(p => p.Id);
                entity.Property(p => p.Id).ValueGeneratedOnAdd();

                entity.Property(p => p.PartNumber)
                      .HasMaxLength(100)
                      .IsRequired();
                entity.HasIndex(p => p.PartNumber);

                entity.Property(p => p.Barcode)
                      .HasMaxLength(100);
                entity.HasIndex(p => p.Barcode);

                entity.Property(p => p.Name)
                      .HasMaxLength(250)
                      .IsRequired();

                entity.Property(p => p.CompatibleVehicles)
                      .HasMaxLength(300);

                entity.Property(p => p.Category)
                      .HasMaxLength(100)
                      .HasDefaultValue("عام");

                entity.Property(p => p.PurchasePrice)
                      .HasPrecision(18, 2);

                entity.Property(p => p.SellingPrice)
                      .HasPrecision(18, 2);

                entity.Property(p => p.Quantity)
                      .HasDefaultValue(0);

                entity.Property(p => p.DamagedQuantity)
                      .HasDefaultValue(0);

                entity.Property(p => p.MinimumStock)
                      .HasDefaultValue(5);

                entity.Property(p => p.Notes)
                      .HasMaxLength(500);

                // استبعاد الخصائص المحسوبة الخاصة بواجهة المستخدم من أعمدة قاعدة البيانات
                entity.Ignore(p => p.IsLowStock);
                entity.Ignore(p => p.ProfitMargin);
                entity.Ignore(p => p.StockStatusText);
                entity.Ignore(p => p.StockStatusColor);
                entity.Ignore(p => p.TotalStockQuantity);
            });

            // 2. تكوين جدول الموردين (Suppliers)
            modelBuilder.Entity<SupplierModel>(entity =>
            {
                entity.ToTable("Suppliers");

                entity.HasKey(s => s.Id);
                entity.Property(s => s.Id).ValueGeneratedOnAdd();

                entity.Property(s => s.Name)
                      .HasMaxLength(200)
                      .IsRequired();
                entity.HasIndex(s => s.Name);

                entity.Property(s => s.Phone)
                      .HasMaxLength(50);

                entity.Property(s => s.Address)
                      .HasMaxLength(300);

                entity.Property(s => s.CompanyName)
                      .HasMaxLength(200);

                entity.Property(s => s.Notes)
                      .HasMaxLength(500);

                entity.Property(s => s.IsActive)
                      .HasDefaultValue(true);
            });

            // 3. تكوين جدول فواتير المشتريات (PurchaseInvoices)
            modelBuilder.Entity<PurchaseInvoiceModel>(entity =>
            {
                entity.ToTable("PurchaseInvoices");

                entity.HasKey(p => p.Id);
                entity.Property(p => p.Id).ValueGeneratedOnAdd();

                // قيد فريد صارم على رقم الفاتورة لمنع التكرار
                entity.Property(p => p.InvoiceNumber)
                      .HasMaxLength(50)
                      .IsRequired();
                entity.HasIndex(p => p.InvoiceNumber).IsUnique();

                entity.Property(p => p.Status)
                      .HasMaxLength(30)
                      .HasDefaultValue("مسودة");

                entity.Property(p => p.SupplierName)
                      .HasMaxLength(200)
                      .IsRequired(false);

                entity.Property(p => p.TotalAmount)
                      .HasPrecision(18, 2);

                entity.Property(p => p.Notes)
                      .HasMaxLength(500)
                      .IsRequired(false);

                entity.Property(p => p.CancellationReason)
                      .HasMaxLength(500)
                      .IsRequired(false);

                // علاقة رأس بأطراف مع بنود الفاتورة
                entity.HasMany(p => p.Items)
                      .WithOne()
                      .HasForeignKey(i => i.PurchaseInvoiceId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.Ignore(p => p.IsDraft);
                entity.Ignore(p => p.IsPosted);
                entity.Ignore(p => p.IsCancelled);
                entity.Ignore(p => p.StatusColor);
            });

            // 4. تكوين جدول بنود فواتير المشتريات (PurchaseInvoiceItems)
            modelBuilder.Entity<PurchaseInvoiceItemModel>(entity =>
            {
                entity.ToTable("PurchaseInvoiceItems");

                entity.HasKey(i => i.Id);
                entity.Property(i => i.Id).ValueGeneratedOnAdd();

                entity.Property(i => i.PartNumber)
                      .HasMaxLength(100);

                entity.Property(i => i.ProductName)
                      .HasMaxLength(250);

                entity.Property(i => i.UnitPrice)
                      .HasPrecision(18, 2);

                entity.Ignore(i => i.TotalPrice);
            });

            // 5. تكوين جدول حركات المخزون (InventoryMovements)
            modelBuilder.Entity<InventoryMovementModel>(entity =>
            {
                entity.ToTable("InventoryMovements");

                entity.HasKey(m => m.Id);
                entity.Property(m => m.Id).ValueGeneratedOnAdd();

                entity.Property(m => m.PartNumber)
                      .HasMaxLength(100);

                entity.Property(m => m.ProductName)
                      .HasMaxLength(250);

                entity.Property(m => m.MovementType)
                      .HasMaxLength(50);

                entity.Property(m => m.ReferenceNumber)
                      .HasMaxLength(50);

                entity.Property(m => m.Notes)
                      .HasMaxLength(500);

                entity.HasIndex(m => m.ProductId);
                entity.HasIndex(m => m.MovementDate);

                entity.Ignore(m => m.PreviousBalance);
                entity.Ignore(m => m.NewBalance);
            });

            // 6. تكوين جدول فواتير المبيعات (SalesInvoices)
            modelBuilder.Entity<SalesInvoiceModel>(entity =>
            {
                entity.ToTable("SalesInvoices");

                entity.HasKey(s => s.Id);
                entity.Property(s => s.Id).ValueGeneratedOnAdd();

                entity.Property(s => s.InvoiceNumber)
                      .HasMaxLength(50)
                      .IsRequired();
                entity.HasIndex(s => s.InvoiceNumber).IsUnique();

                entity.Property(s => s.CustomerName)
                      .HasMaxLength(200)
                      .IsRequired(false);

                entity.Property(s => s.CustomerPhone)
                      .HasMaxLength(50)
                      .IsRequired(false);

                entity.Property(s => s.CustomerAddress)
                      .HasMaxLength(300)
                      .IsRequired(false);

                // ربط اختياري بجدول العملاء (nullable — الفواتير القديمة تبقى صالحة)
                entity.Property(s => s.CustomerId)
                      .IsRequired(false);

                entity.Property(s => s.Status)
                      .HasMaxLength(30)
                      .HasDefaultValue("مسودة");

                entity.Property(s => s.SubTotal)
                      .HasPrecision(18, 2);

                entity.Property(s => s.Discount)
                      .HasPrecision(18, 2);

                entity.Property(s => s.GrandTotal)
                      .HasPrecision(18, 2);

                entity.Property(s => s.PaidAmount)
                      .HasPrecision(18, 2);

                entity.Property(s => s.PaymentMethod)
                      .HasMaxLength(50)
                      .HasDefaultValue("نقدي");

                entity.Property(s => s.Notes)
                      .HasMaxLength(500)
                      .IsRequired(false);

                entity.Property(s => s.CancellationReason)
                      .HasMaxLength(500)
                      .IsRequired(false);

                entity.HasMany(s => s.Items)
                      .WithOne()
                      .HasForeignKey(i => i.SalesInvoiceId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.Ignore(s => s.IsDraft);
                entity.Ignore(s => s.IsPosted);
                entity.Ignore(s => s.IsCancelled);
                entity.Ignore(s => s.StatusColor);
                entity.Ignore(s => s.RemainingAmount);
            });

            // 12. تكوين جدول العملاء (Customers)
            modelBuilder.Entity<CustomerModel>(entity =>
            {
                entity.ToTable("Customers");

                entity.HasKey(c => c.Id);
                entity.Property(c => c.Id).ValueGeneratedOnAdd();

                entity.Property(c => c.Name)
                      .HasMaxLength(200)
                      .IsRequired();
                entity.HasIndex(c => c.Name);

                entity.Property(c => c.Phone)
                      .HasMaxLength(50)
                      .IsRequired(false);
                entity.HasIndex(c => c.Phone);

                entity.Property(c => c.Address)
                      .HasMaxLength(300)
                      .IsRequired(false);

                entity.Property(c => c.Notes)
                      .HasMaxLength(500)
                      .IsRequired(false);

                entity.Property(c => c.OpeningBalance)
                      .HasPrecision(18, 2)
                      .HasDefaultValue(0m);

                entity.Property(c => c.IsActive)
                      .HasDefaultValue(true);

                entity.Property(c => c.CreatedDate)
                      .IsRequired();

                // استبعاد الخصائص المحسوبة من قاعدة البيانات
                entity.Ignore(c => c.CurrentBalance);
                entity.Ignore(c => c.HasBalance);
                entity.Ignore(c => c.BalanceColor);
                entity.Ignore(c => c.StatusText);
                entity.Ignore(c => c.StatusColor);
                entity.Ignore(c => c.StatusBackground);
            });

            // 13. تكوين جدول دفعات العملاء (CustomerPayments)
            modelBuilder.Entity<CustomerPaymentModel>(entity =>
            {
                entity.ToTable("CustomerPayments");

                entity.HasKey(p => p.Id);
                entity.Property(p => p.Id).ValueGeneratedOnAdd();

                entity.Property(p => p.CustomerId)
                      .IsRequired();
                entity.HasIndex(p => p.CustomerId);

                entity.Property(p => p.Amount)
                      .HasPrecision(18, 2)
                      .IsRequired();

                entity.Property(p => p.PaymentDate)
                      .IsRequired();

                entity.Property(p => p.PaymentMethod)
                      .HasMaxLength(50)
                      .HasDefaultValue("نقدي");

                entity.Property(p => p.Notes)
                      .HasMaxLength(500)
                      .IsRequired(false);

                entity.Property(p => p.CreatedDate)
                      .IsRequired();

                entity.Property(p => p.IsCancelled)
                      .HasDefaultValue(false);

                entity.Property(p => p.CancellationDate)
                      .IsRequired(false);

                entity.Property(p => p.CancellationReason)
                      .HasMaxLength(500)
                      .IsRequired(false);

                entity.Ignore(p => p.StatusText);
                entity.Ignore(p => p.StatusColor);
            });

            // 7. تكوين جدول بنود فواتير المبيعات (SalesInvoiceItems)
            modelBuilder.Entity<SalesInvoiceItemModel>(entity =>
            {
                entity.ToTable("SalesInvoiceItems");

                entity.HasKey(i => i.Id);
                entity.Property(i => i.Id).ValueGeneratedOnAdd();

                entity.Property(i => i.PartNumber)
                      .HasMaxLength(100);

                entity.Property(i => i.ProductName)
                      .HasMaxLength(250);

                entity.Property(i => i.UnitPrice)
                      .HasPrecision(18, 2);

                entity.Ignore(i => i.TotalPrice);
                entity.Ignore(i => i.AvailableQuantity);
                entity.Ignore(i => i.Barcode);
            });

            // 8. تكوين جدول مرتجعات المبيعات (SalesReturns)
            modelBuilder.Entity<SalesReturnModel>(entity =>
            {
                entity.ToTable("SalesReturns");

                entity.HasKey(r => r.Id);
                entity.Property(r => r.Id).ValueGeneratedOnAdd();

                entity.Property(r => r.ReturnNumber)
                      .HasMaxLength(50)
                      .IsRequired();
                entity.HasIndex(r => r.ReturnNumber).IsUnique();

                entity.Property(r => r.SalesInvoiceNumber)
                      .HasMaxLength(50)
                      .IsRequired();

                entity.Property(r => r.CustomerName)
                      .HasMaxLength(200);

                entity.Property(r => r.Status)
                      .HasMaxLength(30)
                      .HasDefaultValue("معتمدة");

                entity.Property(r => r.TotalAmount)
                      .HasPrecision(18, 2);

                entity.Property(r => r.Reason)
                      .HasMaxLength(300);

                entity.Property(r => r.Notes)
                      .HasMaxLength(500);

                entity.HasMany(r => r.Items)
                      .WithOne()
                      .HasForeignKey(i => i.SalesReturnId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.Ignore(r => r.IsDraft);
                entity.Ignore(r => r.IsPosted);
                entity.Ignore(r => r.StatusColor);
            });

            // 9. تكوين جدول بنود مرتجعات المبيعات (SalesReturnItems)
            modelBuilder.Entity<SalesReturnItemModel>(entity =>
            {
                entity.ToTable("SalesReturnItems");

                entity.HasKey(i => i.Id);
                entity.Property(i => i.Id).ValueGeneratedOnAdd();

                entity.Property(i => i.PartNumber)
                      .HasMaxLength(100);

                entity.Property(i => i.ProductName)
                      .HasMaxLength(250);

                entity.Property(i => i.ItemCondition)
                      .HasMaxLength(50)
                      .HasDefaultValue("صالح للبيع");

                entity.Property(i => i.UnitPrice)
                      .HasPrecision(18, 2);

                entity.Ignore(i => i.TotalPrice);
                entity.Ignore(i => i.MaxReturnableQuantity);
                entity.Ignore(i => i.Condition);
                entity.Ignore(i => i.SalesReturn);
                entity.Ignore(i => i.Product);
            });

            // 10. تكوين جدول مرتجعات المشتريات (PurchaseReturns)
            modelBuilder.Entity<PurchaseReturnModel>(entity =>
            {
                entity.ToTable("PurchaseReturns");

                entity.HasKey(r => r.Id);
                entity.Property(r => r.Id).ValueGeneratedOnAdd();

                entity.Property(r => r.ReturnNumber)
                      .HasMaxLength(50)
                      .IsRequired();
                entity.HasIndex(r => r.ReturnNumber).IsUnique();

                entity.Property(r => r.PurchaseInvoiceNumber)
                      .HasMaxLength(50)
                      .IsRequired();

                entity.Property(r => r.SupplierName)
                      .HasMaxLength(200);

                entity.Property(r => r.Status)
                      .HasMaxLength(30)
                      .HasDefaultValue("معتمدة");

                entity.Property(r => r.TotalAmount)
                      .HasPrecision(18, 2);

                entity.Property(r => r.Reason)
                      .HasMaxLength(300);

                entity.Property(r => r.Notes)
                      .HasMaxLength(500);

                entity.HasMany(r => r.Items)
                      .WithOne()
                      .HasForeignKey(i => i.PurchaseReturnId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.Ignore(r => r.IsDraft);
                entity.Ignore(r => r.IsPosted);
                entity.Ignore(r => r.StatusColor);
            });

            // 11. تكوين جدول بنود مرتجعات المشتريات (PurchaseReturnItems)
            modelBuilder.Entity<PurchaseReturnItemModel>(entity =>
            {
                entity.ToTable("PurchaseReturnItems");

                entity.HasKey(i => i.Id);
                entity.Property(i => i.Id).ValueGeneratedOnAdd();

                entity.Property(i => i.PartNumber)
                      .HasMaxLength(100);

                entity.Property(i => i.ProductName)
                      .HasMaxLength(250);

                entity.Property(i => i.UnitPrice)
                      .HasPrecision(18, 2);

                entity.Ignore(i => i.TotalPrice);
                entity.Ignore(i => i.MaxReturnableQuantity);
                entity.Ignore(i => i.PurchaseReturn);
                entity.Ignore(i => i.Product);
            });

            // 12. تكوين جدول تصنيفات المصروفات (ExpenseCategories)
            modelBuilder.Entity<ExpenseCategoryModel>(entity =>
            {
                entity.ToTable("ExpenseCategories");

                entity.HasKey(c => c.Id);
                entity.Property(c => c.Id).ValueGeneratedOnAdd();

                entity.Property(c => c.Name)
                      .HasMaxLength(150)
                      .IsRequired();
                entity.HasIndex(c => c.Name);

                entity.Property(c => c.IsActive)
                      .HasDefaultValue(true);

                entity.Property(c => c.CreatedDate)
                      .IsRequired();

                entity.Ignore(c => c.IsUsed);
                entity.Ignore(c => c.UsageText);
                entity.Ignore(c => c.StatusText);
                entity.Ignore(c => c.StatusColor);
                entity.Ignore(c => c.StatusBackground);
            });

            // 13. تكوين جدول المصروفات (Expenses)
            modelBuilder.Entity<ExpenseModel>(entity =>
            {
                entity.ToTable("Expenses");

                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();

                entity.Property(e => e.ExpenseDate)
                      .IsRequired();
                entity.HasIndex(e => e.ExpenseDate);

                entity.Property(e => e.CategoryId)
                      .IsRequired();
                entity.HasIndex(e => e.CategoryId);

                entity.Property(e => e.Description)
                      .HasMaxLength(300)
                      .IsRequired(false);

                entity.Property(e => e.Amount)
                      .HasPrecision(18, 2)
                      .IsRequired();

                entity.Property(e => e.PaymentMethod)
                      .HasMaxLength(50)
                      .HasDefaultValue("نقدي");

                entity.Property(e => e.ReferenceNumber)
                      .HasMaxLength(100)
                      .IsRequired(false);

                entity.Property(e => e.Notes)
                      .HasMaxLength(500)
                      .IsRequired(false);

                entity.Property(e => e.IsCancelled)
                      .HasDefaultValue(false);

                entity.Property(e => e.CancellationReason)
                      .HasMaxLength(500)
                      .IsRequired(false);

                entity.Property(e => e.CreatedAt)
                      .IsRequired();

                entity.Property(e => e.CreatedBy)
                      .HasMaxLength(150)
                      .IsRequired(false);

                entity.Property(e => e.ModifiedBy)
                      .HasMaxLength(150)
                      .IsRequired(false);

                // ربط واجب بالتصنيف مع منع الحذف النهائي لأي تصنيف مستخدم (ON DELETE RESTRICT)
                entity.HasOne<ExpenseCategoryModel>()
                      .WithMany()
                      .HasForeignKey(e => e.CategoryId)
                      .OnDelete(DeleteBehavior.Restrict);

                // خصائص عرض محسوبة لا تُخزَّن في قاعدة البيانات
                entity.Ignore(e => e.StatusText);
                entity.Ignore(e => e.StatusColor);
                entity.Ignore(e => e.StatusBackground);
                entity.Ignore(e => e.UserDisplayName);
                entity.Ignore(e => e.ExpenseDateText);
                entity.Ignore(e => e.AmountText);
                entity.Ignore(e => e.ModifiedAtText);
                entity.Ignore(e => e.CategoryName);
            });

            // 14. تكوين جدول سجل تدقيق المصروفات (ExpenseHistories)
            modelBuilder.Entity<ExpenseHistoryModel>(entity =>
            {
                entity.ToTable("ExpenseHistories");

                entity.HasKey(h => h.Id);
                entity.Property(h => h.Id).ValueGeneratedOnAdd();

                entity.Property(h => h.ExpenseId)
                      .IsRequired();
                entity.HasIndex(h => h.ExpenseId);

                entity.Property(h => h.ChangedAt)
                      .IsRequired();
                entity.HasIndex(h => h.ChangedAt);

                entity.Property(h => h.ChangedBy)
                      .HasMaxLength(150)
                      .IsRequired(false);

                entity.Property(h => h.ChangeType)
                      .HasMaxLength(30)
                      .HasDefaultValue("تعديل");

                entity.Property(h => h.PreviousCategoryName)
                      .HasMaxLength(150)
                      .IsRequired(false);

                entity.Property(h => h.NewCategoryName)
                      .HasMaxLength(150)
                      .IsRequired(false);

                entity.Property(h => h.Reason)
                      .HasMaxLength(500)
                      .IsRequired(false);

                // ربط بسجل المصروف مع الحفاظ على سجل التدقيق (لا يُحذف المصروف أصلاً)
                entity.HasOne<ExpenseModel>()
                      .WithMany()
                      .HasForeignKey(h => h.ExpenseId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.Ignore(h => h.ChangedAtText);
                entity.Ignore(h => h.ChangedByText);
                entity.Ignore(h => h.ChangeTypeColor);
                entity.Ignore(h => h.PreviousAmountText);
                entity.Ignore(h => h.NewAmountText);
                entity.Ignore(h => h.AmountChangeText);
                entity.Ignore(h => h.CategoryChangeText);
                entity.Ignore(h => h.DateChangeText);
            });
        }
    }
}
