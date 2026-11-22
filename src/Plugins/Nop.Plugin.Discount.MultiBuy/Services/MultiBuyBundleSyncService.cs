using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Core.Domain.Catalog;
using Nop.Plugin.DiscountRules.MultiBuy.Services;
using Nop.Services.Catalog;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Discounts;
using Nop.Services.Seo;

namespace Nop.Plugin.DiscountRules.MultiBuy.Services;

/// <summary>
/// Service responsible for creating, linking, and synchronizing bundled products for MultiBuy discount requirements.
/// </summary>
public class MultiBuyBundleSyncService
{
    #region Fields

    private readonly ICategoryService _categoryService;
    private readonly IDiscountService _discountService;
    private readonly IGenericAttributeService _genericAttributeService;
    private readonly IProductAttributeService _productAttributeService;
    private readonly IProductService _productService;
    private readonly ISettingService _settingService;
    private readonly IUrlRecordService _urlRecordService;

    #endregion

    #region Ctor

    public MultiBuyBundleSyncService(
        ICategoryService categoryService,
        IDiscountService discountService,
        IGenericAttributeService genericAttributeService,
        IProductAttributeService productAttributeService,
        IProductService productService,
        ISettingService settingService,
        IUrlRecordService urlRecordService)
    {
        _categoryService = categoryService;
        _discountService = discountService;
        _genericAttributeService = genericAttributeService;
        _productAttributeService = productAttributeService;
        _productService = productService;
        _settingService = settingService;
        _urlRecordService = urlRecordService;
    }

    #endregion

    #region Utilities

    private static string FormatPrompt(string template, int slotIndex)
    {
        if (string.IsNullOrWhiteSpace(template))
            return $"Choose Item {slotIndex}";

        return template.Contains("{0}")
            ? string.Format(template, slotIndex)
            : $"{template.Trim()} {slotIndex}";
    }

    #endregion

    #region Methods

    /// <summary>
    /// Gets the "Offers" category, or creates it if it does not exist.
    /// </summary>
    public async Task<Category> GetOrCreateOffersCategoryAsync()
    {
        var allCategories = await _categoryService.GetAllCategoriesAsync(showHidden: true);
        var offersCategory = allCategories.FirstOrDefault(c =>
            c.Name.Equals("Offers", StringComparison.OrdinalIgnoreCase) && !c.Deleted);

        if (offersCategory != null)
            return offersCategory;

        offersCategory = new Category
        {
            Name = "Offers",
            Published = true,
            DisplayOrder = 1,
            CreatedOnUtc = DateTime.UtcNow,
            UpdatedOnUtc = DateTime.UtcNow
        };

        await _categoryService.InsertCategoryAsync(offersCategory);
        var seName = await _urlRecordService.ValidateSeNameAsync(offersCategory, string.Empty, offersCategory.Name, true);
        await _urlRecordService.SaveSlugAsync(offersCategory, seName, 0);

        return offersCategory;
    }

    /// <summary>
    /// Returns available categories as SelectListItems, defaulting to "Offers" if no selection is made.
    /// </summary>
    public async Task<IList<SelectListItem>> GetAvailableCategoriesAsync(int selectedCategoryId)
    {
        var allCategories = await _categoryService.GetAllCategoriesAsync(showHidden: true);
        var offersCategory = allCategories.FirstOrDefault(c =>
            c.Name.Equals("Offers", StringComparison.OrdinalIgnoreCase) && !c.Deleted);

        if (selectedCategoryId <= 0 && offersCategory != null)
            selectedCategoryId = offersCategory.Id;

        var items = new List<SelectListItem>();
        foreach (var category in allCategories.Where(c => !c.Deleted))
        {
            items.Add(new SelectListItem
            {
                Value = category.Id.ToString(),
                Text = category.Name,
                Selected = category.Id == selectedCategoryId
            });
        }

        // If no categories exist or "Offers" wasn't in the list, offer to auto-create "Offers"
        if (!items.Any(i => i.Text.Equals("Offers", StringComparison.OrdinalIgnoreCase)))
        {
            items.Insert(0, new SelectListItem
            {
                Value = "0",
                Text = "(Auto-create 'Offers' category)",
                Selected = selectedCategoryId <= 0
            });
        }

        return items;
    }

