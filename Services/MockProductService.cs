using CarPartManager.Models;

namespace CarPartManager.Services
{
    /// <summary>
    /// خدمة بيانات تجريبية (Mock Product Service) تحاكي قاعدة البيانات في الذاكرة.
    /// توفر بيانات واقعية لمحل قطع غيار سيارات في مرحلة التطوير الحالية.
    /// </summary>
    public class MockProductService : IProductService
    {
        private readonly List<ProductModel> _products = new();
        private int _nextId = 1;

        public MockProductService()
        {
            SeedInitialData();
        }

        private void SeedInitialData()
        {
            var initialItems = new List<ProductModel>
            {
                new ProductModel
                {
                    Id = _nextId++,
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
                    Id = _nextId++,
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
                    Id = _nextId++,
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
                    Id = _nextId++,
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
                    Id = _nextId++,
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
                    Id = _nextId++,
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
                    Id = _nextId++,
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
                    Id = _nextId++,
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

            _products.AddRange(initialItems);
        }

        public Task<IEnumerable<ProductModel>> GetAllProductsAsync()
        {
            return Task.FromResult<IEnumerable<ProductModel>>(_products.ToList());
        }

        public Task<ProductModel?> GetByIdAsync(int id)
        {
            var item = _products.FirstOrDefault(p => p.Id == id);
            return Task.FromResult(item);
        }

        public Task<ProductModel> AddProductAsync(ProductModel product)
        {
            product.Id = _nextId++;
            _products.Add(product);
            return Task.FromResult(product);
        }

        public Task<bool> UpdateProductAsync(ProductModel product)
        {
            var existing = _products.FirstOrDefault(p => p.Id == product.Id);
            if (existing == null) return Task.FromResult(false);

            existing.CopyFrom(product);
            return Task.FromResult(true);
        }

        public Task<bool> DeleteProductAsync(int id)
        {
            var existing = _products.FirstOrDefault(p => p.Id == id);
            if (existing == null) return Task.FromResult(false);

            _products.Remove(existing);
            return Task.FromResult(true);
        }

        public Task<IEnumerable<ProductModel>> SearchProductsAsync(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return Task.FromResult<IEnumerable<ProductModel>>(_products.ToList());

            var q = query.Trim().ToLower();
            var filtered = _products.Where(p =>
                (!string.IsNullOrEmpty(p.Name) && p.Name.ToLower().Contains(q)) ||
                (!string.IsNullOrEmpty(p.PartNumber) && p.PartNumber.ToLower().Contains(q)) ||
                (!string.IsNullOrEmpty(p.Barcode) && p.Barcode.ToLower().Contains(q)) ||
                (!string.IsNullOrEmpty(p.CompatibleVehicles) && p.CompatibleVehicles.ToLower().Contains(q)) ||
                (!string.IsNullOrEmpty(p.Category) && p.Category.ToLower().Contains(q))
            ).ToList();

            return Task.FromResult<IEnumerable<ProductModel>>(filtered);
        }

        public Task<ProductModel?> GetByBarcodeAsync(string barcode)
        {
            if (string.IsNullOrWhiteSpace(barcode))
                return Task.FromResult<ProductModel?>(null);

            var code = barcode.Trim();
            var item = _products.FirstOrDefault(p => !string.IsNullOrEmpty(p.Barcode) && p.Barcode.Equals(code, StringComparison.OrdinalIgnoreCase));
            return Task.FromResult(item);
        }

        public Task<List<ProductModel>> GetBarcodeSuggestionsAsync(string prefix, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(prefix))
                return Task.FromResult(new List<ProductModel>());

            var clean = prefix.Trim().Replace("%", "").Replace("_", "");
            if (clean.Length < 2)
                return Task.FromResult(new List<ProductModel>());

            var startsWith = _products
                .Where(p => !string.IsNullOrEmpty(p.Barcode) && p.Barcode.StartsWith(clean, StringComparison.OrdinalIgnoreCase))
                .OrderBy(p => p.Barcode)
                .Take(8)
                .ToList();

            if (startsWith.Count >= 8)
                return Task.FromResult(startsWith);

            int needed = 8 - startsWith.Count;
            var contains = _products
                .Where(p => !string.IsNullOrEmpty(p.Barcode) &&
                            p.Barcode.Contains(clean, StringComparison.OrdinalIgnoreCase) &&
                            !p.Barcode.StartsWith(clean, StringComparison.OrdinalIgnoreCase))
                .OrderBy(p => p.Barcode)
                .Take(needed)
                .ToList();

            startsWith.AddRange(contains);
            return Task.FromResult(startsWith);
        }
    }
}
