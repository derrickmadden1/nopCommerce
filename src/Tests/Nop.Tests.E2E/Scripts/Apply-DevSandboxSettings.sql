-- ==============================================================================
-- Apply-DevSandboxSettings.sql
-- Pre-flight safeguards and test product seeding for local E2E checkout testing.
-- Database: rcc_azure_20260801_2 (or your active dev database)
-- ==============================================================================

SET NOCOUNT ON;

PRINT '1. Enforcing Sandbox mode for payment providers...';
UPDATE Setting 
SET Value = 'True' 
WHERE Name IN ('squarepaymentsettings.usesandbox', 'paypalcommercesettings.usesandbox');

-- Ensure Store 1 Square access token matches the valid sandbox token
UPDATE Setting
SET Value = 'EAAAEBSajl6ZFHQFhEq5nSjGnyzK69jrt2_KKUFoFfv2ZIhPUvL7-MT5LzuZkIlX'
WHERE Name = 'squarepaymentsettings.accesstoken' AND StoreId = 1;

-- Ensure PayPal Commerce merchantid is empty so Sandbox uses REST app client credentials
UPDATE Setting
SET Value = ''
WHERE Name = 'paypalcommercesettings.merchantid';

PRINT '2. Disabling background email transmission (QueuedMessagesSendTask)...';
UPDATE ScheduleTask 
SET Enabled = 0 
WHERE Id = 1;

PRINT '3. Disabling EU Cookie Law popup on dev store...';
UPDATE Setting 
SET Value = 'False' 
WHERE Name = 'storeinformationsettings.displayeucookielawwarning';

PRINT '3. Ensuring E2E verification test product exists...';
IF NOT EXISTS (SELECT 1 FROM Product WHERE Sku = 'TEST-E2E-001')
BEGIN
    INSERT INTO Product (
        ProductTypeId, ParentGroupedProductId, VisibleIndividually, Name, ShortDescription, FullDescription,
        AdminComment, ProductTemplateId, VendorId, ShowOnHomepage, MetaKeywords, MetaDescription, MetaTitle,
        AllowCustomerReviews, ApprovedRatingSum, NotApprovedRatingSum, ApprovedTotalReviews, NotApprovedTotalReviews,
        SubjectToAcl, LimitedToStores, Sku, ManufacturerPartNumber, Gtin, IsGiftCard, GiftCardTypeId,
        OverriddenGiftCardAmount, RequireOtherProducts, RequiredProductIds, AutomaticallyAddRequiredProducts,
        IsDownload, DownloadId, UnlimitedDownloads, MaxNumberOfDownloads, DownloadExpirationDays,
        DownloadActivationTypeId, HasSampleDownload, SampleDownloadId, HasUserAgreement, UserAgreementText,
        IsRecurring, RecurringCycleLength, RecurringCyclePeriodId, RecurringTotalCycles, IsRental,
        RentalPriceLength, RentalPricePeriodId, IsShipEnabled, IsFreeShipping, ShipSeparately,
        AdditionalShippingCharge, DeliveryDateId, IsTaxExempt, TaxCategoryId, ManageInventoryMethodId,
        ProductAvailabilityRangeId, UseMultipleWarehouses, WarehouseId, StockQuantity, DisplayStockAvailability,
        DisplayStockQuantity, MinStockQuantity, LowStockActivityId, NotifyAdminForQuantityBelow, BackorderModeId,
        AllowBackInStockSubscriptions, OrderMinimumQuantity, OrderMaximumQuantity, AllowedQuantities,
        AllowAddingOnlyExistingAttributeCombinations, NotReturnable, DisableBuyButton, DisableWishlistButton,
        AvailableForPreOrder, PreOrderAvailabilityStartDateTimeUtc, CallForPrice, Price, OldPrice, ProductCost,
        CustomerEntersPrice, MinimumCustomerEnteredPrice, MaximumCustomerEnteredPrice, BasepriceEnabled,
        BasepriceAmount, BasepriceUnitId, BasepriceBaseAmount, BasepriceBaseUnitId, MarkAsNew,
        MarkAsNewStartDateTimeUtc, MarkAsNewEndDateTimeUtc, Weight, Length, Width, Height,
        AvailableStartDateTimeUtc, AvailableEndDateTimeUtc, DisplayOrder, Published, Deleted,
        CreatedOnUtc, UpdatedOnUtc, DisplayAttributeCombinationImagesOnly, AgeVerification, MinimumAgeToPurchase
    )
    SELECT TOP 1
        ProductTypeId, ParentGroupedProductId, 1, 'System Verification Item', 'E2E Test Product', 'E2E Test Product for Checkout Automation',
        'Created for E2E Checkout Testing', ProductTemplateId, VendorId, 0, 'test', 'test', 'System Verification Item',
        0, 0, 0, 0, 0,
        0, 0, 'TEST-E2E-001', '', '', 0, 0,
        0, 0, '', 0,
        0, 0, 0, 0, 0,
        0, 0, 0, 0, '',
        0, 0, 0, 0, 0,
        0, 0, 1, 0, 0,
        0.00, DeliveryDateId, 1, TaxCategoryId, 1,
        ProductAvailabilityRangeId, 0, WarehouseId, 9999, 1,
        1, 0, 0, 0, 0,
        0, 1, 100, '',
        0, 0, 0, 0,
        0, NULL, 0, 0.01, 0.00, 0.00,
        0, 0, 0, 0,
        0, 0, 0, 0, 0,
        NULL, NULL, 0.1, 10, 10, 5,
        NULL, NULL, 0, 1, 0,
        GETUTCDATE(), GETUTCDATE(), DisplayAttributeCombinationImagesOnly, 0, 0
    FROM Product 
    WHERE Published = 1 AND Deleted = 0 AND IsShipEnabled = 1
    ORDER BY Id ASC;

    DECLARE @NewProductId INT = SCOPE_IDENTITY();

    INSERT INTO UrlRecord (EntityId, EntityName, Slug, IsActive, LanguageId)
    VALUES (@NewProductId, 'Product', 'system-verification-item', 1, 0);

    PRINT 'Created test product with ID: ' + CAST(@NewProductId AS VARCHAR);
END
ELSE
BEGIN
    UPDATE Product
    SET Published = 1, Deleted = 0, StockQuantity = 9999, Price = 0.01, DisableBuyButton = 0
    WHERE Sku = 'TEST-E2E-001';
    
    PRINT 'Verified existing test product (Sku TEST-E2E-001).';
END

PRINT '4. Status Summary:';
SELECT Name, Value FROM Setting WHERE Name IN ('squarepaymentsettings.usesandbox', 'paypalcommercesettings.usesandbox');
SELECT Id, Name, Enabled FROM ScheduleTask WHERE Id = 1;
SELECT p.Id, p.Name, p.Sku, p.Price, p.StockQuantity, u.Slug 
FROM Product p
LEFT JOIN UrlRecord u ON u.EntityId = p.Id AND u.EntityName = 'Product' AND u.IsActive = 1
WHERE p.Sku = 'TEST-E2E-001';