    /// <summary>
    /// Creates a new bundled product in the catalog, assigns it to the target category,
    /// sets its price to BundlePrice, builds its associated attributes, and links it to the requirement.
    /// </summary>
    public async Task<Product> CreateBundleProductAsync(
        int discountId,
        int requirementId,
        MultiBuyRequirementSettings settings,
        string promptTemplate,
        int targetCategoryId)
    {
        var discount = await _discountService.GetDiscountByIdAsync(discountId);
        var productName = !string.IsNullOrWhiteSpace(discount?.Name)
            ? discount.Name
            : "MultiBuy Bundle";

        if (targetCategoryId <= 0)
        {
            var offersCategory = await GetOrCreateOffersCategoryAsync();
            targetCategoryId = offersCategory.Id;
        }

        var product = new Product
        {
            Name = productName,
            ShortDescription = $"Special MultiBuy Bundle Offer: {settings.BundleSize} items for {settings.BundlePrice:C}",
            FullDescription = "<p>Choose your items below to complete your customizable bundle.</p>",
            Price = settings.BundlePrice,
            ProductType = ProductType.SimpleProduct,
            VisibleIndividually = true,
            Published = false, // Draft for admin review before launch
            AllowCustomerReviews = true,
            IsShipEnabled = true,
            ManageInventoryMethod = ManageInventoryMethod.DontManageStock,
            OrderMinimumQuantity = 1,
            OrderMaximumQuantity = 10000,
            CreatedOnUtc = DateTime.UtcNow,
            UpdatedOnUtc = DateTime.UtcNow
        };

        await _productService.InsertProductAsync(product);
        var seName = await _urlRecordService.ValidateSeNameAsync(product, string.Empty, product.Name, true);
        await _urlRecordService.SaveSlugAsync(product, seName, 0);

        if (targetCategoryId > 0)
        {
            await _categoryService.InsertProductCategoryAsync(new ProductCategory
            {
                ProductId = product.Id,
                CategoryId = targetCategoryId,
                IsFeaturedProduct = false,
                DisplayOrder = 1
            });
        }

        // Enable fixed price bundle so associated product prices are not added on top of the bundle price
        await _genericAttributeService.SaveAttributeAsync(product, NopCatalogDefaults.FixedPriceBundleAttribute, true);

        // Enable dynamic dual preview for 2-item bundles
        if (settings.BundleSize == 2)
        {
            await _genericAttributeService.SaveAttributeAsync(product, NopCatalogDefaults.DynamicDualPreviewAttribute, true);
        }

        // Generate product attributes and associated values
        await SyncBundleAttributesAsync(product, settings, promptTemplate);

        // Update settings with the newly linked product
        settings.BundledProductId = product.Id;
        settings.TargetCategoryId = targetCategoryId;
        settings.AttributePromptTemplate = promptTemplate;

        var settingsKey = string.Format(DiscountRequirementDefaults.SETTINGS_KEY, requirementId);
        await _settingService.SetSettingAsync(settingsKey, Newtonsoft.Json.JsonConvert.SerializeObject(settings));

        return product;
    }

