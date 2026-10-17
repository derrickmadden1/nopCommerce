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

-- Ensure Store 1 Square access token matches Store 0 without hardcoding secrets
UPDATE s1
SET s1.Value = s0.Value
FROM Setting s1
JOIN Setting s0 ON s0.Name = s1.Name AND s0.StoreId = 0
WHERE s1.Name = 'squarepaymentsettings.accesstoken' 
  AND s1.StoreId = 1 
  AND (s1.Value IS NULL OR s1.Value = '');

-- Ensure PayPal Commerce merchantid is empty so Sandbox uses REST app client credentials
UPDATE Setting
SET Value = ''
WHERE Name = 'paypalcommercesettings.merchantid';

-- Ensure CheckMoneyOrder is enabled for manual offline checkout testing
IF NOT EXISTS (SELECT 1 FROM Setting WHERE Name = 'paymentsettings.activepaymentmethodsystemnames' AND Value LIKE '%Payments.CheckMoneyOrder%')
BEGIN
    UPDATE Setting
    SET Value = Value + ',Payments.CheckMoneyOrder'
    WHERE Name = 'paymentsettings.activepaymentmethodsystemnames';
END

PRINT '2. Disabling background email transmission (QueuedMessagesSendTask)...';
UPDATE ScheduleTask 
SET Enabled = 0 
WHERE Id = 1;

PRINT '3. Disabling EU Cookie Law popup on dev store...';
UPDATE Setting 
SET Value = 'False' 
WHERE Name = 'storeinformationsettings.displayeucookielawwarning';

PRINT '4. Disabling Captcha on dev store to enable automated E2E testing...';
UPDATE Setting 
SET Value = 'False' 
WHERE Name = 'captchasettings.enabled';

PRINT '5. Setting user registration type to Standard for immediate login upon registration...';
UPDATE Setting 
SET Value = 'Standard' 
WHERE Name = 'customersettings.userregistrationtype';

PRINT '6. Disabling minimum order placement interval to permit sequential automated test orders...';
UPDATE Setting 
SET Value = '0' 
WHERE Name = 'ordersettings.minimumorderplacementinterval';

PRINT '7. Ensuring deterministic returning test customer exists...';
IF NOT EXISTS (SELECT 1 FROM Customer WHERE Email = 'e2e-returning-customer@rosecottagecroft.co.uk')
BEGIN
    INSERT INTO Customer (
        CustomerGuid, Username, Email, EmailToRevalidate, AdminComment,
        IsTaxExempt, AffiliateId, VendorId, HasShoppingCartItems, RequireReLogin,
        FailedLoginAttempts, CannotLoginUntilDateUtc, Active, Deleted, IsSystemAccount,
        SystemName, LastIpAddress, CreatedOnUtc, LastLoginDateUtc, LastActivityDateUtc,
        RegisteredInStoreId, BillingAddress_Id, ShippingAddress_Id, FirstName, LastName,
        Company, StreetAddress, City, County, ZipPostalCode, CountryId, StateProvinceId,
        VatNumberStatusId, Phone, MustChangePassword, PhoneSmsVerified
    )
    VALUES (
        NEWID(), 'e2e-returning-customer@rosecottagecroft.co.uk', 'e2e-returning-customer@rosecottagecroft.co.uk', NULL, 'E2E Returning Test Customer',
        0, 0, 0, 0, 0,
        0, NULL, 1, 0, 0,
        NULL, '127.0.0.1', GETUTCDATE(), GETUTCDATE(), GETUTCDATE(),
        1, NULL, NULL, 'Returning', 'Buyer',
        'Rose Cottage Test Ltd', '123 Test Lane', 'Testville', 'Testshire', 'TE5 5ST', 82, 0,
        0, '+447700900123', 0, 0
    );

    DECLARE @ReturningCustomerId INT = SCOPE_IDENTITY();

    INSERT INTO CustomerPassword (CustomerId, Password, PasswordFormatId, PasswordSalt, CreatedOnUtc)
    VALUES (@ReturningCustomerId, 'Password123!', 0, NULL, GETUTCDATE());

    INSERT INTO Customer_CustomerRole_Mapping (Customer_Id, CustomerRole_Id)
    SELECT @ReturningCustomerId, Id FROM CustomerRole WHERE SystemName = 'Registered';

    -- Insert pre-saved address
    INSERT INTO Address (
        FirstName, LastName, Email, Company, CountryId, StateProvinceId, City, Address1, ZipPostalCode, PhoneNumber, CreatedOnUtc, County
    )
    VALUES (
        'Returning', 'Buyer', 'e2e-returning-customer@rosecottagecroft.co.uk', 'Rose Cottage Test Ltd', 82, NULL, 'Testville', '123 Test Lane', 'TE5 5ST', '+447700900123', GETUTCDATE(), 'Testshire'
    );

    DECLARE @AddressId INT = SCOPE_IDENTITY();

    INSERT INTO CustomerAddresses (Customer_Id, Address_Id)
    VALUES (@ReturningCustomerId, @AddressId);

    UPDATE Customer 
    SET BillingAddress_Id = @AddressId, ShippingAddress_Id = @AddressId 
    WHERE Id = @ReturningCustomerId;

    PRINT 'Created returning customer with ID: ' + CAST(@ReturningCustomerId AS VARCHAR);
END
ELSE
BEGIN
    DECLARE @ExistingId INT = (SELECT TOP 1 Id FROM Customer WHERE Email = 'e2e-returning-customer@rosecottagecroft.co.uk');
    UPDATE Customer SET Active = 1, Deleted = 0, CannotLoginUntilDateUtc = NULL, FailedLoginAttempts = 0, Phone = '+447700900123' WHERE Id = @ExistingId;
    UPDATE Address SET PhoneNumber = '+447700900123' WHERE Email = 'e2e-returning-customer@rosecottagecroft.co.uk';
    DELETE FROM CustomerPassword WHERE CustomerId = @ExistingId;
    INSERT INTO CustomerPassword (CustomerId, Password, PasswordFormatId, PasswordSalt, CreatedOnUtc)
    VALUES (@ExistingId, 'Password123!', 0, NULL, GETUTCDATE());
    PRINT 'Updated existing returning test customer (ID: ' + CAST(@ExistingId AS VARCHAR) + ')';
END

PRINT '7. Ensuring E2E verification test product exists...';
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

PRINT '8. Status Summary:';
SELECT Name, Value FROM Setting WHERE Name IN ('squarepaymentsettings.usesandbox', 'paypalcommercesettings.usesandbox');
SELECT Id, Name, Enabled FROM ScheduleTask WHERE Id = 1;
SELECT p.Id, p.Name, p.Sku, p.Price, p.StockQuantity, u.Slug 
FROM Product p
LEFT JOIN UrlRecord u ON u.EntityId = p.Id AND u.EntityName = 'Product' AND u.IsActive = 1
WHERE p.Sku = 'TEST-E2E-001';
