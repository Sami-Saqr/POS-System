using System.IO;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using CarPartManager.Data;
using CarPartManager.Models;
using CarPartManager.Services;
using CarPartManager.ViewModels;

namespace CarPartManager
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // تهيئة قاعدة البيانات المحلية SQLite بشكل آمن عند بدء التطبيق
            try
            {
                using var db = new CarPartsDbContext();
                await DbInitializer.InitializeAsync(db);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"تعذر تهيئة قاعدة البيانات المحلية SQLite:\n{ex.Message}",
                    "تنبيه قاعدة البيانات",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
            }

            // تشغيل الفحص الذاتي والاختبارات الآلية عند تمرير وسيط --run-tests
            if (e.Args.Contains("--run-tests"))
            {
                RunSelfDiagnostics();
                Shutdown(0);
            }
        }

        private void RunSelfDiagnostics()
        {
            SalesInvoicesViewModel.IsHeadlessTestMode = true;
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine("==================================================");
            Console.WriteLine("بدء الاختبارات الآلية والتحقق البرمجي لقاعدة بيانات SQLite");
            Console.WriteLine("==================================================");


            int passed = 0;
            int total = 0;

            void Assert(bool condition, string testName)
            {
                total++;
                if (condition)
                {
                    passed++;
                    Console.WriteLine($"[نجاح] {testName}");
                }
                else
                {
                    Console.WriteLine($"[فشل] {testName}");
                    throw new Exception($"Test failed: {testName}");
                }
            }

            // 1. التحقق من مسار ملف قاعدة البيانات
            var dbPath = CarPartsDbContext.GetDatabaseFilePath();
            Assert(!string.IsNullOrWhiteSpace(dbPath) && File.Exists(dbPath), $"وجود ملف قاعدة البيانات المحلية في المسار الآمن: {dbPath}");

            // 2. اختبار خدمة قاعدة البيانات الحقيقية SqliteProductService
            var sqliteService = new SqliteProductService();
            var allProducts = sqliteService.GetAllProductsAsync().GetAwaiter().GetResult().ToList();
            Assert(allProducts.Count >= 8, $"قراءة الأصناف من SQLite بنجاح (العدد الحالي: {allProducts.Count})");

            // 3. اختبار البحث في قاعدة البيانات الحقيقية برقم OEM واسم القطعة
            var sampleProduct = allProducts.FirstOrDefault(p => !string.IsNullOrEmpty(p.PartNumber)) ?? allProducts.First();
            var searchOem = sqliteService.SearchProductsAsync(sampleProduct.PartNumber).GetAwaiter().GetResult().ToList();
            Assert(searchOem.Count >= 1 && searchOem.Any(p => p.Id == sampleProduct.Id), $"البحث في SQLite برقم OEM ({sampleProduct.PartNumber}) يرجع القطعة بدقة");

            var namePart = sampleProduct.Name.Split(new[] { ' ', '-' }, StringSplitOptions.RemoveEmptyEntries)[0];
            var searchName = sqliteService.SearchProductsAsync(namePart).GetAwaiter().GetResult().ToList();
            Assert(searchName.Count >= 1, $"البحث في SQLite بجزء من اسم القطعة '{namePart}' يرجع النتائج المطابقة");

            // 4. اختبار دورة الإضافة والتعديل والحفظ الدائم (Persistence Lifecycle)
            var testOem = "PERSIST-TEST-" + Guid.NewGuid().ToString("N")[..6].ToUpper();
            var testProduct = new ProductModel
            {
                PartNumber = testOem,
                Barcode = "622999888111",
                Name = "طقم سيور محرك تجريبي للاختبار",
                CompatibleVehicles = "ميتسوبيشي لانسر شارك",
                Category = "سيور وبكرات",
                PurchasePrice = 250m,
                SellingPrice = 380m,
                Quantity = 7,
                MinimumStock = 4,
                Notes = "صنف مخصص للتحقق من الحفظ على القرص"
            };

            // أ. الإضافة إلى قاعدة البيانات
            var added = sqliteService.AddProductAsync(testProduct).GetAwaiter().GetResult();
            Assert(added.Id > 0, $"إضافة قطعة إلى SQLite وتوليد المعرف تلقائياً (ID = {added.Id})");

            // ب. التعديل في قاعدة البيانات
            added.SellingPrice = 410m;
            added.Quantity = 12;
            var updated = sqliteService.UpdateProductAsync(added).GetAwaiter().GetResult();
            Assert(updated, "تعديل السعر والكمية في SQLite بنجاح");

            // ج. محاكاة إغلاق التطبيق وإعادة فتحه (إنشاء سياق وقناة اتصال جديدة تماماً من القرص)
            var reopenedService = new SqliteProductService();
            var persistentCheck = reopenedService.GetByIdAsync(added.Id).GetAwaiter().GetResult();
            Assert(
                persistentCheck != null &&
                persistentCheck.SellingPrice == 410m &&
                persistentCheck.Quantity == 12 &&
                persistentCheck.PartNumber == testOem,
                "تأكيد الحفظ الدائم على القرص: إعادة قراءة القطعة بعد محاكاة إغلاق التطبيق ببياناتها المعدلة"
            );

            // د. حذف الصنف التجريبي لترك قاعدة البيانات نظيفة
            var deleted = reopenedService.DeleteProductAsync(added.Id).GetAwaiter().GetResult();
            Assert(deleted, "حذف الصنف التجريبي بنجاح لتنظيف قاعدة البيانات");

            var deletedCheck = reopenedService.GetByIdAsync(added.Id).GetAwaiter().GetResult();
            Assert(deletedCheck == null, "التأكد من عدم وجود الصنف بعد الحذف");

            // 5. اختبار قواعد التحقق في ProductDialogViewModel
            var dialogVm = new ProductDialogViewModel();

            // فحص الاسم الفارغ
            dialogVm.PartNumber = "OEM-555";
            dialogVm.Name = "";
            dialogVm.SaveCommand.Execute(null);
            Assert(dialogVm.HasError && dialogVm.ErrorMessage.Contains("اسم الصنف"), "رفض الصنف بدون اسم مع رسالة عربية واضحة");

            // فحص كود القطعة الفارغ
            dialogVm.Name = "دينمو أصلي";
            dialogVm.PartNumber = "";
            dialogVm.SaveCommand.Execute(null);
            Assert(dialogVm.HasError && dialogVm.ErrorMessage.Contains("كود القطعة"), "رفض الصنف بدون كود OEM مع رسالة واضحة");

            // فحص السعر السالب
            dialogVm.PartNumber = "OEM-555";
            dialogVm.SellingPriceText = "-50";
            dialogVm.SaveCommand.Execute(null);
            Assert(dialogVm.HasError && dialogVm.ErrorMessage.Contains("سعر البيع") && dialogVm.ErrorMessage.Contains("سالباً"), "رفض سعر البيع السالب");

            dialogVm.SellingPriceText = "200";
            dialogVm.PurchasePriceText = "-10";
            dialogVm.SaveCommand.Execute(null);
            Assert(dialogVm.HasError && dialogVm.ErrorMessage.Contains("سعر الشراء") && dialogVm.ErrorMessage.Contains("سالباً"), "رفض سعر الشراء السالب");

            // فحص الكمية السالبة
            dialogVm.PurchasePriceText = "120";
            dialogVm.QuantityText = "-5";
            dialogVm.SaveCommand.Execute(null);
            Assert(dialogVm.HasError && dialogVm.ErrorMessage.Contains("الكمية"), "رفض الكمية السالبة");

            // 6. اختبار شاشة عرض الأصناف ProductsViewModel مع خدمة SQLite
            var productsVm = new ProductsViewModel(sqliteService);
            productsVm.LoadProductsAsync().GetAwaiter().GetResult();
            Assert(productsVm.FilteredProducts.Count >= 8, "تحميل أصناف SQLite في الـ DataGrid الخاص بـ ProductsViewModel");

            var firstItem = productsVm.FilteredProducts.First();
            var searchWord = firstItem.Name.Split(new[] { ' ', '-' }, StringSplitOptions.RemoveEmptyEntries)[0];
            productsVm.SearchText = searchWord;
            Assert(productsVm.FilteredProducts.Count >= 1 && productsVm.FilteredProducts.Any(p => p.Id == firstItem.Id), $"التصفية الحية للبحث بالاسم ('{searchWord}') في شاشة الأصناف");

            productsVm.ClearSearchCommand.Execute(null);
            Assert(productsVm.FilteredProducts.Count >= 8, "مسح البحث وإعادة إظهار كافة أصناف قاعدة البيانات");

            // 7. اختبار التنقل في MainViewModel
            var mainVm = new MainViewModel();
            Assert(mainVm.CurrentViewModel is DashboardViewModel, "الشاشة الافتراضية هي لوحة التحكم DashboardViewModel");

            mainVm.NavigateCommand.Execute("Products");
            Assert(mainVm.CurrentViewModel is ProductsViewModel, "الانتقال بنجاح إلى شاشة الأصناف ProductsViewModel");

            mainVm.NavigateCommand.Execute("PurchaseInvoices");
            Assert(mainVm.CurrentViewModel is PurchaseInvoicesViewModel, "الانتقال بنجاح إلى شاشة فواتير المشتريات PurchaseInvoicesViewModel");

            mainVm.NavigateCommand.Execute("Suppliers");
            Assert(mainVm.CurrentViewModel is SuppliersViewModel, "الانتقال بنجاح إلى شاشة الموردين SuppliersViewModel");

            mainVm.NavigateCommand.Execute("Dashboard");
            Assert(mainVm.CurrentViewModel is DashboardViewModel, "العودة بنجاح إلى لوحة التحكم DashboardViewModel");

            // 8. اختبار خدمة الموردين SqliteSupplierService
            var supplierService = new SqliteSupplierService();
            var suppliers = supplierService.GetAllSuppliersAsync().GetAwaiter().GetResult().ToList();
            Assert(suppliers.Count >= 3, $"قراءة الموردين من SQLite بنجاح (العدد الحالي: {suppliers.Count})");

            var testSupplier = new SupplierModel
            {
                Name = "مؤسسة الأهرام لقطع الغيار - تجريبي",
                CompanyName = "الأهرام للتجارة",
                Phone = "01009988776",
                Address = "ش رمسيس، القاهرة",
                Notes = "مورد لاختبار عمليات الشراء",
                IsActive = true
            };
            var addedSupplier = supplierService.AddSupplierAsync(testSupplier).GetAwaiter().GetResult();
            Assert(addedSupplier.Id > 0, $"إضافة مورد جديد بنجاح (ID = {addedSupplier.Id})");

            // 9. اختبار توليد الترقيم التسلسلي لفواتير المشتريات
            var invoiceService = new SqlitePurchaseInvoiceService();
            var nextInvoiceNum = invoiceService.GenerateInvoiceNumberAsync().GetAwaiter().GetResult();
            Assert(nextInvoiceNum.StartsWith("PUR-") && nextInvoiceNum.Length >= 12, $"توليد رقم تسلسلي فريد لفاتورة الشراء: {nextInvoiceNum}");

            // 10. اختبار حفظ فاتورة الشراء كـ (مسودة) والتحقق من عدم المساس بالمخزون
            var targetProduct = allProducts.First();
            int stockBeforeDraft = targetProduct.Quantity;

            var draftInvoice = new PurchaseInvoiceModel
            {
                InvoiceNumber = nextInvoiceNum,
                InvoiceDate = DateTime.Now,
                SupplierId = addedSupplier.Id,
                SupplierName = addedSupplier.Name,
                Status = "مسودة",
                Notes = "فاتورة مسودة تجريبية للاختبار"
            };

            draftInvoice.Items.Add(new PurchaseInvoiceItemModel
            {
                ProductId = targetProduct.Id,
                PartNumber = targetProduct.PartNumber,
                ProductName = targetProduct.Name,
                Quantity = 5,
                UnitPrice = 120.00m
            });
            draftInvoice.RecalculateTotal();

            var savedDraft = invoiceService.SaveDraftInvoiceAsync(draftInvoice).GetAwaiter().GetResult();
            Assert(savedDraft.Id > 0 && savedDraft.Status == "مسودة", $"حفظ مسودة فاتورة الشراء بنجاح (ID = {savedDraft.Id})");

            // التحقق المؤكد من أن كمية الصنف لم تتأثر إطلاقاً بحفظ المسودة
            var freshProductCheckAfterDraft = sqliteService.GetByIdAsync(targetProduct.Id).GetAwaiter().GetResult();
            Assert(freshProductCheckAfterDraft != null && freshProductCheckAfterDraft.Quantity == stockBeforeDraft,
                $"الأمان المخزني للمسودة: رصيد الصنف ({targetProduct.Name}) لم يتغير وظل {stockBeforeDraft} قطعة");

            // 11. اختبار اعتماد الفاتورة (Post Invoice) وتحديث المخزون ذرياً مع سجل الحركات
            var postedInvoice = invoiceService.PostInvoiceAsync(savedDraft.Id).GetAwaiter().GetResult();
            Assert(postedInvoice.Status == "معتمدة" && postedInvoice.PostedDate.HasValue, "اعتماد الفاتورة وتغيير حالتها إلى 'معتمدة' مع توثيق تاريخ الاعتماد");

            // التحقق من زيادة رصيد المخزون في SQLite
            var freshProductCheckAfterPost = sqliteService.GetByIdAsync(targetProduct.Id).GetAwaiter().GetResult();
            int expectedStockAfterPost = stockBeforeDraft + 5;
            Assert(freshProductCheckAfterPost != null && freshProductCheckAfterPost.Quantity == expectedStockAfterPost,
                $"تحديث المخزون التلقائي: زاد رصيد الصنف من {stockBeforeDraft} إلى {expectedStockAfterPost} (+5 قطع واردة)");

            // التحقق من إنشاء سجل حركة المخزون (InventoryMovement Audit Log)
            using (var auditDb = new CarPartsDbContext())
            {
                var movement = auditDb.InventoryMovements
                    .FirstOrDefault(m => m.ReferenceId == postedInvoice.Id && m.ProductId == targetProduct.Id && m.MovementType == "شراء وارد");
                Assert(movement != null &&
                       movement.MovementType == "شراء وارد" &&
                       movement.QuantityChange == 5 &&
                       movement.PreviousBalance == stockBeforeDraft &&
                       movement.NewBalance == expectedStockAfterPost,
                    "تسجيل حركة المخزون الرقابية في جدول InventoryMovements مع الأرصدة السابقة والجديدة بدقة");
            }

            // 12. اختبار منع التكرار (Double-Posting Guard)
            bool doublePostPrevented = false;
            try
            {
                invoiceService.PostInvoiceAsync(postedInvoice.Id).GetAwaiter().GetResult();
            }
            catch (InvalidOperationException)
            {
                doublePostPrevented = true;
            }

            Assert(doublePostPrevented, "حماية قاعدة البيانات: منع اعتماد الفاتورة المعتمدة مرة أخرى وإلقاء استثناء يمنع التكرار");

            // والتأكد مجدداً أن الرصيد لم يزدد للمرة الثانية
            var doubleCheckProduct = sqliteService.GetByIdAsync(targetProduct.Id).GetAwaiter().GetResult();
            Assert(doubleCheckProduct != null && doubleCheckProduct.Quantity == expectedStockAfterPost,
                "ثبات الرصيد: لم يتم تكرار إضافة الكمية بعد محاولة الاعتماد المزدوج");

            // 13. اختبار استعادة الفاتورة وبنودها من القرص مع تفاصيل المورد
            var retrievedInvoice = invoiceService.GetInvoiceByIdAsync(postedInvoice.Id).GetAwaiter().GetResult();
            Assert(retrievedInvoice != null &&
                   retrievedInvoice.Items.Count == 1 &&
                   retrievedInvoice.TotalAmount == 600.00m && // 5 * 120 = 600
                   retrievedInvoice.Items[0].TotalPrice == 600.00m,
                "الحفظ العلائقي الدائم: قراءة الفاتورة المعتمدة مع كافة بنودها وإجمالياتها المحسوبة بدقة");

            // 14. اختبار نموذج عرض فواتير المشتريات PurchaseInvoicesViewModel
            var purchaseVm = new PurchaseInvoicesViewModel(invoiceService, sqliteService, supplierService);
            purchaseVm.LoadAllDataAsync().GetAwaiter().GetResult();
            if (!string.IsNullOrEmpty(purchaseVm.StatusMessage))
            {
                Console.WriteLine($"[تشخيص فواتير المشتريات]: {purchaseVm.StatusMessage}");
            }
            Console.WriteLine($"[تشخيص التاريخ]: InvoicesHistory.Count = {purchaseVm.InvoicesHistory.Count}");
            Assert(purchaseVm.SuppliersList.Count >= 3, "تحميل قائمة الموردين في الـ ComboBox الخاص بفاتورة الشراء");
            Assert(purchaseVm.FilteredAvailableProducts.Count >= 8, "تحميل الأصناف المتاحة للإضافة في جدول بنود الفاتورة");
            Assert(purchaseVm.InvoicesHistory.Count >= 1, "ظهور الفاتورة المعتمدة في أرشيف الفواتير السابقة");

            // 15. اختبار خدمة لوحة التحكم الحقيقية SqliteDashboardService
            var dashboardService = new SqliteDashboardService();
            var dashboardData = dashboardService.GetDashboardSummaryAsync().GetAwaiter().GetResult();
            Assert(dashboardData != null, "استخراج كائن ملخص لوحة التحكم من قاعدة البيانات بنجاح");
            if (dashboardData == null) return;
            Assert(dashboardData.TotalProductsCount >= 8, $"تطابق إجمالي عدد الأصناف المحسوب ({dashboardData.TotalProductsCount}) مع جدول الأصناف");
            Assert(dashboardData.TotalPurchasesCount >= 1, "حساب إجمالي فواتير المشتريات من قاعدة البيانات الحقيقية");
            Assert(dashboardData.RecentInvoices.Count >= 1, "استرجاع آخر فواتير المشتريات الفعلية مرتبة في لوحة التحكم");
            Assert(dashboardData.RecentInvoices[0].InvoiceCode.StartsWith("PUR-"), "ظهور كود فاتورة المشتريات الحقيقي في أحدث الفواتير");

            // فحص تطابق النواقص مع قاعدة البيانات
            int actualLowStockInDb;
            using (var checkDb = new CarPartsDbContext())
            {
                actualLowStockInDb = checkDb.Products.Count(p => p.Quantity <= p.MinimumStock);
            }
            Assert(dashboardData.LowStockProductsCount == actualLowStockInDb, 
                $"تطابق عدد الأصناف منخفضة المخزون ({dashboardData.LowStockProductsCount}) مع معادلة حد الأمان في SQLite ({actualLowStockInDb})");

            // 16. اختبار نموذج عرض لوحة التحكم DashboardViewModel وتحديث الخصائص الحية
            var dashboardVm = new DashboardViewModel(dashboardService);
            dashboardVm.LoadDashboardDataAsync().GetAwaiter().GetResult();
            Assert(dashboardVm.ProductCount.Contains(dashboardData.TotalProductsCount.ToString()), "عرض عدد الأصناف الحقيقي في بطاقة لوحة التحكم");
            Assert(dashboardVm.LowStockCount.Contains(dashboardData.LowStockProductsCount.ToString()), "عرض عدد النواقص الحقيقي في بطاقة لوحة التحكم");
            Assert(dashboardVm.RecentInvoices.Count == dashboardData.RecentInvoices.Count, "عرض الفواتير الفعلية في قائمة العمليات الحديثة");
            Assert(dashboardVm.LowStockItems.Count == dashboardData.LowStockItems.Count, "عرض النواقص الفعلية في جدول نواقص المستودع");

            // 17. اختبار التحديث التلقائي للوحة التحكم عند إضافة صنف جديد وتحديث النواقص
            int countBeforeAdd = dashboardData.TotalProductsCount;
            var liveTestProduct = new ProductModel
            {
                PartNumber = "LIVE-DASH-" + Guid.NewGuid().ToString("N")[..4].ToUpper(),
                Name = "صنف اختبار التحديث اللحظي للوحة التحكم",
                Category = "فلاتر وزيوت",
                PurchasePrice = 50m,
                SellingPrice = 80m,
                Quantity = 1, // أقل من الحد الأدنى (5) ليؤثر على النواقص أيضاً
                MinimumStock = 5
            };

            sqliteService.AddProductAsync(liveTestProduct).GetAwaiter().GetResult();
            // إطلاق حدث التحديث كما تفعل واجهات الإدخال
            DataEvents.RaiseInventoryOrInvoicesChanged();
            // قراءة بيانات لوحة التحكم بعد التحديث
            var refreshedDashboard = dashboardService.GetDashboardSummaryAsync().GetAwaiter().GetResult();
            Assert(refreshedDashboard.TotalProductsCount == countBeforeAdd + 1, "تحديث مؤشر إجمالي الأصناف فورياً بعد إضافة صنف جديد");
            Assert(refreshedDashboard.LowStockProductsCount == dashboardData.LowStockProductsCount + 1, "تحديث مؤشر تنبيهات النواقص فورياً عند إضافة صنف أقل من حد الأمان");

            // حذف الصنف التجريبي
            sqliteService.DeleteProductAsync(liveTestProduct.Id).GetAwaiter().GetResult();
            DataEvents.RaiseInventoryOrInvoicesChanged();

            // 18. تنظيف بيانات الاختبار المؤقتة
            // إعادة ضبط رصيد الصنف إلى حالته الأصلية
            targetProduct.Quantity = stockBeforeDraft;
            sqliteService.UpdateProductAsync(targetProduct).GetAwaiter().GetResult();

            // =========================================================================
            // اختبارات نظام فواتير المبيعات ونقاط البيع (Sales & POS Invoices Tests)
            // =========================================================================
            Console.WriteLine("--------------------------------------------------");
            Console.WriteLine("بدء اختبارات نظام فواتير المبيعات ونقاط البيع وخصم المخزون");
            Console.WriteLine("--------------------------------------------------");

            var salesService = new SqliteSalesInvoiceService();

            // 19. اختبار توليد الترقيم التسلسلي الفريد لفاتورة البيع
            var nextSalesNum = salesService.GenerateNextInvoiceNumberAsync().GetAwaiter().GetResult();
            Assert(nextSalesNum.StartsWith("INV-") && nextSalesNum.Length >= 12, $"توليد رقم تسلسلي فريد لفاتورة البيع: {nextSalesNum}");

            // 20. اختبار حفظ فاتورة بيع كـ (مسودة) والتحقق القاطع من عدم خصم المخزون
            var testProduct1 = allProducts[0];
            var testProduct2 = allProducts[1];
            if (testProduct1.Quantity < 10)
            {
                testProduct1.Quantity = 20;
                sqliteService.UpdateProductAsync(testProduct1).GetAwaiter().GetResult();
            }
            if (testProduct2.Quantity < 10)
            {
                testProduct2.Quantity = 20;
                sqliteService.UpdateProductAsync(testProduct2).GetAwaiter().GetResult();
            }
            int stockP1Before = testProduct1.Quantity;
            int stockP2Before = testProduct2.Quantity;

            var salesDraft = new SalesInvoiceModel
            {
                InvoiceNumber = nextSalesNum,
                InvoiceDate = DateTime.Now,
                CustomerName = "مركز الأمل للصيانة",
                CustomerPhone = "01055566778",
                Status = "مسودة",
                PaymentMethod = "نقدي",
                Discount = 20.00m,
                Notes = "فاتورة مسودة لاختبار عدم المساس بالمخزون"
            };

            salesDraft.Items.Add(new SalesInvoiceItemModel
            {
                ProductId = testProduct1.Id,
                PartNumber = testProduct1.PartNumber,
                ProductName = testProduct1.Name,
                Quantity = 2,
                UnitPrice = testProduct1.SellingPrice > 0 ? testProduct1.SellingPrice : 200m
            });
            salesDraft.RecalculateTotals();

            var savedSalesDraft = salesService.SaveDraftInvoiceAsync(salesDraft).GetAwaiter().GetResult();
            Assert(savedSalesDraft.Id > 0 && savedSalesDraft.Status == "مسودة", $"حفظ مسودة فاتورة البيع في SQLite بنجاح (ID = {savedSalesDraft.Id})");

            // التحقق من أن المخزون لم يُخصم إطلاقاً عند حفظ المسودة
            var p1FreshAfterDraft = sqliteService.GetByIdAsync(testProduct1.Id).GetAwaiter().GetResult();
            Assert(p1FreshAfterDraft != null && p1FreshAfterDraft.Quantity == stockP1Before,
                $"الأمان المخزني للمسودة: رصيد الصنف ({testProduct1.Name}) لم يتغير وظل {stockP1Before} قطعة");

            // 21. اختبار بيع عدة أصناف في فاتورة واحدة واعتمادها وخصم الكميات بدقة
            int qtyToSellP1 = 2;
            int qtyToSellP2 = 3;

            savedSalesDraft.Items.Add(new SalesInvoiceItemModel
            {
                ProductId = testProduct2.Id,
                PartNumber = testProduct2.PartNumber,
                ProductName = testProduct2.Name,
                Quantity = qtyToSellP2,
                UnitPrice = testProduct2.SellingPrice > 0 ? testProduct2.SellingPrice : 100m
            });
            savedSalesDraft.RecalculateTotals();

            // حفظ المسودة بعد إضافة البند الثاني
            savedSalesDraft = salesService.SaveDraftInvoiceAsync(savedSalesDraft).GetAwaiter().GetResult();
            Assert(savedSalesDraft.Items.Count == 2, "تحديث بنود مسودة البيع لتشمل عدة أصناف (2 أصناف)");

            // اعتماد الفاتورة وترحيلها وخصم المخزون ذرياً
            var postedSalesInvoice = salesService.PostInvoiceAsync(savedSalesDraft.Id).GetAwaiter().GetResult();
            Assert(postedSalesInvoice.Status == "معتمدة" && postedSalesInvoice.PostedDate.HasValue,
                "اعتماد فاتورة البيع وتغيير حالتها إلى 'معتمدة' وتوثيق وقت الاعتماد");

            // التحقق من خصم الكميات الفعلية من المستودع لكلا الصنفين
            var p1AfterPost = sqliteService.GetByIdAsync(testProduct1.Id).GetAwaiter().GetResult();
            var p2AfterPost = sqliteService.GetByIdAsync(testProduct2.Id).GetAwaiter().GetResult();
            Assert(p1AfterPost != null && p1AfterPost.Quantity == stockP1Before - qtyToSellP1,
                $"خصم المخزون للصنف الأول: نقص الرصيد من {stockP1Before} إلى {p1AfterPost?.Quantity} (-{qtyToSellP1} قطعة)");
            Assert(p2AfterPost != null && p2AfterPost.Quantity == stockP2Before - qtyToSellP2,
                $"خصم المخزون للصنف الثاني: نقص الرصيد من {stockP2Before} إلى {p2AfterPost?.Quantity} (-{qtyToSellP2} قطعة)");

            // 22. التحقق من توثيق حركات المخزون بنوع 'بيع صادر' والرصيد السالب
            using (var auditDb = new CarPartsDbContext())
            {
                var mov1 = auditDb.InventoryMovements
                    .FirstOrDefault(m => m.ReferenceId == postedSalesInvoice.Id && m.ProductId == testProduct1.Id && m.MovementType == "بيع صادر");
                var mov2 = auditDb.InventoryMovements
                    .FirstOrDefault(m => m.ReferenceId == postedSalesInvoice.Id && m.ProductId == testProduct2.Id && m.MovementType == "بيع صادر");

                Assert(mov1 != null && mov1.MovementType == "بيع صادر" && mov1.QuantityChange == -qtyToSellP1,
                    $"توثيق حركة البيع للصنف الأول بنوع 'بيع صادر' وكمية سالبة (-{qtyToSellP1}) في InventoryMovements");
                Assert(mov2 != null && mov2.MovementType == "بيع صادر" && mov2.QuantityChange == -qtyToSellP2,
                    $"توثيق حركة البيع للصنف الثاني بنوع 'بيع صادر' وكمية سالبة (-{qtyToSellP2}) في InventoryMovements");
            }

            // 23. اختبار منع الاعتماد المكرر (Double-Posting Guard)
            bool duplicatePostBlocked = false;
            try
            {
                salesService.PostInvoiceAsync(postedSalesInvoice.Id).GetAwaiter().GetResult();
            }
            catch (InvalidOperationException)
            {
                duplicatePostBlocked = true;
            }
            Assert(duplicatePostBlocked, "منع الاعتماد المزدوج لفاتورة البيع وإلقاء استثناء صريح يحمي المخزون");

            // التأكد أن رصيد المخزون لم يتناقص للمرة الثانية
            var p1AfterDupCheck = sqliteService.GetByIdAsync(testProduct1.Id).GetAwaiter().GetResult();
            Assert(p1AfterDupCheck?.Quantity == stockP1Before - qtyToSellP1, "ثبات الأرصدة وعدم تكرار الخصم بعد محاولة الاعتماد المزدوج");

            // 24. اختبار رفض بيع كمية أكبر من المخزون المتاح (Insufficient Stock Validation)
            var excessiveInvoice = new SalesInvoiceModel
            {
                InvoiceNumber = salesService.GenerateNextInvoiceNumberAsync().GetAwaiter().GetResult(),
                InvoiceDate = DateTime.Now,
                CustomerName = "عميل طلب كمية تفوق الرصيد",
                Status = "مسودة"
            };

            int currentStockP1 = p1AfterPost!.Quantity;
            excessiveInvoice.Items.Add(new SalesInvoiceItemModel
            {
                ProductId = testProduct1.Id,
                PartNumber = testProduct1.PartNumber,
                ProductName = testProduct1.Name,
                Quantity = currentStockP1 + 999, // كمية تفوق الرصيد قطعاً
                UnitPrice = 150m
            });
            excessiveInvoice.RecalculateTotals();

            bool outOfStockBlocked = false;
            try
            {
                salesService.PostInvoiceAsync(excessiveInvoice).GetAwaiter().GetResult();
            }
            catch (InvalidOperationException ex)
            {
                outOfStockBlocked = ex.Message.Contains("غير كافية");
            }
            Assert(outOfStockBlocked, "رفض اعتماد فاتورة بيع بكمية أكبر من المخزون المتوفر مع رسالة عربية واضحة");

            // التأكد من التراجع الذري وعدم خصم أي صنف عند فشل الاعتماد
            var p1RollbackCheck = sqliteService.GetByIdAsync(testProduct1.Id).GetAwaiter().GetResult();
            Assert(p1RollbackCheck?.Quantity == currentStockP1, "التراجع الذري (Transaction Rollback): بقاء رصيد الصنف ثابتاً دون أي خصم جزئي");

            // 25. اختبار استرجاع الفاتورة وبنودها من القرص ومحاكاة فتح التطبيق (Relational Persistence)
            var freshSalesService = new SqliteSalesInvoiceService();
            var reloadedInvoice = freshSalesService.GetInvoiceByIdAsync(postedSalesInvoice.Id).GetAwaiter().GetResult();
            Assert(reloadedInvoice != null &&
                   reloadedInvoice.Items.Count == 2 &&
                   reloadedInvoice.Status == "معتمدة" &&
                   reloadedInvoice.GrandTotal > 0 &&
                   reloadedInvoice.CustomerName == "مركز الأمل للصيانة",
                "استرجاع فاتورة البيع المعتمدة مع كافة بنودها وعميلها وإجمالياتها بدقة من SQLite");

            // 26. اختبار نموذج العرض SalesInvoicesViewModel وعمليات البحث والتصفية
            var salesVm = new SalesInvoicesViewModel(salesService, sqliteService);
            salesVm.LoadAllDataAsync().GetAwaiter().GetResult();
            Assert(salesVm.FilteredAvailableProducts.Count >= 8, "تحميل قائمة الأصناف المتاحة في SalesInvoicesViewModel");
            Assert(salesVm.InvoicesHistory.Count >= 1, "ظهور فاتورة البيع في أرشيف فواتير المبيعات");

            // تصفية الأرشيف برقم الفاتورة واسم العميل
            salesVm.SearchHistoryText = postedSalesInvoice.InvoiceNumber;
            Assert(salesVm.InvoicesHistory.Count == 1 && salesVm.InvoicesHistory[0].InvoiceNumber == postedSalesInvoice.InvoiceNumber,
                "تصفية أرشيف فواتير البيع برقم الفاتورة");

            salesVm.SearchHistoryText = "الأمل";
            Assert(salesVm.InvoicesHistory.Count >= 1 && salesVm.InvoicesHistory.Any(i => i.Id == postedSalesInvoice.Id),
                "تصفية أرشيف فواتير البيع باسم العميل");

            salesVm.SearchHistoryText = string.Empty;

            // 27. اختبار تكامل لوحة التحكم Dashboard مع مبيعات اليوم
            var dashAfterSales = dashboardService.GetDashboardSummaryAsync().GetAwaiter().GetResult();
            Assert(dashAfterSales.TodaySalesCount >= 1 && dashAfterSales.TodaySalesTotal > 0,
                $"تحديث لوحة التحكم بنجاح: رصد {dashAfterSales.TodaySalesCount} فاتورة بيع معتمدة بإجمالي {dashAfterSales.TodaySalesTotal:N2} ج.م");
            Assert(dashAfterSales.RecentInvoices.Any(i => i.InvoiceCode == postedSalesInvoice.InvoiceNumber),
                "ظهور فاتورة البيع في قائمة أحدث الفواتير الصادرة والواردة على Dashboard");

            // 28. اختبار MainViewModel والتنقل إلى فواتير المبيعات ونقاط البيع
            Assert(!mainVm.NavigationItems.Any(n => n.Tag == "SalesInvoices"), "إزالة قسم فواتير المبيعات من القائمة الجانبية بنجاح");
            Assert(mainVm.NavigationItems.Any(n => n.Tag == "POS"), "بقاء نقطة البيع (الكاشير) كمدخل أساسي في القائمة الجانبية");

            mainVm.NavigateCommand.Execute("SalesInvoices");
            Assert(mainVm.CurrentViewModel is SalesInvoicesViewModel, "الانتقال بنجاح إلى شاشة فواتير المبيعات SalesInvoicesViewModel");

            mainVm.NavigateCommand.Execute("POS");
            Assert(mainVm.CurrentViewModel is SalesInvoicesViewModel, "الانتقال بنجاح إلى شاشة نقطة البيع (الكاشير) SalesInvoicesViewModel");

            mainVm.NavigateCommand.Execute("Returns");
            Assert(mainVm.CurrentViewModel is ReturnsViewModel, "الانتقال بنجاح إلى شاشة المرتجعات ReturnsViewModel عبر شريط التنقل");

            // 29. اختبار التنقل المستقل لسجل فواتير المبيعات وسجل فواتير المشتريات
            Assert(mainVm.NavigationItems.Any(n => n.Tag == "SalesInvoicesHistory"), "وجود قسم سجل فواتير المبيعات في القائمة الجانبية");
            Assert(mainVm.NavigationItems.Any(n => n.Tag == "PurchaseInvoicesHistory"), "وجود قسم سجل فواتير المشتريات في القائمة الجانبية");

            mainVm.NavigateCommand.Execute("SalesInvoicesHistory");
            Assert(mainVm.CurrentViewModel is SalesInvoicesHistoryViewModel, "الانتقال بنجاح إلى شاشة سجل فواتير المبيعات SalesInvoicesHistoryViewModel");

            mainVm.NavigateCommand.Execute("PurchaseInvoicesHistory");
            Assert(mainVm.CurrentViewModel is PurchaseInvoicesHistoryViewModel, "الانتقال بنجاح إلى شاشة سجل فواتير المشتريات PurchaseInvoicesHistoryViewModel");

            var testSalesHistVm = new SalesInvoicesHistoryViewModel(salesService);
            testSalesHistVm.LoadAllDataAsync().GetAwaiter().GetResult();
            Assert(testSalesHistVm.InvoicesHistory.Count >= 1, "تحميل سجل فواتير المبيعات بنجاح في SalesInvoicesHistoryViewModel المستقل");

            var testPurHistVm = new PurchaseInvoicesHistoryViewModel(new SqlitePurchaseInvoiceService());
            testPurHistVm.LoadAllDataAsync().GetAwaiter().GetResult();
            Assert(testPurHistVm.InvoicesHistory.Count >= 1, "تحميل سجل فواتير المشتريات بنجاح في PurchaseInvoicesHistoryViewModel المستقل");

            // =========================================================================
            // الاختبارات الشاملة للمرتجعات وإلغاء الفواتير المعتمدة (الحالات 1 إلى 12)
            // =========================================================================
            Console.WriteLine("--------------------------------------------------");
            Console.WriteLine("بدء اختبارات دورة مرتجعات المبيعات والمشتريات وإلغاء الفواتير");
            Console.WriteLine("--------------------------------------------------");

            var returnsService = new SqliteReturnsService();
            var salesInvoiceService = new SqliteSalesInvoiceService();
            var purchaseInvoiceService = new SqlitePurchaseInvoiceService();

            // إعداد صنف تجريبي للاختبارات
            var returnTestProduct = new ProductModel
            {
                PartNumber = "RET-TEST-" + Guid.NewGuid().ToString("N")[..6].ToUpper(),
                Barcode = "BAR-RET-" + Guid.NewGuid().ToString("N")[..8],
                Name = "مساعدين خلفي ياباني اختبار",
                CompatibleVehicles = "تويوتا كورولا",
                Category = "عفشة ومساعدين",
                PurchasePrice = 600m,
                SellingPrice = 850m,
                Quantity = 50,
                MinimumStock = 5,
                Notes = "صنف مخصص لاختبار المرتجعات والإلغاء"
            };
            returnTestProduct = sqliteService.AddProductAsync(returnTestProduct).GetAwaiter().GetResult();
            Assert(returnTestProduct.Id > 0, $"إنشاء صنف تجريبي لاختبارات المرتجعات برصيد {returnTestProduct.Quantity} قطعة");

            // إنشاء واعتماد فاتورة بيع تجريبية بـ 10 قطع
            var testSalesInvoice = new SalesInvoiceModel
            {
                InvoiceNumber = salesInvoiceService.GenerateNextInvoiceNumberAsync().GetAwaiter().GetResult(),
                InvoiceDate = DateTime.Now,
                CustomerName = "عميل اختبار المرتجعات",
                CustomerPhone = "01000000000",
                PaymentMethod = "نقدي",
                Status = "مسودة",
                Items = new List<SalesInvoiceItemModel>
                {
                    new SalesInvoiceItemModel
                    {
                        ProductId = returnTestProduct.Id,
                        PartNumber = returnTestProduct.PartNumber,
                        ProductName = returnTestProduct.Name,
                        Quantity = 10,
                        UnitPrice = 850m
                    }
                }
            };
            testSalesInvoice = salesInvoiceService.PostInvoiceAsync(testSalesInvoice).GetAwaiter().GetResult();
            int stockAfterSale = sqliteService.GetByIdAsync(returnTestProduct.Id).GetAwaiter().GetResult()!.Quantity;
            Assert(testSalesInvoice.Status == "معتمدة" && stockAfterSale == 40,
                $"اعتماد فاتورة مبيعات بـ 10 قطع وخصم المخزون من 50 إلى {stockAfterSale}");

            // الحالة 1: مرتجع مبيعات جزئي (Partial Sales Return)
            var partialSalesReturn = new SalesReturnModel
            {
                ReturnNumber = returnsService.GenerateSalesReturnNumberAsync().GetAwaiter().GetResult(),
                SalesInvoiceId = testSalesInvoice.Id,
                ReturnDate = DateTime.Now,
                Reason = "طلب بالخطأ",
                Notes = "مرتجع جزئي صالح للبيع",
                Items = new List<SalesReturnItemModel>
                {
                    new SalesReturnItemModel
                    {
                        SalesInvoiceItemId = testSalesInvoice.Items[0].Id,
                        ProductId = returnTestProduct.Id,
                        ProductName = returnTestProduct.Name,
                        Quantity = 4,
                        UnitPrice = 850m,
                        Condition = "صالح للبيع"
                    }
                }
            };
            partialSalesReturn = returnsService.ProcessSalesReturnAsync(partialSalesReturn).GetAwaiter().GetResult();
            int stockAfterPartialReturn = sqliteService.GetByIdAsync(returnTestProduct.Id).GetAwaiter().GetResult()!.Quantity;
            Assert(partialSalesReturn.Status == "معتمدة" && stockAfterPartialReturn == 44,
                $"الحالة 1 (نجاح): مرتجع مبيعات جزئي لـ 4 قطع صالحة للبيع، وزيادة رصيد المستودع من 40 إلى {stockAfterPartialReturn}");

            // الحالة 2: مرتجع مبيعات كامل والتعامل مع القطع التالفة
            // متبقي 6 قطع: سنرجع 3 صالحة (تزيد المخزون) و3 تالفة (لا تزيد المخزون المتاح للبيع)
            var fullSalesReturn = new SalesReturnModel
            {
                ReturnNumber = returnsService.GenerateSalesReturnNumberAsync().GetAwaiter().GetResult(),
                SalesInvoiceId = testSalesInvoice.Id,
                ReturnDate = DateTime.Now,
                Reason = "عيب صناعة وتلف",
                Notes = "3 قطع صالحة و3 قطع تالفة",
                Items = new List<SalesReturnItemModel>
                {
                    new SalesReturnItemModel
                    {
                        SalesInvoiceItemId = testSalesInvoice.Items[0].Id,
                        ProductId = returnTestProduct.Id,
                        ProductName = returnTestProduct.Name,
                        Quantity = 3,
                        UnitPrice = 850m,
                        Condition = "صالح للبيع"
                    },
                    new SalesReturnItemModel
                    {
                        SalesInvoiceItemId = testSalesInvoice.Items[0].Id,
                        ProductId = returnTestProduct.Id,
                        ProductName = returnTestProduct.Name,
                        Quantity = 3,
                        UnitPrice = 850m,
                        Condition = "تالف"
                    }
                }
            };
            fullSalesReturn = returnsService.ProcessSalesReturnAsync(fullSalesReturn).GetAwaiter().GetResult();
            int stockAfterFullReturn = sqliteService.GetByIdAsync(returnTestProduct.Id).GetAwaiter().GetResult()!.Quantity;
            int damagedAfterFullReturn = sqliteService.GetByIdAsync(returnTestProduct.Id).GetAwaiter().GetResult()!.DamagedQuantity;
            Assert(stockAfterFullReturn == 47,
                $"الحالة 2 (نجاح): مرتجع مبيعات كامل، وإضافة 3 قطع صالحة فقط للمخزون (الرصيد: {stockAfterFullReturn}) مع تسجيل 3 قطع تالفة دون زيادتها للمخزون البيعي");
            Assert(damagedAfterFullReturn == 3,
                $"الحالة 2 (نجاح): توثيق رصيد التالف المستقل (DamagedQuantity = {damagedAfterFullReturn}) بنجاح");

            // الحالة 3: محاولة إرجاع كمية أكبر من المسموح
            bool exceededCaught = false;
            try
            {
                var illegalReturn = new SalesReturnModel
                {
                    ReturnNumber = returnsService.GenerateSalesReturnNumberAsync().GetAwaiter().GetResult(),
                    SalesInvoiceId = testSalesInvoice.Id,
                    ReturnDate = DateTime.Now,
                    Reason = "إرجاع زائد",
                    Items = new List<SalesReturnItemModel>
                    {
                        new SalesReturnItemModel
                        {
                            SalesInvoiceItemId = testSalesInvoice.Items[0].Id,
                            ProductId = returnTestProduct.Id,
                            ProductName = returnTestProduct.Name,
                            Quantity = 1,
                            UnitPrice = 850m,
                            Condition = "صالح للبيع"
                        }
                    }
                };
                returnsService.ProcessSalesReturnAsync(illegalReturn).GetAwaiter().GetResult();
            }
            catch (InvalidOperationException)
            {
                exceededCaught = true;
            }
            Assert(exceededCaught, "الحالة 3 (نجاح): رفض محاولة إرجاع كمية أكبر من الكمية المباعة المتبقية بعد اكتمال المرتجع");

            // الحالة 4: منع اعتماد المرتجع مرتين برقم مكرر
            bool doubleReturnCaught = false;
            try
            {
                var dupReturn = new SalesReturnModel
                {
                    ReturnNumber = partialSalesReturn.ReturnNumber, // رقم مكرر مسجل بالفعل
                    SalesInvoiceId = testSalesInvoice.Id,
                    Items = new List<SalesReturnItemModel>
                    {
                        new SalesReturnItemModel
                        {
                            SalesInvoiceItemId = testSalesInvoice.Items[0].Id,
                            ProductId = returnTestProduct.Id,
                            ProductName = returnTestProduct.Name,
                            Quantity = 1,
                            UnitPrice = 850m
                        }
                    }
                };
                returnsService.ProcessSalesReturnAsync(dupReturn).GetAwaiter().GetResult();
            }
            catch (InvalidOperationException)
            {
                doubleReturnCaught = true;
            }
            Assert(doubleReturnCaught, "الحالة 4 (نجاح): منع تكرار اعتماد المرتجع أو تكرار رقم إذن المرتجع");

            // الحالة 5: مرتجع مشتريات جزئي وكامل
            var testPurchaseInvoice = new PurchaseInvoiceModel
            {
                InvoiceNumber = purchaseInvoiceService.GenerateNextInvoiceNumberAsync().GetAwaiter().GetResult(),
                InvoiceDate = DateTime.Now,
                SupplierName = "مورد اختبار المرتجعات",
                Status = "مسودة",
                Items = new List<PurchaseInvoiceItemModel>
                {
                    new PurchaseInvoiceItemModel
                    {
                        ProductId = returnTestProduct.Id,
                        PartNumber = returnTestProduct.PartNumber,
                        ProductName = returnTestProduct.Name,
                        Quantity = 8,
                        UnitPrice = 600m
                    }
                }
            };
            testPurchaseInvoice = purchaseInvoiceService.PostInvoiceAsync(testPurchaseInvoice).GetAwaiter().GetResult();
            int stockAfterPurchase = sqliteService.GetByIdAsync(returnTestProduct.Id).GetAwaiter().GetResult()!.Quantity;
            Assert(testPurchaseInvoice.Status == "معتمدة" && stockAfterPurchase == 55,
                $"اعتماد فاتورة مشتريات بـ 8 قطع وزيادة الرصيد من 47 إلى {stockAfterPurchase}");

            // مرتجع مشتريات جزئي (3 قطع)
            var partialPurchaseReturn = new PurchaseReturnModel
            {
                ReturnNumber = returnsService.GeneratePurchaseReturnNumberAsync().GetAwaiter().GetResult(),
                PurchaseInvoiceId = testPurchaseInvoice.Id,
                ReturnDate = DateTime.Now,
                Reason = "غير مطابق",
                Items = new List<PurchaseReturnItemModel>
                {
                    new PurchaseReturnItemModel
                    {
                        PurchaseInvoiceItemId = testPurchaseInvoice.Items[0].Id,
                        ProductId = returnTestProduct.Id,
                        ProductName = returnTestProduct.Name,
                        Quantity = 3,
                        UnitPrice = 600m
                    }
                }
            };
            partialPurchaseReturn = returnsService.ProcessPurchaseReturnAsync(partialPurchaseReturn).GetAwaiter().GetResult();
            int stockAfterPartialPR = sqliteService.GetByIdAsync(returnTestProduct.Id).GetAwaiter().GetResult()!.Quantity;
            Assert(partialPurchaseReturn.Status == "معتمدة" && stockAfterPartialPR == 52,
                $"الحالة 5.أ (نجاح): مرتجع مشتريات جزئي للمورد بـ 3 قطع وخصم الرصيد إلى {stockAfterPartialPR}");

            // مرتجع مشتريات كامل للمتبقي (5 قطع)
            var fullPurchaseReturn = new PurchaseReturnModel
            {
                ReturnNumber = returnsService.GeneratePurchaseReturnNumberAsync().GetAwaiter().GetResult(),
                PurchaseInvoiceId = testPurchaseInvoice.Id,
                ReturnDate = DateTime.Now,
                Reason = "عيب توريد",
                Items = new List<PurchaseReturnItemModel>
                {
                    new PurchaseReturnItemModel
                    {
                        PurchaseInvoiceItemId = testPurchaseInvoice.Items[0].Id,
                        ProductId = returnTestProduct.Id,
                        ProductName = returnTestProduct.Name,
                        Quantity = 5,
                        UnitPrice = 600m
                    }
                }
            };
            fullPurchaseReturn = returnsService.ProcessPurchaseReturnAsync(fullPurchaseReturn).GetAwaiter().GetResult();
            int stockAfterFullPR = sqliteService.GetByIdAsync(returnTestProduct.Id).GetAwaiter().GetResult()!.Quantity;
            Assert(fullPurchaseReturn.Status == "معتمدة" && stockAfterFullPR == 47,
                $"الحالة 5.ب (نجاح): مرتجع مشتريات كامل لكامل الفاتورة وخصم المخزون إلى {stockAfterFullPR}");

            // الحالة 6: محاولة مرتجع مشتريات بكمية غير متاحة في المخزون
            // صنف كميته صفر، محاولة إرجاع قطعة منه
            var outOfStockProduct = new ProductModel
            {
                PartNumber = "OOS-TEST-" + Guid.NewGuid().ToString("N")[..6].ToUpper(),
                Name = "قطعة تجريبية نافدة",
                Category = "عام",
                PurchasePrice = 100m,
                Quantity = 0
            };
            outOfStockProduct = sqliteService.AddProductAsync(outOfStockProduct).GetAwaiter().GetResult();

            var oosPurchaseInvoice = new PurchaseInvoiceModel
            {
                InvoiceNumber = purchaseInvoiceService.GenerateNextInvoiceNumberAsync().GetAwaiter().GetResult(),
                InvoiceDate = DateTime.Now,
                SupplierName = "مورد عام",
                Status = "مسودة",
                Items = new List<PurchaseInvoiceItemModel>
                {
                    new PurchaseInvoiceItemModel
                    {
                        ProductId = outOfStockProduct.Id,
                        PartNumber = outOfStockProduct.PartNumber,
                        ProductName = outOfStockProduct.Name,
                        Quantity = 5,
                        UnitPrice = 100m
                    }
                }
            };
            oosPurchaseInvoice = purchaseInvoiceService.PostInvoiceAsync(oosPurchaseInvoice).GetAwaiter().GetResult();
            // تصريف المخزون يدوياً لمحاكاة نفاده بالكامل (الرصيد الحالي = 0)
            outOfStockProduct.Quantity = 0;
            sqliteService.UpdateProductAsync(outOfStockProduct).GetAwaiter().GetResult();

            bool oosReturnRejected = false;
            try
            {
                var oosReturn = new PurchaseReturnModel
                {
                    ReturnNumber = returnsService.GeneratePurchaseReturnNumberAsync().GetAwaiter().GetResult(),
                    PurchaseInvoiceId = oosPurchaseInvoice.Id,
                    Items = new List<PurchaseReturnItemModel>
                    {
                        new PurchaseReturnItemModel
                        {
                            PurchaseInvoiceItemId = oosPurchaseInvoice.Items[0].Id,
                            ProductId = outOfStockProduct.Id,
                            ProductName = outOfStockProduct.Name,
                            Quantity = 3,
                            UnitPrice = 100m
                        }
                    }
                };
                returnsService.ProcessPurchaseReturnAsync(oosReturn).GetAwaiter().GetResult();
            }
            catch (InvalidOperationException)
            {
                oosReturnRejected = true;
            }
            Assert(oosReturnRejected, "الحالة 6 (نجاح): رفض مرتجع المشتريات لعدم كفاية الرصيد بالمخزون لمنع حدوث مخزون سالب");

            // الحالة 7: إلغاء فاتورة شراء مع توفر الكمية اللازمة
            var cancelTestPurchase = new PurchaseInvoiceModel
            {
                InvoiceNumber = purchaseInvoiceService.GenerateNextInvoiceNumberAsync().GetAwaiter().GetResult(),
                InvoiceDate = DateTime.Now,
                SupplierName = "مورد تجريبي للإلغاء",
                Status = "مسودة",
                Items = new List<PurchaseInvoiceItemModel>
                {
                    new PurchaseInvoiceItemModel
                    {
                        ProductId = returnTestProduct.Id,
                        PartNumber = returnTestProduct.PartNumber,
                        ProductName = returnTestProduct.Name,
                        Quantity = 6,
                        UnitPrice = 600m
                    }
                }
            };
            cancelTestPurchase = purchaseInvoiceService.PostInvoiceAsync(cancelTestPurchase).GetAwaiter().GetResult();
            int stockBeforeCancel = sqliteService.GetByIdAsync(returnTestProduct.Id).GetAwaiter().GetResult()!.Quantity;
            Assert(stockBeforeCancel == 53, $"توريد 6 قطع للشراء، الرصيد قبل الإلغاء: {stockBeforeCancel}");

            var cancelSuccess = purchaseInvoiceService.CancelInvoiceAsync(cancelTestPurchase.Id, "إلغاء تجريبي مع توفر الرصيد").GetAwaiter().GetResult();
            int stockAfterCancel = sqliteService.GetByIdAsync(returnTestProduct.Id).GetAwaiter().GetResult()!.Quantity;
            using (var dbCheck = new CarPartsDbContext())
            {
                var cancelledDbInv = dbCheck.PurchaseInvoices.Find(cancelTestPurchase.Id);
                Assert(cancelSuccess && cancelledDbInv!.Status == "ملغاة" && stockAfterCancel == 47,
                    $"الحالة 7 (نجاح): إلغاء فاتورة المشتريات المعتمدة بأمان وعكس الرصيد إلى {stockAfterCancel} وتغيير الحالة لـ 'ملغاة'");
            }

            // الحالة 8: رفض إلغاء فاتورة شراء إذا كان عكس الحركة سيؤدي إلى مخزون سالب
            var noNegPurchase = new PurchaseInvoiceModel
            {
                InvoiceNumber = purchaseInvoiceService.GenerateNextInvoiceNumberAsync().GetAwaiter().GetResult(),
                InvoiceDate = DateTime.Now,
                SupplierName = "مورد اختبار منع السالب",
                Status = "مسودة",
                Items = new List<PurchaseInvoiceItemModel>
                {
                    new PurchaseInvoiceItemModel
                    {
                        ProductId = returnTestProduct.Id,
                        PartNumber = returnTestProduct.PartNumber,
                        ProductName = returnTestProduct.Name,
                        Quantity = 10,
                        UnitPrice = 600m
                    }
                }
            };
            noNegPurchase = purchaseInvoiceService.PostInvoiceAsync(noNegPurchase).GetAwaiter().GetResult();
            // تقليص المخزون إلى 2 فقط (أقل من الـ 10 التي يجب عكسها)
            returnTestProduct.Quantity = 2;
            sqliteService.UpdateProductAsync(returnTestProduct).GetAwaiter().GetResult();

            bool negCancelRejected = false;
            try
            {
                purchaseInvoiceService.CancelInvoiceAsync(noNegPurchase.Id, "محاولة إلغاء ستسبب مخزون سالب").GetAwaiter().GetResult();
            }
            catch (InvalidOperationException)
            {
                negCancelRejected = true;
            }
            int stockAfterReject = sqliteService.GetByIdAsync(returnTestProduct.Id).GetAwaiter().GetResult()!.Quantity;
            Assert(negCancelRejected && stockAfterReject == 2,
                "الحالة 8 (نجاح): رفض إلغاء فاتورة الشراء لأن الكمية المتاحة لا تكفي لعكس الحركة ومنع حدوث مخزون سالب");

            // الحالة 9: رفض الإلغاء المتكرر
            bool doubleCancelRejected = false;
            try
            {
                // cancelTestPurchase ملغاة بالفعل في الحالة 7
                purchaseInvoiceService.CancelInvoiceAsync(cancelTestPurchase.Id, "تكرار الإلغاء").GetAwaiter().GetResult();
            }
            catch (InvalidOperationException)
            {
                doubleCancelRejected = true;
            }
            Assert(doubleCancelRejected, "الحالة 9 (نجاح): رفض الإلغاء المتكرر لفاتورة ملغاة مسبقاً");

            // رفض إلغاء فاتورة عليها مرتجعات معتمدة
            bool cancelWithReturnsRejected = false;
            try
            {
                // testSalesInvoice عليها مرتجعات معتمدة
                salesInvoiceService.CancelInvoiceAsync(testSalesInvoice.Id, "محاولة إلغاء فاتورة عليها مرتجعات").GetAwaiter().GetResult();
            }
            catch (InvalidOperationException)
            {
                cancelWithReturnsRejected = true;
            }
            Assert(cancelWithReturnsRejected, "رفض إلغاء فاتورة مبيعات مسجل عليها مرتجعات معتمدة حماية لسلامة الحسابات");

            // الحالة 10: التأكد من صحة المخزون بعد إغلاق البرنامج وإعادة تشغيله (قراءة من قرص SQLite)
            using (var freshContext = new CarPartsDbContext())
            {
                var diskProduct = freshContext.Products.AsNoTracking().FirstOrDefault(p => p.Id == returnTestProduct.Id);
                Assert(diskProduct != null && diskProduct.Quantity == 2,
                    $"الحالة 10 (نجاح): التحقق من صحة المخزون على القرص بعد إعادة التهيئة ومطابقة الأرصدة (الرصيد = {diskProduct?.Quantity})");
            }

            // الحالة 11: التأكد من تحديث Dashboard وسجل الفواتير
            var dashMetrics = dashboardService.GetDashboardSummaryAsync().GetAwaiter().GetResult();
            Assert(dashMetrics.TodaySalesReturnsCount >= 1 && dashMetrics.TodayPurchaseReturnsCount >= 1,
                $"الحالة 11 (نجاح): تحديث مؤشرات لوحة التحكم ورصد المرتجعات ({dashMetrics.TodaySalesReturnsCount} بيع، {dashMetrics.TodayPurchaseReturnsCount} شراء)");
            Assert(dashMetrics.RecentInvoices.Any(i => i.TypeName.Contains("ملغاة")),
                "تمييز الفواتير الملغاة بوضوح في سجل الفواتير الأحدث على لوحة التحكم");

            // الحالة 12: التأكد من عدم حدوث تغييرات جزئية عند فشل المعاملة (Atomicity / Rollback)
            int movementsBeforeFail;
            using (var countContext = new CarPartsDbContext())
            {
                movementsBeforeFail = countContext.InventoryMovements.Count();
            }
            try
            {
                var failingReturn = new SalesReturnModel
                {
                    ReturnNumber = "FAIL-TX-" + Guid.NewGuid().ToString("N")[..4],
                    SalesInvoiceId = testSalesInvoice.Id,
                    Items = new List<SalesReturnItemModel>
                    {
                        new SalesReturnItemModel
                        {
                            SalesInvoiceItemId = 999999, // بند غير موجود يؤدي لفشل فوري
                            ProductId = returnTestProduct.Id,
                            Quantity = 5
                        }
                    }
                };
                returnsService.ProcessSalesReturnAsync(failingReturn).GetAwaiter().GetResult();
            }
            catch
            {
                // خطأ متوقع
            }
            int movementsAfterFail;
            using (var countContext = new CarPartsDbContext())
            {
                movementsAfterFail = countContext.InventoryMovements.Count();
            }
            Assert(movementsBeforeFail == movementsAfterFail,
                $"الحالة 12 (نجاح): التراجع التام (Rollback) عند فشل المعاملة وعدم حدوث أي تغييرات جزئية بالمخزون والحركات");

            // =========================================================================
            // 30. الاختبارات الموسعة للمتطلبات الجديدة (مصير المرتجع، مسح الفاتورة، ونقطة الكاشير)
            // =========================================================================
            Console.WriteLine("--------------------------------------------------");
            Console.WriteLine("بدء اختبارات المتطلبات الجديدة: مصير المرتجع ومسح الفاتورة ونقطة الكاشير");
            Console.WriteLine("--------------------------------------------------");

            // أ. اختبار مسح الفاتورة وتصفير الحقول دون التأثير على المخزون أو الأصناف
            var testPosVm = new SalesInvoicesViewModel(salesService, sqliteService);
            testPosVm.LoadAllDataAsync().GetAwaiter().GetResult();
            var pToAdd = testPosVm.FilteredAvailableProducts.First();
            int stockBeforeClear = pToAdd.Quantity;
            testPosVm.CurrentInvoiceItems.Add(new SalesInvoiceItemModel
            {
                ProductId = pToAdd.Id,
                PartNumber = pToAdd.PartNumber,
                ProductName = pToAdd.Name,
                Quantity = 5,
                UnitPrice = 100m
            });
            testPosVm.RecalculateTotals();
            Assert(testPosVm.CurrentInvoiceItems.Count == 1 && testPosVm.GrandTotal == 500m, "إضافة بنود للفاتورة في نموذج الكاشير بنجاح");

            // تنفيذ مسح الفاتورة (PrepareNewInvoiceAsync)
            testPosVm.PrepareNewInvoiceAsync().GetAwaiter().GetResult();
            Assert(testPosVm.CurrentInvoiceItems.Count == 0 && testPosVm.GrandTotal == 0 && testPosVm.CurrentInvoiceId == 0,
                "مسح الفاتورة وتصفير جميع البنود والإجماليات بنجاح");

            var stockAfterClear = sqliteService.GetByIdAsync(pToAdd.Id).GetAwaiter().GetResult()!.Quantity;
            Assert(stockBeforeClear == stockAfterClear, "أمان المخزون: مسح الفاتورة لا يؤثر إطلاقاً على أرصدة الأصناف بالمستودع");

            // ب. اختبار خيارات مصير المرتجع التالف الثلاثة
            var multiFateProduct = new ProductModel
            {
                PartNumber = "FATE-" + Guid.NewGuid().ToString("N")[..6].ToUpper(),
                Barcode = "BAR-FATE-" + Guid.NewGuid().ToString("N")[..8],
                Name = "مقصات أمامية رياضية اختبار مصير",
                CompatibleVehicles = "هيونداي إلنترا",
                Category = "تعليق",
                PurchasePrice = 500m,
                SellingPrice = 850m,
                Quantity = 50,
                DamagedQuantity = 0,
                MinimumStock = 5
            };
            multiFateProduct = sqliteService.AddProductAsync(multiFateProduct).GetAwaiter().GetResult();

            // 1. إنشاء فاتورة مبيعات جديدة بـ 9 قطع واعتمادها
            var multiFateInvoice = new SalesInvoiceModel
            {
                InvoiceNumber = salesInvoiceService.GenerateNextInvoiceNumberAsync().GetAwaiter().GetResult(),
                InvoiceDate = DateTime.Now,
                CustomerName = "مركز الاختبار المتقدم",
                PaymentMethod = "نقدي",
                Status = "معتمدة",
                Items = new List<SalesInvoiceItemModel>
                {
                    new SalesInvoiceItemModel
                    {
                        ProductId = multiFateProduct.Id,
                        PartNumber = multiFateProduct.PartNumber,
                        ProductName = multiFateProduct.Name,
                        Quantity = 9,
                        UnitPrice = 850m
                    }
                }
            };
            multiFateInvoice = salesInvoiceService.PostInvoiceAsync(multiFateInvoice).GetAwaiter().GetResult();
            int stockAfterPost9 = sqliteService.GetByIdAsync(multiFateProduct.Id).GetAwaiter().GetResult()!.Quantity;
            int damagedBefore = sqliteService.GetByIdAsync(multiFateProduct.Id).GetAwaiter().GetResult()!.DamagedQuantity;
            Assert(stockAfterPost9 == 41, $"خصم 9 قطع بنجاح: الرصيد الحالي بالمستودع = {stockAfterPost9}");

            // 2. مرتجع 3 قطع مع خيار (إرجاع إلى المخزون المتاح للبيع)
            var returnSellable = new SalesReturnModel
            {
                ReturnNumber = returnsService.GenerateSalesReturnNumberAsync().GetAwaiter().GetResult(),
                SalesInvoiceId = multiFateInvoice.Id,
                ReturnDate = DateTime.Now,
                Reason = "طلب زائد من العميل",
                Items = new List<SalesReturnItemModel>
                {
                    new SalesReturnItemModel
                    {
                        SalesInvoiceItemId = multiFateInvoice.Items[0].Id,
                        ProductId = multiFateProduct.Id,
                        ProductName = multiFateProduct.Name,
                        Quantity = 3,
                        UnitPrice = 850m,
                        Condition = "إرجاع إلى المخزون المتاح للبيع"
                    }
                }
            };
            returnsService.ProcessSalesReturnAsync(returnSellable).GetAwaiter().GetResult();
            int stockAfterSellableReturn = sqliteService.GetByIdAsync(multiFateProduct.Id).GetAwaiter().GetResult()!.Quantity;
            Assert(stockAfterSellableReturn == 44,
                $"خيار 1 (نجاح): إرجاع القطع الصالحة يزيد المخزون المتاح للبيع من 41 إلى {stockAfterSellableReturn} (+3 قطع)");

            // 3. مرتجع 3 قطع مع خيار (إرجاع إلى مخزون التالف)
            var returnDamaged = new SalesReturnModel
            {
                ReturnNumber = returnsService.GenerateSalesReturnNumberAsync().GetAwaiter().GetResult(),
                SalesInvoiceId = multiFateInvoice.Id,
                ReturnDate = DateTime.Now,
                Reason = "كسر في التغليف",
                Items = new List<SalesReturnItemModel>
                {
                    new SalesReturnItemModel
                    {
                        SalesInvoiceItemId = multiFateInvoice.Items[0].Id,
                        ProductId = multiFateProduct.Id,
                        ProductName = multiFateProduct.Name,
                        Quantity = 3,
                        UnitPrice = 850m,
                        Condition = "إرجاع إلى مخزون التالف"
                    }
                }
            };
            returnsService.ProcessSalesReturnAsync(returnDamaged).GetAwaiter().GetResult();
            var productAfterDamaged = sqliteService.GetByIdAsync(multiFateProduct.Id).GetAwaiter().GetResult()!;
            Assert(productAfterDamaged.Quantity == 44,
                $"خيار 2 (نجاح): القطع التالفة لا تضاف للمخزون المتاح للبيع (رصيد البيع ثابت: {productAfterDamaged.Quantity})");
            Assert(productAfterDamaged.DamagedQuantity == damagedBefore + 3,
                $"خيار 2 (نجاح): زيادة مخزون التالف المستقل بدقة إلى ({productAfterDamaged.DamagedQuantity}) قطعة");
            Assert(productAfterDamaged.TotalStockQuantity == productAfterDamaged.Quantity + productAfterDamaged.DamagedQuantity,
                $"خيار 2 (نجاح): إجمالي المخزون (صالح + تالف) = {productAfterDamaged.TotalStockQuantity}");

            // 4. مرتجع 3 قطع مع خيار (استبعاد نهائي من المخزون)
            var returnDisposal = new SalesReturnModel
            {
                ReturnNumber = returnsService.GenerateSalesReturnNumberAsync().GetAwaiter().GetResult(),
                SalesInvoiceId = multiFateInvoice.Id,
                ReturnDate = DateTime.Now,
                Reason = "تلف كلي غير صالح للاحتفاظ به",
                Items = new List<SalesReturnItemModel>
                {
                    new SalesReturnItemModel
                    {
                        SalesInvoiceItemId = multiFateInvoice.Items[0].Id,
                        ProductId = multiFateProduct.Id,
                        ProductName = multiFateProduct.Name,
                        Quantity = 3,
                        UnitPrice = 850m,
                        Condition = "استبعاد نهائي من المخزون"
                    }
                }
            };
            returnsService.ProcessSalesReturnAsync(returnDisposal).GetAwaiter().GetResult();
            var productAfterDisposal = sqliteService.GetByIdAsync(multiFateProduct.Id).GetAwaiter().GetResult()!;
            Assert(productAfterDisposal.Quantity == 44 && productAfterDisposal.DamagedQuantity == productAfterDamaged.DamagedQuantity,
                "خيار 3 (نجاح): استبعاد القطع نهائياً دون زيادتها للمخزون الصالح أو مخزون التالف");

            // التحقق من تسجيل الحركة التاريخية للاستبعاد النهائي
            using (var auditDb = new CarPartsDbContext())
            {
                var disposalMovement = auditDb.InventoryMovements
                    .FirstOrDefault(m => m.ReferenceNumber == returnDisposal.ReturnNumber && m.MovementType.Contains("استبعاد"));
                Assert(disposalMovement != null, "توثيق حركة مخزنية رسمية لنوع الاستبعاد النهائي في سجل الحركات");

                var savedReturnDisposal = auditDb.SalesReturns.Include(r => r.Items).FirstOrDefault(r => r.ReturnNumber == returnDisposal.ReturnNumber);
                Assert(savedReturnDisposal != null && savedReturnDisposal.Items.Count == 1,
                    "الاحتفاظ الكامل بسجل المرتجع الأصلي وتفاصيله وتاريخه في SQLite");
            }

            // 31. اختبارات الباركود وسجل الفواتير غير المتداخل
            Console.WriteLine("--------------------------------------------------");
            Console.WriteLine("بدء اختبارات الباركود في الكاشير وسجل الفواتير الجديد");
            Console.WriteLine("--------------------------------------------------");

            // أ. حفظ صنف بباركود يحتوي أصفار في البداية والتحقق من حفظه كنص
            string barcodeWithZeros = "00" + Random.Shared.Next(10000000, 99999999).ToString();
            var barcodeProduct1 = new ProductModel
            {
                PartNumber = "BAR-" + Guid.NewGuid().ToString("N")[..6].ToUpper(),
                Barcode = barcodeWithZeros,
                Name = "حساس أكسجين أصلي اختبار باركود",
                Category = "كهرباء",
                PurchasePrice = 200m,
                SellingPrice = 320m,
                Quantity = 5,
                MinimumStock = 2
            };
            barcodeProduct1 = sqliteService.AddProductAsync(barcodeProduct1).GetAwaiter().GetResult();

            var fetchedByBarcode = sqliteService.GetByBarcodeAsync(barcodeWithZeros).GetAwaiter().GetResult();
            Assert(fetchedByBarcode != null && fetchedByBarcode.Barcode == barcodeWithZeros,
                "إضافة صنف جديد مع باركود نصي يحتوي أصفاراً في البداية واسترجاعه بدقة");

            // ب. رفض تسجيل باركود مكرر لصنف آخر
            bool duplicateBarcodeRejected = false;
            try
            {
                var duplicateProduct = new ProductModel
                {
                    PartNumber = "DUP-" + Guid.NewGuid().ToString("N")[..6].ToUpper(),
                    Barcode = barcodeWithZeros,
                    Name = "صنف باركود مكرر غير مسموح",
                    Category = "عام",
                    PurchasePrice = 50m,
                    SellingPrice = 100m,
                    Quantity = 2
                };
                sqliteService.AddProductAsync(duplicateProduct).GetAwaiter().GetResult();
            }
            catch
            {
                duplicateBarcodeRejected = true;
            }
            Assert(duplicateBarcodeRejected, "رفض تسجيل باركود مكرر لصنف آخر بنجاح ومنع تكراره");

            // ج. السماح بأصناف بدون باركود دون تعارض
            var noBarcodeProd1 = new ProductModel
            {
                PartNumber = "NOBAR1-" + Guid.NewGuid().ToString("N")[..6].ToUpper(),
                Barcode = string.Empty,
                Name = "صنف 1 بدون باركود",
                Category = "عام",
                PurchasePrice = 20m,
                SellingPrice = 30m,
                Quantity = 10
            };
            var noBarcodeProd2 = new ProductModel
            {
                PartNumber = "NOBAR2-" + Guid.NewGuid().ToString("N")[..6].ToUpper(),
                Barcode = string.Empty,
                Name = "صنف 2 بدون باركود",
                Category = "عام",
                PurchasePrice = 25m,
                SellingPrice = 40m,
                Quantity = 10
            };
            noBarcodeProd1 = sqliteService.AddProductAsync(noBarcodeProd1).GetAwaiter().GetResult();
            noBarcodeProd2 = sqliteService.AddProductAsync(noBarcodeProd2).GetAwaiter().GetResult();
            Assert(noBarcodeProd1.Id > 0 && noBarcodeProd2.Id > 0, "السماح بتسجيل أصناف بدون باركود دون أي تعارض");

            // د. اختبار إضافة الصنف بالباركود في نموذج الكاشير SalesInvoicesViewModel
            var posBarcodeVm = new SalesInvoicesViewModel(salesInvoiceService, sqliteService);
            posBarcodeVm.LoadAllDataAsync().GetAwaiter().GetResult();
            posBarcodeVm.PrepareNewInvoiceAsync().GetAwaiter().GetResult();

            posBarcodeVm.BarcodeInput = barcodeWithZeros;
            posBarcodeVm.AddItemByBarcodeCommand.Execute(null);

            Assert(posBarcodeVm.CurrentInvoiceItems.Count == 1 &&
                   posBarcodeVm.CurrentInvoiceItems[0].ProductId == barcodeProduct1.Id &&
                   posBarcodeVm.CurrentInvoiceItems[0].Quantity == 1,
                   "إضافة الصنف إلى فاتورة البيع بكتابة أو مسح الباركود بنجاح");
            Assert(string.IsNullOrEmpty(posBarcodeVm.BarcodeInput),
                   "مسح حقل الباركود بعد الإضافة الناجحة والاستعداد للمسحة التالية");

            // هـ. زيادة كمية الصنف عند مسح الباركود نفسه مرة أخرى
            posBarcodeVm.BarcodeInput = barcodeWithZeros;
            posBarcodeVm.AddItemByBarcodeCommand.Execute(null);

            Assert(posBarcodeVm.CurrentInvoiceItems.Count == 1 &&
                   posBarcodeVm.CurrentInvoiceItems[0].Quantity == 2,
                   "زيادة كمية الصنف في الفاتورة عند قراءة الباركود نفسه مرة أخرى دون إنشاء سطر مكرر");

            // و. تطبيق قواعد المخزون ومنع تجاوز الكمية المتوفرة
            posBarcodeVm.BarcodeInput = barcodeWithZeros;
            posBarcodeVm.InputQuantity = 10; // المتاح 5، الفاتورة بها 2، الإجمالي المطلوب 12 > 5
            posBarcodeVm.AddItemByBarcodeCommand.Execute(null);

            Assert(posBarcodeVm.CurrentInvoiceItems[0].Quantity == 2,
                   "تطبيق قواعد المخزون ورفض تجاوز الكمية المتاحة عند إضافة صنف بالباركود");

            // ز. التعامل مع باركود غير موجود
            posBarcodeVm.BarcodeInput = "UNKNOWN_CODE_404";
            posBarcodeVm.AddItemByBarcodeCommand.Execute(null);
            Assert(posBarcodeVm.StatusMessage.Contains("لم يتم العثور") && string.IsNullOrEmpty(posBarcodeVm.BarcodeInput),
                   "التعامل مع باركود غير موجود وعرض رسالة عربية واضحة ومسح الحقل");

            // ط. اختبار الاقتراحات الحية للباركود (Live Barcode Suggestions)
            // 1) إنشاء أصناف تجريبية للتحقق من ترتيب البادئة (Starts-With أولاً ثم Contains) وحد النتائج (Take 8) والصنف نافد المخزون
            string testPrefix = "98765";
            var prefixProd1 = new ProductModel
            {
                PartNumber = "SUGG-PRE1-" + Guid.NewGuid().ToString("N")[..6].ToUpper(),
                Barcode = testPrefix + "111",
                Name = "صنف مقترح يبدأ بالبادئة 1",
                Category = "عام",
                PurchasePrice = 50m,
                SellingPrice = 80m,
                Quantity = 15
            };
            var prefixProd2 = new ProductModel
            {
                PartNumber = "SUGG-PRE2-" + Guid.NewGuid().ToString("N")[..6].ToUpper(),
                Barcode = testPrefix + "222",
                Name = "صنف مقترح يبدأ بالبادئة نافد",
                Category = "عام",
                PurchasePrice = 60m,
                SellingPrice = 90m,
                Quantity = 0
            };
            var containsProd = new ProductModel
            {
                PartNumber = "SUGG-CON1-" + Guid.NewGuid().ToString("N")[..6].ToUpper(),
                Barcode = "11" + testPrefix + "99",
                Name = "صنف مقترح يحتوي البادئة بالوسط",
                Category = "عام",
                PurchasePrice = 70m,
                SellingPrice = 110m,
                Quantity = 8
            };

            prefixProd1 = sqliteService.AddProductAsync(prefixProd1).GetAwaiter().GetResult();
            prefixProd2 = sqliteService.AddProductAsync(prefixProd2).GetAwaiter().GetResult();
            containsProd = sqliteService.AddProductAsync(containsProd).GetAwaiter().GetResult();

            // 2) اختبار خدمة قاعدة البيانات GetBarcodeSuggestionsAsync وترتيب النتائج
            var suggestions = sqliteService.GetBarcodeSuggestionsAsync(testPrefix).GetAwaiter().GetResult();
            Assert(suggestions.Count >= 3 && suggestions.Count <= 8, "استرجاع اقتراحات الباركود بحد أقصى 8 نتائج");

            // التأكد من أن الأصناف التي تبدأ بالبادئة تسبق الأصناف التي تحتويها فقط
            int idxPref1 = suggestions.FindIndex(p => p.Id == prefixProd1.Id);
            int idxPref2 = suggestions.FindIndex(p => p.Id == prefixProd2.Id);
            int idxCont = suggestions.FindIndex(p => p.Id == containsProd.Id);
            Assert(idxPref1 >= 0 && idxPref2 >= 0 && idxCont >= 0 && idxPref1 < idxCont && idxPref2 < idxCont,
                   "أولوية نتائج الاقتراحات: الباركود الذي يبدأ بالنص يأتي أولاً ثم الذي يحتويه");

            // 3) التحقق من خاصية IsOutOfStock
            var outOfStockSugg = suggestions.First(p => p.Id == prefixProd2.Id);
            Assert(outOfStockSugg.IsOutOfStock && outOfStockSugg.Quantity == 0,
                   "تمييز الصنف نافد المخزون IsOutOfStock = true في قائمة الاقتراحات");

            // 4) التحقق من عدم اقتراح الأصناف التي بدون باركود إطلاقاً
            var noBarcodeSugg = suggestions.Any(p => string.IsNullOrWhiteSpace(p.Barcode));
            Assert(!noBarcodeSugg, "عدم اقتراح أي منتج ليس له باركود مطلقاً");

            // 5) اختبار تفاعل نموذج العرض مع الاقتراحات والتنقل بلوحة المفاتيح
            posBarcodeVm.CloseBarcodeSuggestions();
            foreach (var s in suggestions) posBarcodeVm.BarcodeSuggestions.Add(s);
            posBarcodeVm.IsBarcodePopupOpen = true;

            // اختبار السهم للأسفل وللأعلى
            posBarcodeVm.SelectNextBarcodeSuggestion();
            Assert(posBarcodeVm.SelectedBarcodeSuggestion?.Id == suggestions[0].Id, "التنقل بالسهم لأسفل وتحديد المقترح الأول");

            posBarcodeVm.SelectNextBarcodeSuggestion();
            Assert(posBarcodeVm.SelectedBarcodeSuggestion?.Id == suggestions[1].Id, "التنقل بالسهم لأسفل وتحديد المقترح الثاني");

            posBarcodeVm.SelectPreviousBarcodeSuggestion();
            Assert(posBarcodeVm.SelectedBarcodeSuggestion?.Id == suggestions[0].Id, "التنقل بالسهم لأعلى والعودة للمقترح السابق");

            // 6) اختبار اختيار صنف نافد المخزون من الاقتراحات ومنع إضافته للفاتورة
            int itemsCountBefore = posBarcodeVm.CurrentInvoiceItems.Count;
            posBarcodeVm.SelectBarcodeSuggestionCommand.Execute(prefixProd2);
            Assert(posBarcodeVm.CurrentInvoiceItems.Count == itemsCountBefore && posBarcodeVm.StatusMessage.Contains("نفد من المخزون"),
                   "منع إضافة الصنف نافد المخزون عند اختياره من قائمة المقترحات مع رسالة تحذيرية");

            // 7) اختبار اختيار صنف متاح من الاقتراحات وإضافته بنجاح للفاتورة ومسح الحقل وإغلاق القائمة
            posBarcodeVm.SelectBarcodeSuggestionCommand.Execute(prefixProd1);
            Assert(posBarcodeVm.CurrentInvoiceItems.Any(i => i.ProductId == prefixProd1.Id) &&
                   string.IsNullOrEmpty(posBarcodeVm.BarcodeInput) &&
                   !posBarcodeVm.IsBarcodePopupOpen,
                   "إضافة الصنف المقترح إلى الفاتورة وإغلاق القائمة ومسح حقل الباركود للاستعداد للمسحة التالية");

            // 8) اختبار إغلاق القائمة بمفتاح Esc
            posBarcodeVm.IsBarcodePopupOpen = true;
            posBarcodeVm.CloseBarcodeSuggestions();
            Assert(!posBarcodeVm.IsBarcodePopupOpen && posBarcodeVm.SelectedBarcodeSuggestion == null,
                   "إغلاق قائمة الاقتراحات بنجاح بمفتاح Esc وإلغاء التحديد");

            // ح. التحقق من سلامة تحميل الفواتير في السجل عبر LoadInvoiceCommand
            var testHistoryPurchaseVm = new PurchaseInvoicesViewModel(purchaseInvoiceService, sqliteService, supplierService);
            testHistoryPurchaseVm.LoadAllDataAsync().GetAwaiter().GetResult();
            if (testHistoryPurchaseVm.InvoicesHistory.Count > 0)
            {
                var targetInv = testHistoryPurchaseVm.InvoicesHistory[0];
                testHistoryPurchaseVm.LoadInvoiceCommand.Execute(targetInv);
                Assert(testHistoryPurchaseVm.CurrentInvoiceId == targetInv.Id && testHistoryPurchaseVm.InvoiceNumber == targetInv.InvoiceNumber,
                       "أمر فتح الفاتورة في سجل فواتير المشتريات يعمل بدقة ودون أي تداخل");
            }

            if (posBarcodeVm.InvoicesHistory.Count > 0)
            {
                var targetSalesInv = posBarcodeVm.InvoicesHistory[0];
                posBarcodeVm.LoadInvoiceCommand.Execute(targetSalesInv);
                Assert(posBarcodeVm.CurrentInvoiceId == targetSalesInv.Id && posBarcodeVm.InvoiceNumber == targetSalesInv.InvoiceNumber,
                       "أمر فتح الفاتورة في سجل فواتير المبيعات يعمل بدقة ودون أي تداخل");
            }

            // --------------------------------------------------
            // اختبارات جدول بنود الفاتورة الحالية في الكاشير وعرض البيانات
            // --------------------------------------------------
            Console.WriteLine("--------------------------------------------------");
            Console.WriteLine("بدء اختبارات جدول بنود الفاتورة الحالية وتحديث البيانات الحي");
            Console.WriteLine("--------------------------------------------------");

            var posGridVm = new SalesInvoicesViewModel(salesService, sqliteService);
            posGridVm.LoadAllDataAsync().GetAwaiter().GetResult();
            var itemProduct = new ProductModel
            {
                Id = 99999,
                Name = "مساعدين خلفي ياباني",
                PartNumber = "OEM-SHOCK-55",
                Barcode = "8899001122",
                SellingPrice = 350m,
                Quantity = 20
            };

            // 1. إضافة صنف بالباركود والتحقق من حفظ الباركود في البند
            var addedItem = new SalesInvoiceItemModel(itemProduct, 2, 350m);
            posGridVm.CurrentInvoiceItems.Add(addedItem);
            Assert(addedItem.Barcode == "8899001122", "حفظ الباركود بدقة داخل بند الفاتورة (SalesInvoiceItemModel.Barcode)");
            Assert(addedItem.TotalPrice == 700m, "حساب إجمالي السطر بدقة (2 × 350 = 700)");
            Assert(posGridVm.GrandTotal == 700m, "تحديث إجمالي الفاتورة تلقائياً عند إضافة البند للقائمة");

            // 2. تعديل الكمية مباشرة واختبار تحديث الإجماليات اللحظي
            addedItem.Quantity = 5;
            Assert(addedItem.TotalPrice == 1750m, "تحديث إجمالي البند عند تعديل الكمية (5 × 350 = 1750)");
            Assert(posGridVm.GrandTotal == 1750m, "تحديث إجمالي الفاتورة تلقائياً في الوقت الفعلي عند تعديل الكمية بالجدول");

            // 3. اختبار تقييد الكمية (Clamping)
            addedItem.Quantity = 0; // محاولة إدخال صفر
            Assert(addedItem.Quantity == 1, "تقييد الكمية بالحد الأدنى (1) ومنع الكمية الصفرية أو السالبة");

            addedItem.Quantity = 25; // محاولة تجاوز المتاح بالمستودع (20)
            Assert(addedItem.Quantity == 20, "تقييد الكمية بالحد الأقصى المتاح بالمستودع (AvailableQuantity = 20)");

            // 4. حذف البند واختبار تصفير الإجمالي
            posGridVm.RemoveItemCommand.Execute(addedItem);
            Assert(posGridVm.CurrentInvoiceItems.Count == 0 && posGridVm.GrandTotal == 0m, "حذف البند وإعادة احتساب الإجماليات فورياً");

            // 29. تنظيف بيانات الاختبار وإعادة المخزون الأصلي بدقة
            testProduct1.Quantity = stockP1Before;
            testProduct2.Quantity = stockP2Before;
            sqliteService.UpdateProductAsync(testProduct1).GetAwaiter().GetResult();
            sqliteService.UpdateProductAsync(testProduct2).GetAwaiter().GetResult();

            // تنظيف أصناف وفواتير الاختبار المنشأة
            using (var cleanupDb = new CarPartsDbContext())
            {
                var testInv = cleanupDb.SalesInvoices.Include(s => s.Items).FirstOrDefault(s => s.Id == postedSalesInvoice.Id);
                if (testInv != null) cleanupDb.SalesInvoices.Remove(testInv);

                var sInv = cleanupDb.SalesInvoices.Include(s => s.Items).FirstOrDefault(s => s.Id == testSalesInvoice.Id);
                if (sInv != null) cleanupDb.SalesInvoices.Remove(sInv);

                var mInv = cleanupDb.SalesInvoices.Include(s => s.Items).FirstOrDefault(s => s.Id == multiFateInvoice.Id);
                if (mInv != null) cleanupDb.SalesInvoices.Remove(mInv);

                var pInv1 = cleanupDb.PurchaseInvoices.Include(p => p.Items).FirstOrDefault(p => p.Id == testPurchaseInvoice.Id);
                if (pInv1 != null) cleanupDb.PurchaseInvoices.Remove(pInv1);

                var pInv2 = cleanupDb.PurchaseInvoices.Include(p => p.Items).FirstOrDefault(p => p.Id == oosPurchaseInvoice.Id);
                if (pInv2 != null) cleanupDb.PurchaseInvoices.Remove(pInv2);

                var pInv3 = cleanupDb.PurchaseInvoices.Include(p => p.Items).FirstOrDefault(p => p.Id == cancelTestPurchase.Id);
                if (pInv3 != null) cleanupDb.PurchaseInvoices.Remove(pInv3);

                var pInv4 = cleanupDb.PurchaseInvoices.Include(p => p.Items).FirstOrDefault(p => p.Id == noNegPurchase.Id);
                if (pInv4 != null) cleanupDb.PurchaseInvoices.Remove(pInv4);

                var prd1 = cleanupDb.Products.Find(returnTestProduct.Id);
                if (prd1 != null) cleanupDb.Products.Remove(prd1);

                var prd2 = cleanupDb.Products.Find(outOfStockProduct.Id);
                if (prd2 != null) cleanupDb.Products.Remove(prd2);

                var prd3 = cleanupDb.Products.Find(barcodeProduct1.Id);
                if (prd3 != null) cleanupDb.Products.Remove(prd3);

                var prd4 = cleanupDb.Products.Find(noBarcodeProd1.Id);
                if (prd4 != null) cleanupDb.Products.Remove(prd4);

                var prd5 = cleanupDb.Products.Find(noBarcodeProd2.Id);
                if (prd5 != null) cleanupDb.Products.Remove(prd5);

                var prd6 = cleanupDb.Products.Find(prefixProd1.Id);
                if (prd6 != null) cleanupDb.Products.Remove(prd6);

                var prd7 = cleanupDb.Products.Find(prefixProd2.Id);
                if (prd7 != null) cleanupDb.Products.Remove(prd7);

                var prd8 = cleanupDb.Products.Find(containsProd.Id);
                if (prd8 != null) cleanupDb.Products.Remove(prd8);

                cleanupDb.SaveChanges();
            }

            // ==================================================
            // اختبارات وحدة إدارة العملاء، حساب الرصيد، والدفعات
            // ==================================================
            Console.WriteLine("--------------------------------------------------");
            Console.WriteLine("بدء اختبارات وحدة العملاء (الرصيد، الفواتير، الدفعات، كشف الحساب)");
            Console.WriteLine("--------------------------------------------------");

            var customerService = new SqliteCustomerService();
            var testPhone = "0109988" + Random.Shared.Next(1000, 9999);

            // 1. إضافة عميل مع رصيد افتتاحي
            var newCust = new CustomerModel
            {
                Name = "عميل تجريبي للاختبار الآلي",
                Phone = testPhone,
                Address = "القاهرة - مصر",
                Notes = "سجل اختبار لاحتساب الرصيد",
                OpeningBalance = 500.00m,
                IsActive = true
            };
            var addedCust = customerService.AddCustomerAsync(newCust).GetAwaiter().GetResult();
            Assert(addedCust.Id > 0, $"إضافة عميل جديد بنجاح في SQLite (ID = {addedCust.Id})");

            // 2. التحقق من الرصيد الأولي المطابق للرصيد الافتتاحي
            var initialBal = customerService.CalculateBalanceAsync(addedCust.Id).GetAwaiter().GetResult();
            Assert(initialBal == 500.00m, $"الرصيد الأولي للعميل يطابق الرصيد الافتتاحي بدقة (500.00 ج.م)");

            // 3. البحث بالاسم ورقم الهاتف
            var searchByName = customerService.SearchCustomersAsync("تجريبي للاختبار").GetAwaiter().GetResult().ToList();
            Assert(searchByName.Any(c => c.Id == addedCust.Id), "البحث عن العميل بالاسم يرجع النتيجة الصحيحة");

            var searchByPhone = customerService.SearchCustomersAsync(testPhone).GetAwaiter().GetResult().ToList();
            Assert(searchByPhone.Any(c => c.Id == addedCust.Id), "البحث عن العميل برقم الهاتف يرجع النتيجة الصحيحة");

            // 4. التحقق من فحص تكرار الهاتف
            var phoneDuplicate = customerService.PhoneExistsAsync(testPhone, excludeCustomerId: 0).GetAwaiter().GetResult();
            Assert(phoneDuplicate, "فحص تكرار رقم الهاتف يرصد الهاتف المسجل مسبقاً بنجاح");

            var phoneSelfExcluded = customerService.PhoneExistsAsync(testPhone, excludeCustomerId: addedCust.Id).GetAwaiter().GetResult();
            Assert(!phoneSelfExcluded, "استثناء معرف العميل الحالي عند فحص تكرار هاتفه لتسهيل التعديل");

            // 5. تعديل بيانات العميل
            addedCust.Address = "الجيزة - شارع الهرم";
            var updatedCust = customerService.UpdateCustomerAsync(addedCust).GetAwaiter().GetResult();
            Assert(updatedCust, "تعديل عنوان العميل في SQLite بنجاح");

            // 6. تعطيل وتفعيل العميل بدلاً من الحذف
            var deactivated = customerService.SetActiveStatusAsync(addedCust.Id, false).GetAwaiter().GetResult();
            var deactivatedCust = customerService.GetByIdAsync(addedCust.Id).GetAwaiter().GetResult();
            Assert(deactivated && deactivatedCust != null && !deactivatedCust.IsActive, "تعطيل العميل بدلاً من الحذف بنجاح");

            var reactivated = customerService.SetActiveStatusAsync(addedCust.Id, true).GetAwaiter().GetResult();
            var reactivatedCust = customerService.GetByIdAsync(addedCust.Id).GetAwaiter().GetResult();
            Assert(reactivated && reactivatedCust != null && reactivatedCust.IsActive, "إعادة تفعيل العميل بنجاح");

            // 7. ربط فاتورة بيع بالعميل والتحقق من حساب الرصيد
            // فاتورة: إجمالي 1200 ج.م، مدفوع منها 200 ج.م -> المتبقي 1000 ج.م
            // الرصيد المتوقع = 500 (افتتاحي) + 1000 (متبقي من الفاتورة) = 1500 ج.م
            int testInvoiceId = 0;
            using (var dbInv = new CarPartsDbContext())
            {
                var invEntity = new SalesInvoiceModel
                {
                    InvoiceNumber = "INV-CUST-" + Guid.NewGuid().ToString("N")[..6].ToUpper(),
                    InvoiceDate = DateTime.Now,
                    CustomerId = addedCust.Id,
                    CustomerName = addedCust.Name,
                    CustomerPhone = addedCust.Phone,
                    Status = "معتمدة",
                    SubTotal = 1200.00m,
                    Discount = 0m,
                    GrandTotal = 1200.00m,
                    PaidAmount = 200.00m,
                    PaymentMethod = "آجل",
                    Notes = "فاتورة بيع لاختبار رصيد العميل"
                };
                dbInv.SalesInvoices.Add(invEntity);
                dbInv.SaveChanges();
                testInvoiceId = invEntity.Id;
            }

            var balanceWithInvoice = customerService.CalculateBalanceAsync(addedCust.Id).GetAwaiter().GetResult();
            Assert(balanceWithInvoice == 1500.00m,
                $"حساب رصيد العميل بعد الفاتورة بدقة: 500 (افتتاحي) + 1000 (متبقي غير مدفوع) = 1500.00 ج.م (الفعلي: {balanceWithInvoice:N2})");

            // 8. تسجيل دفعة نقدية وتخفيض الرصيد
            // دفعة 600 ج.م -> الرصيد المتوقع = 1500 - 600 = 900 ج.م
            var payment1 = new CustomerPaymentModel
            {
                CustomerId = addedCust.Id,
                Amount = 600.00m,
                PaymentDate = DateTime.Now,
                PaymentMethod = "نقدي",
                Notes = "سداد دفعة أولى"
            };
            var savedPay1 = customerService.AddPaymentAsync(payment1).GetAwaiter().GetResult();
            Assert(savedPay1.Id > 0, $"تسجيل دفعة نقدية بنجاح في SQLite (ID = {savedPay1.Id})");

            var balanceAfterPayment1 = customerService.CalculateBalanceAsync(addedCust.Id).GetAwaiter().GetResult();
            Assert(balanceAfterPayment1 == 900.00m,
                $"تخفيض رصيد العميل بالدفعة المسجلة: 1500 - 600 = 900.00 ج.م (الفعلي: {balanceAfterPayment1:N2})");

            // 9. تسجيل دفعة ثانية ثم إلغاؤها (قاعدة عدم الحذف المالي والتصحيح بالإلغاء)
            var payment2 = new CustomerPaymentModel
            {
                CustomerId = addedCust.Id,
                Amount = 400.00m,
                PaymentDate = DateTime.Now,
                PaymentMethod = "تحويل بنكي",
                Notes = "دفعة مسجلة بالخطأ لاختبار الإلغاء"
            };
            var savedPay2 = customerService.AddPaymentAsync(payment2).GetAwaiter().GetResult();
            var balanceAfterPay2 = customerService.CalculateBalanceAsync(addedCust.Id).GetAwaiter().GetResult();
            Assert(balanceAfterPay2 == 500.00m, $"تخفيض الرصيد مؤقتاً بالدفعة الثانية: 900 - 400 = 500.00 ج.م");

            // إلغاء الدفعة الثانية مع سبب
            var cancelPaymentSuccess = customerService.CancelPaymentAsync(savedPay2.Id, "تسجيل بالخطأ من الكاشير").GetAwaiter().GetResult();
            Assert(cancelPaymentSuccess, "إلغاء الدفعة المسجلة بالخطأ دون حذف سجلها المالي");

            // التحقق من أن السجل المالي لا يزال موجوداً وحالته ملغاة
            using (var dbCheck = new CarPartsDbContext())
            {
                var payInDb = dbCheck.CustomerPayments.FirstOrDefault(p => p.Id == savedPay2.Id);
                Assert(payInDb != null && payInDb.IsCancelled && payInDb.CancellationReason == "تسجيل بالخطأ من الكاشير",
                    "بقاء السجل المالي في قاعدة البيانات مع توثيق حالة الإلغاء وتاريخها وسببها");
            }

            // التحقق من عودة الرصيد إلى 900 ج.م
            var balanceAfterCancel = customerService.CalculateBalanceAsync(addedCust.Id).GetAwaiter().GetResult();
            Assert(balanceAfterCancel == 900.00m,
                $"عودة الرصيد بدقة بعد إلغاء الدفعة الخاطئة إلى 900.00 ج.م (الفعلي: {balanceAfterCancel:N2})");

            // 10. التحقق من كشف الحساب والسطور المحسوبة
            var statement = customerService.GetStatementAsync(addedCust.Id).GetAwaiter().GetResult().ToList();
            Assert(statement.Count >= 4, $"توليد كشف الحساب المتكامل بنجاح ({statement.Count} أسطر)");

            var openLine = statement.FirstOrDefault(l => l.EntryType == "رصيد افتتاحي");
            Assert(openLine != null && openLine.DebitAmount == 500.00m, "توثيق سطر الرصيد الافتتاحي في كشف الحساب بمبلغ 500.00 ج.م");

            var invLine = statement.FirstOrDefault(l => l.EntryType == "فاتورة بيع");
            Assert(invLine != null && invLine.InvoiceTotal == 1200.00m && invLine.InvoicePaid == 200.00m && invLine.DebitAmount == 1000.00m,
                "توثيق تفاصيل فاتورة المبيعات (الإجمالي 1200، المدفوع 200، المتبقي 1000) في كشف الحساب");

            var validPayLine = statement.FirstOrDefault(l => l.EntryType == "دفعة استلام");
            Assert(validPayLine != null && validPayLine.CreditAmount == 600.00m, "توثيق الدفعة المستلمة بدائن 600.00 ج.م");

            var cancelledPayLine = statement.FirstOrDefault(l => l.EntryType == "دفعة ملغاة");
            Assert(cancelledPayLine != null && cancelledPayLine.IsCancelledPayment && cancelledPayLine.CreditAmount == 0,
                "إظهار الدفعة الملغاة في كشف الحساب كدفعة ملغاة دون التأثير على الرصيد التراكمي");

            var finalLine = statement.Last();
            Assert(finalLine.RunningBalance == 900.00m, $"الرصيد التراكمي الختامي لكشف الحساب يطابق رصيد العميل تماماً: {finalLine.RunningBalance:N2} ج.م");

            // تنظيف بيانات الاختبار الخاصة بالعميل
            using (var cleanupDb = new CarPartsDbContext())
            {
                var paymentsToDelete = cleanupDb.CustomerPayments.Where(p => p.CustomerId == addedCust.Id).ToList();
                cleanupDb.CustomerPayments.RemoveRange(paymentsToDelete);

                if (testInvoiceId > 0)
                {
                    var invToDelete = cleanupDb.SalesInvoices.Find(testInvoiceId);
                    if (invToDelete != null) cleanupDb.SalesInvoices.Remove(invToDelete);
                }

                var custToDelete = cleanupDb.Customers.Find(addedCust.Id);
                if (custToDelete != null) cleanupDb.Customers.Remove(custToDelete);

                cleanupDb.SaveChanges();
            }

            // --------------------------------------------------
            // اختبارات تفاعلية نموذج العملاء والتحقق اللحظي والأرقام المشرقية وتوحيد الهاتف
            // --------------------------------------------------
            Console.WriteLine("--------------------------------------------------");
            Console.WriteLine("بدء اختبارات تفاعلية نموذج العملاء والتحقق اللحظي وتوحيد الهاتف");
            Console.WriteLine("--------------------------------------------------");

            var customersVm = new CustomersViewModel(customerService);

            // 1. فحص CanSaveCustomer وأمر الحفظ
            Assert(!customersVm.CanSaveCustomer, "زر الحفظ معطل مبدئياً عند فراغ اسم العميل (CanSaveCustomer = false)");
            Assert(!customersVm.SaveCustomerCommand.CanExecute(null), "SaveCustomerCommand.CanExecute يرجع false عند فراغ الاسم");

            // كتابة الاسم يفعل الزر فوراً
            customersVm.FormName = "شركة النور لقطع الغيار";
            Assert(customersVm.CanSaveCustomer, "زر الحفظ يتفعل فورياً بمجرد كتابة الاسم");
            Assert(customersVm.SaveCustomerCommand.CanExecute(null), "SaveCustomerCommand.CanExecute يرجع true عند وجود الاسم");

            // مسح الاسم يعطل الزر فوراً
            customersVm.FormName = "   ";
            Assert(!customersVm.CanSaveCustomer, "مسح الاسم يعطل زر الحفظ فورياً");

            // 2. اختبار الحقول الاختيارية وعدم تعطيل الحفظ عند فراغها
            customersVm.FormName = "عميل تجريبي للاختبار";
            customersVm.FormPhone = "";
            customersVm.FormAddress = "";
            customersVm.FormNotes = "";
            customersVm.FormOpeningBalanceText = ""; // فارغ تماماً لا يمنع الحفظ

            customersVm.SaveCustomerCommand.Execute(null);
            Thread.Sleep(300); // إتاحة مهلة للعملية غير المتزامنة
            Assert(customersVm.HasStatusMessage && !customersVm.IsStatusError,
                "الحفظ ينجح تماماً عند ترك الرصيد الافتتاحي والهاتف فارغين (حقول اختيارية)");
            Assert(customersVm.StatusMessage.Contains("تم حفظ بيانات العميل"), "ظهور رسالة النجاح العربية بوضوح وثباتها");

            var savedCust = customerService.GetAllCustomersAsync().GetAwaiter().GetResult()
                .FirstOrDefault(c => c.Name == "عميل تجريبي للاختبار");
            Assert(savedCust != null && savedCust.OpeningBalance == 0m, "حفظ العميل في SQLite برصيد افتتاحي 0 عند تركه فارغاً");

            // 3. اختبار الأرقام العربية المشرقية والفاصلة العشرية في الرصيد الافتتاحي
            customersVm.FormName = "عميل أرقام مشرقية";
            customersVm.FormOpeningBalanceText = "١٥٠٠٫٥٠"; // أرقام مشرقية مع فاصلة عربية
            customersVm.SaveCustomerCommand.Execute(null);
            Thread.Sleep(300);

            var easternCust = customerService.GetAllCustomersAsync().GetAwaiter().GetResult()
                .FirstOrDefault(c => c.Name == "عميل أرقام مشرقية");
            Assert(easternCust != null && easternCust.OpeningBalance == 1500.50m,
                "التحويل الناجح للأرقام المشرقية والفاصلة العربية في الرصيد الافتتاحي إلى 1500.50 ج.م");

            // 4. اختبار الرصيد الافتتاحي غير الصالح
            customersVm.FormName = "عميل خطأ رصيد";
            customersVm.FormOpeningBalanceText = "-50";
            customersVm.SaveCustomerCommand.Execute(null);
            Assert(customersVm.IsStatusError && customersVm.StatusMessage.Contains("الرصيد الافتتاحي"),
                "إظهار رسالة خطأ عربية واضحة ومنع الحفظ عند إدخال رصيد سالب دون تعطيل الزر");

            // 5. اختبار توحيد رقم الهاتف ومنع التكرار مع ذكر اسم العميل
            customersVm.FormName = "محمد أحمد";
            customersVm.FormPhone = "01012345678";
            customersVm.FormOpeningBalanceText = "0";
            customersVm.SaveCustomerCommand.Execute(null);
            Thread.Sleep(300);

            // محاولة إضافة عميل ثانٍ بنفس الهاتف بصيغة دولية ومسافات
            customersVm.FormName = "علي محمود";
            customersVm.FormPhone = "+20 10 1234 5678";
            customersVm.SaveCustomerCommand.Execute(null);
            Thread.Sleep(200);

            Assert(customersVm.IsStatusError && customersVm.StatusMessage.Contains("محمد أحمد"),
                "منع تكرار رقم الهاتف الموحد وإظهار رسالة عربية تذكر اسم العميل الحالي 'محمد أحمد'");

            // تنظيف بيانات الاختبار الخاصة بنموذج العملاء
            using (var cleanupDb = new CarPartsDbContext())
            {
                var custsToDelete = cleanupDb.Customers
                    .Where(c => c.Name == "عميل تجريبي للاختبار" || c.Name == "عميل أرقام مشرقية" || c.Name == "محمد أحمد" || c.Name == "علي محمود")
                    .ToList();
                cleanupDb.Customers.RemoveRange(custsToDelete);
                cleanupDb.SaveChanges();
            }

            // ==================================================
            // اختبارات ربط العملاء بنقطة البيع (الكاشير) ومنع التكرار
            // ==================================================
            Console.WriteLine("--------------------------------------------------");
            Console.WriteLine("بدء اختبارات ربط العملاء بنقطة البيع (الكاشير) ومنع التكرار");
            Console.WriteLine("--------------------------------------------------");

            var posSalesService = new SqliteSalesInvoiceService();
            var posProductService = new SqliteProductService();

            // تجهيز صنف للاختبار
            var testPosProduct = new ProductModel
            {
                Name = "صنف اختبار عميل الكاشير",
                PartNumber = "POS-CUST-" + Random.Shared.Next(1000, 9999),
                Quantity = 50,
                PurchasePrice = 100m,
                SellingPrice = 150m
            };
            testPosProduct = posProductService.AddProductAsync(testPosProduct).GetAwaiter().GetResult();

            // 1. بيع نقدي مباشر (Walk-in sale) دون إدخال بيانات عميل
            var walkInInvoice = new SalesInvoiceModel
            {
                InvoiceNumber = "POS-TEST-WALKIN-" + Random.Shared.Next(1000, 9999),
                InvoiceDate = DateTime.Now,
                CustomerName = "عميل نقدي",
                CustomerPhone = "",
                CustomerAddress = "",
                PaymentMethod = "نقدي",
                Items = new List<SalesInvoiceItemModel>
                {
                    new SalesInvoiceItemModel
                    {
                        ProductId = testPosProduct.Id,
                        PartNumber = testPosProduct.PartNumber,
                        ProductName = testPosProduct.Name,
                        Quantity = 1,
                        UnitPrice = 150m
                    }
                }
            };
            walkInInvoice.RecalculateTotals();
            var postedWalkIn = posSalesService.PostInvoiceAsync(walkInInvoice).GetAwaiter().GetResult();
            Assert(postedWalkIn.CustomerId == null && postedWalkIn.CustomerName == "عميل نقدي",
                "البيع المباشر لعميل نقدي يسجل CustomerId = null واسم 'عميل نقدي' بنجاح");

            // 2. إنشاء عميل جديد تلقائياً عند كتابة اسم وهاتف غير مسجلين في الكاشير
            var uniquePhoneRaw = "01055" + Random.Shared.Next(100000, 999999);
            var autoCreateInvoice = new SalesInvoiceModel
            {
                InvoiceNumber = "POS-TEST-AUTO-" + Random.Shared.Next(1000, 9999),
                InvoiceDate = DateTime.Now,
                CustomerName = "عميل كاشير جديد",
                CustomerPhone = uniquePhoneRaw,
                CustomerAddress = "مدينة نصر - القاهرة",
                PaymentMethod = "نقدي",
                Items = new List<SalesInvoiceItemModel>
                {
                    new SalesInvoiceItemModel
                    {
                        ProductId = testPosProduct.Id,
                        PartNumber = testPosProduct.PartNumber,
                        ProductName = testPosProduct.Name,
                        Quantity = 2,
                        UnitPrice = 150m
                    }
                }
            };
            autoCreateInvoice.RecalculateTotals();
            var postedAuto = posSalesService.PostInvoiceAsync(autoCreateInvoice).GetAwaiter().GetResult();
            Assert(postedAuto.CustomerId.HasValue && postedAuto.CustomerId.Value > 0,
                "إنشاء عميل جديد تلقائياً وربطه بالفاتورة بنجاح في نفس المعاملة (Transaction)");

            var createdCustomerInDb = customerService.GetByIdAsync(postedAuto.CustomerId!.Value).GetAwaiter().GetResult();
            Assert(createdCustomerInDb != null && createdCustomerInDb.Phone == PhoneHelper.Normalize(uniquePhoneRaw),
                "تخزين هاتف العميل المنشأ في صورة موحدة قياسية (Normalized) في قاعدة البيانات");

            // 3. إعادة استخدام العميل الموجود برقم الهاتف الموحد وتفادي التكرار
            var formattedPhone = "+20 " + uniquePhoneRaw.Substring(1);
            var reuseInvoice = new SalesInvoiceModel
            {
                InvoiceNumber = "POS-TEST-REUSE-" + Random.Shared.Next(1000, 9999),
                InvoiceDate = DateTime.Now,
                CustomerName = "اسم مختلف لن يُغير الأصلي",
                CustomerPhone = formattedPhone,
                CustomerAddress = "عنوان جديد لن يمس المسجل",
                PaymentMethod = "نقدي",
                Items = new List<SalesInvoiceItemModel>
                {
                    new SalesInvoiceItemModel
                    {
                        ProductId = testPosProduct.Id,
                        PartNumber = testPosProduct.PartNumber,
                        ProductName = testPosProduct.Name,
                        Quantity = 1,
                        UnitPrice = 150m
                    }
                }
            };
            reuseInvoice.RecalculateTotals();
            var postedReuse = posSalesService.PostInvoiceAsync(reuseInvoice).GetAwaiter().GetResult();
            Assert(postedReuse.CustomerId == postedAuto.CustomerId,
                "إعادة استخدام نفس العميل عند تطابق الهاتف الموحد وعدم تكرار إنشاء العميل");

            // التحقق من عدم الكتابة فوق بيانات العميل المسجل
            var verifyOriginalCustomer = customerService.GetByIdAsync(postedAuto.CustomerId!.Value).GetAwaiter().GetResult();
            Assert(verifyOriginalCustomer != null && verifyOriginalCustomer.Name == "عميل كاشير جديد" && verifyOriginalCustomer.Address == "مدينة نصر - القاهرة",
                "حماية بيانات العميل المسجل مسبقاً من الاستبدال وعدم تعديل الحقول الممتلئة");

            // 4. مطابقة الاسم فقط في حال عدم إدخال رقم الهاتف
            var nameOnlyInvoice = new SalesInvoiceModel
            {
                InvoiceNumber = "POS-TEST-NAME-" + Random.Shared.Next(1000, 9999),
                InvoiceDate = DateTime.Now,
                CustomerName = "   عميل   كاشير   جديد   ",
                CustomerPhone = "",
                CustomerAddress = "",
                PaymentMethod = "نقدي",
                Items = new List<SalesInvoiceItemModel>
                {
                    new SalesInvoiceItemModel
                    {
                        ProductId = testPosProduct.Id,
                        PartNumber = testPosProduct.PartNumber,
                        ProductName = testPosProduct.Name,
                        Quantity = 1,
                        UnitPrice = 150m
                    }
                }
            };
            nameOnlyInvoice.RecalculateTotals();
            var postedNameOnly = posSalesService.PostInvoiceAsync(nameOnlyInvoice).GetAwaiter().GetResult();
            Assert(postedNameOnly.CustomerId == postedAuto.CustomerId,
                "مطابقة العميل بالاسم الموحد وإعادة استخدامه بدقة عند عدم إدخال الهاتف");

            // 5. التحقق من البحث والاقتراحات الحية في نقطة البيع
            var customerSuggestionsList = customerService.GetCustomerSuggestionsAsync("عميل كاشير").GetAwaiter().GetResult().ToList();
            Assert(customerSuggestionsList.Any(c => c.Id == postedAuto.CustomerId),
                "استرجاع اقتراحات العملاء الحية المطابقة للاسم في الكاشير بنجاح");

            // تنظيف بيانات اختبار الكاشير
            using (var cleanupDb = new CarPartsDbContext())
            {
                var invoicesToDelete = cleanupDb.SalesInvoices
                    .Where(i => i.InvoiceNumber.StartsWith("POS-TEST-"))
                    .ToList();
                cleanupDb.SalesInvoices.RemoveRange(invoicesToDelete);

                var productToDelete = cleanupDb.Products.Find(testPosProduct.Id);
                if (productToDelete != null) cleanupDb.Products.Remove(productToDelete);

                if (postedAuto.CustomerId.HasValue)
                {
                    var customerToDelete = cleanupDb.Customers.Find(postedAuto.CustomerId.Value);
                    if (customerToDelete != null) cleanupDb.Customers.Remove(customerToDelete);
                }

                cleanupDb.SaveChanges();
            }

            Console.WriteLine("==================================================");
            Console.WriteLine($"اكتملت جميع الاختبارات بنجاح: {passed} من أصل {total} اختبارات اجتازت 100%");
            Console.WriteLine("==================================================");
        }

    }
}