    /// <summary>
    /// Synchronizes the linked bundle product's price, categories, and attributes.
    /// </summary>
    public async Task SyncBundleProductAsync(
        int bundledProductId,
        MultiBuyRequirementSettings settings,
        string promptTemplate,
        int targetCategoryId)
    {
        if (bundledProductId <= 0)
            return;

        var product = await _productService.GetProductByIdAsync(bundledProductId);
        if (product == null || product.Deleted)
            return;

        // 1. Sync Price and Quantity settings
        var productUpdated = false;
        if (product.Price != settings.BundlePrice)
        {
            product.Price = settings.BundlePrice;
            productUpdated = true;
        }
        if (product.OrderMinimumQuantity < 1)
        {
            product.OrderMinimumQuantity = 1;
            productUpdated = true;
        }
        if (product.OrderMaximumQuantity < 1)
        {
            product.OrderMaximumQuantity = 10000;
            productUpdated = true;
        }
        if (productUpdated)
        {
            product.UpdatedOnUtc = DateTime.UtcNow;
            await _productService.UpdateProductAsync(product);
        }

        // 2. Ensure FixedPriceBundle is enabled so associated product prices are not added on top of bundle price
        var isFixedPrice = await _genericAttributeService.GetAttributeAsync<bool>(product, NopCatalogDefaults.FixedPriceBundleAttribute);
        if (!isFixedPrice)
        {
            await _genericAttributeService.SaveAttributeAsync(product, NopCatalogDefaults.FixedPriceBundleAttribute, true);
        }

        if (settings.BundleSize == 2)
        {
            var isDualPreview = await _genericAttributeService.GetAttributeAsync<bool>(product, NopCatalogDefaults.DynamicDualPreviewAttribute);
            if (!isDualPreview)
            {
                await _genericAttributeService.SaveAttributeAsync(product, NopCatalogDefaults.DynamicDualPreviewAttribute, true);
            }
        }

        // 3. Sync Category if specified
        if (targetCategoryId > 0)
        {
            var productCategories = await _categoryService.GetProductCategoriesByProductIdAsync(product.Id);
            if (!productCategories.Any(pc => pc.CategoryId == targetCategoryId))
            {
                await _categoryService.InsertProductCategoryAsync(new ProductCategory
                {
                    ProductId = product.Id,
                    CategoryId = targetCategoryId,
                    IsFeaturedProduct = false,
                    DisplayOrder = 1
                });
            }
        }

        // 4. Sync Attributes and Associated Values
        await SyncBundleAttributesAsync(product, settings, promptTemplate);
    }

    /// <summary>
    /// Synchronizes the attribute mappings and associated product values on the bundle product.
    /// </summary>
    public async Task SyncBundleAttributesAsync(
        Product product,
        MultiBuyRequirementSettings settings,
        string promptTemplate)
    {
        if (product == null)
            return;

        var isAllBundle = settings.BundleType == MultiBuyBundleType.All;
        var slotCount = (isAllBundle && settings.EligibleProductIds.Any())
            ? settings.EligibleProductIds.Count
            : settings.BundleSize;

        if (slotCount < 1)
            slotCount = 1;

        // 1. Find or create the generic "Bundle Item" product attribute
        var allProductAttributes = await _productAttributeService.GetAllProductAttributesAsync();
        var productAttribute = allProductAttributes.FirstOrDefault(a =>
            a.Name.Equals("Bundle Item", StringComparison.OrdinalIgnoreCase));

        if (productAttribute == null)
        {
            productAttribute = new ProductAttribute
            {
                Name = "Bundle Item",
                Description = "Bundle Item attribute for customizable MultiBuy offers"
            };
            await _productAttributeService.InsertProductAttributeAsync(productAttribute);
        }

        // 2. Load existing attribute mappings
        var existingMappings = (await _productAttributeService.GetProductAttributeMappingsByProductIdAsync(product.Id))
            .OrderBy(m => m.DisplayOrder)
            .ToList();

        // 3. Adjust mapping count: remove excess slots
        while (existingMappings.Count > slotCount)
        {
            var excess = existingMappings.Last();
            var excessValues = await _productAttributeService.GetProductAttributeValuesAsync(excess.Id);
            foreach (var val in excessValues)
                await _productAttributeService.DeleteProductAttributeValueAsync(val);

            await _productAttributeService.DeleteProductAttributeMappingAsync(excess);
            existingMappings.Remove(excess);
        }

        // 4. Adjust mapping count: add missing slots
        for (var i = existingMappings.Count; i < slotCount; i++)
        {
            var textPrompt = FormatPrompt(promptTemplate, i + 1);
            var newMapping = new ProductAttributeMapping
            {
                ProductId = product.Id,
                ProductAttributeId = productAttribute.Id,
                TextPrompt = textPrompt,
                IsRequired = true,
                AttributeControlTypeId = (int)AttributeControlType.ImageSquares,
                DisplayOrder = i
            };

            await _productAttributeService.InsertProductAttributeMappingAsync(newMapping);
            existingMappings.Add(newMapping);
        }

        // 5. Update prompt and control type on all mappings
        for (var i = 0; i < existingMappings.Count; i++)
        {
            var mapping = existingMappings[i];
            var expectedPrompt = FormatPrompt(promptTemplate, i + 1);
            var changed = false;

            if (mapping.TextPrompt != expectedPrompt)
            {
                mapping.TextPrompt = expectedPrompt;
                changed = true;
            }
            if (mapping.AttributeControlTypeId != (int)AttributeControlType.ImageSquares)
            {
                mapping.AttributeControlTypeId = (int)AttributeControlType.ImageSquares;
                changed = true;
            }
            if (!mapping.IsRequired)
            {
                mapping.IsRequired = true;
                changed = true;
            }
            if (mapping.DisplayOrder != i)
            {
                mapping.DisplayOrder = i;
                changed = true;
            }

            if (changed)
                await _productAttributeService.UpdateProductAttributeMappingAsync(mapping);
        }

        // 6. Preload eligible products & their primary pictures
        var eligibleProducts = settings.EligibleProductIds != null && settings.EligibleProductIds.Any()
            ? (await _productService.GetProductsByIdsAsync(settings.EligibleProductIds.ToArray())).ToList()
            : new List<Product>();

        var pictureMap = new Dictionary<int, int>();
        foreach (var ep in eligibleProducts)
        {
            var pics = await _productService.GetProductPicturesByProductIdAsync(ep.Id);
            pictureMap[ep.Id] = pics.FirstOrDefault()?.PictureId ?? 0;
        }

        // 7. Sync attribute values for each mapping slot
        foreach (var mapping in existingMappings)
        {
            var currentValues = (await _productAttributeService.GetProductAttributeValuesAsync(mapping.Id)).ToList();

            // Delete values no longer eligible
            var toDelete = currentValues
                .Where(v => v.AttributeValueType == AttributeValueType.AssociatedToProduct &&
                            !settings.EligibleProductIds.Contains(v.AssociatedProductId))
                .ToList();

            foreach (var del in toDelete)
            {
                await _productAttributeService.DeleteProductAttributeValueAsync(del);
                currentValues.Remove(del);
            }

            // Upsert eligible products
            var order = 1;
            foreach (var ep in eligibleProducts)
            {
                var pictureId = pictureMap.TryGetValue(ep.Id, out var picId) ? picId : 0;
                var existingVal = currentValues.FirstOrDefault(v =>
                    v.AttributeValueType == AttributeValueType.AssociatedToProduct &&
                    v.AssociatedProductId == ep.Id);

                if (existingVal != null)
                {
                    var updated = false;
                    if (existingVal.Name != ep.Name)
                    {
                        existingVal.Name = ep.Name;
                        updated = true;
                    }
                    if (existingVal.ImageSquaresPictureId != pictureId)
                    {
                        existingVal.ImageSquaresPictureId = pictureId;
                        updated = true;
                    }
                    if (existingVal.PriceAdjustment != 0m)
                    {
                        existingVal.PriceAdjustment = 0m;
                        updated = true;
                    }
                    if (existingVal.DisplayOrder != order)
                    {
                        existingVal.DisplayOrder = order;
                        updated = true;
                    }

                    if (updated)
                        await _productAttributeService.UpdateProductAttributeValueAsync(existingVal);
                }
                else
                {
                    var newVal = new ProductAttributeValue
                    {
                        ProductAttributeMappingId = mapping.Id,
                        AttributeValueTypeId = (int)AttributeValueType.AssociatedToProduct,
                        AssociatedProductId = ep.Id,
                        Name = ep.Name,
                        ImageSquaresPictureId = pictureId,
                        PriceAdjustment = 0m,
                        WeightAdjustment = 0m,
                        Quantity = 1,
                        IsPreSelected = false,
                        DisplayOrder = order
                    };

                    await _productAttributeService.InsertProductAttributeValueAsync(newVal);
                }

                order++;
            }
        }
    }

    /// <summary>
    /// Unlinks the bundle product from the requirement, unpublishing the product in the catalog.
    /// </summary>
    public async Task UnlinkBundleProductAsync(int requirementId, MultiBuyRequirementSettings settings)
    {
        if (settings.BundledProductId > 0)
        {
            var product = await _productService.GetProductByIdAsync(settings.BundledProductId);
            if (product != null)
            {
                product.Published = false;
                product.UpdatedOnUtc = DateTime.UtcNow;
                await _productService.UpdateProductAsync(product);
            }

            settings.BundledProductId = 0;
            var settingsKey = string.Format(DiscountRequirementDefaults.SETTINGS_KEY, requirementId);
            await _settingService.SetSettingAsync(settingsKey, Newtonsoft.Json.JsonConvert.SerializeObject(settings));
        }
    }

    #endregion
}
