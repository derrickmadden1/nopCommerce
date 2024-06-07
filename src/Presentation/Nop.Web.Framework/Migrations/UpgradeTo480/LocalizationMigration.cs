using FluentMigrator;
using Nop.Core.Infrastructure;
using Nop.Data;
using Nop.Data.Migrations;
using Nop.Services.Localization;
using Nop.Web.Framework.Extensions;

namespace Nop.Web.Framework.Migrations.UpgradeTo480;

[NopUpdateMigration("2023-05-21 12:00:00", "4.80", UpdateMigrationType.Localization)]
public class LocalizationMigration : MigrationBase
{
    /// <summary>Collect the UP migration expressions</summary>
    public override void Up()
    {
        if (!DataSettingsManager.IsDatabaseInstalled())
            return;

        //do not use DI, because it produces exception on the installation process
        var localizationService = EngineContext.Current.Resolve<ILocalizationService>();

        var (languageId, languages) = this.GetLanguageData();

        #region Delete locales

        localizationService.DeleteLocaleResources(new List<string>
        {
            //#7215
            "Admin.Configuration.Settings.ProductEditor.DisplayAttributeCombinationImagesOnly",

            //#374
            "Admin.Documentation.Reference.Acl",
            "Admin.Configuration.Plugins.Fields.AclCustomerRoles",
            "Admin.Configuration.Plugins.Fields.AclCustomerRoles.Hint",
            "Admin.Catalog.Categories.Fields.AclCustomerRoles",
            "Admin.Catalog.Categories.Fields.AclCustomerRoles.Hint",
            "Admin.Catalog.Manufacturers.Fields.AclCustomerRoles",
            "Admin.Catalog.Manufacturers.Fields.AclCustomerRoles.Hint",
            "Admin.Catalog.Products.Fields.AclCustomerRoles",
            "Admin.Catalog.Products.Fields.AclCustomerRoles.Hint",
            "Admin.ContentManagement.Topics.Fields.AclCustomerRoles",
            "Admin.ContentManagement.Topics.Fields.AclCustomerRoles.Hint",
            "Permission.AccessAdminPanel",
            "Permission.AccessWebService",
            "Permission.AllowCustomerImpersonation",
            "Permission.Authentication.EnableMultiFactorAuthentication",
            "Permission.Authentication.ManageExternalMethods",
            "Permission.Authentication.ManageMultifactorMethods",
            "Permission.DisplayPrices",
            "Permission.EnableShoppingCart",
            "Permission.EnableWishlist",
            "Permission.HtmlEditor.ManagePictures",
            "Permission.ManageACL",
            "Permission.ManageActivityLog",
            "Permission.ManageAffiliates",
            "Permission.ManageAppSettings",
            "Permission.ManageAttributes",
            "Permission.ManageBlog",
            "Permission.ManageCampaigns",
            "Permission.ManageCategories",
            "Permission.ManageCountries",
            "Permission.ManageCurrencies",
            "Permission.ManageCurrentCarts",
            "Permission.ManageCustomers",
            "Permission.ManageDiscounts",
            "Permission.ManageEmailAccounts",
            "Permission.ManageExternalAuthenticationMethods",
            "Permission.ManageForums",
            "Permission.ManageGiftCards",
            "Permission.ManageLanguages",
            "Permission.ManageMaintenance",
            "Permission.ManageManufacturers",
            "Permission.ManageMessageQueue",
            "Permission.ManageMessageTemplates",
            "Permission.ManageMultifactorAuthenticationMethods",
            "Permission.ManageNews",
            "Permission.ManageNewsletterSubscribers",
            "Permission.ManageOrders",
            "Permission.ManagePaymentMethods",
            "Permission.ManagePlugins",
            "Permission.ManagePolls",
            "Permission.ManageProductReviews",
            "Permission.ManageProducts",
            "Permission.ManageProductTags",
            "Permission.ManageRecurringPayments",
            "Permission.ManageReturnRequests",
            "Permission.ManageScheduleTasks",
            "Permission.ManageSettings",
            "Permission.ManageShippingSettings",
            "Permission.ManageStores",
            "Permission.ManageSystemLog",
            "Permission.ManageTaxSettings",
            "Permission.ManageTopics",
            "Permission.ManageWidgets",
            "Permission.OrderCountryReport",
            "Permission.PublicStoreAllowNavigation",
            "Permission.SalesSummaryReport",
        });

        #endregion

        #region Rename locales

        #endregion

        #region Add or update locales

        localizationService.AddOrUpdateLocaleResource(new Dictionary<string, string>
        {
            //#7089
            ["Admin.ContentManagement.MessageTemplates.List.SearchEmailAccount"] = "Email account",
            ["Admin.ContentManagement.MessageTemplates.List.SearchEmailAccount.All"] = "All",

            //#7108
            ["Admin.ContentManagement.MessageTemplates.Description.OrderCancelled.VendorNotification"] = "This message template is used to notify a vendor that the certain order was cancelled.The order can be cancelled by a customer on the account page or by store owner in Customers - Customers in Orders tab or in Sales - Orders.",

            //#7215
            ["Admin.Catalog.Products.Fields.DisplayAttributeCombinationImagesOnly.Hint"] = "You may choose pictures associated to each product attribute value or attribute combination (these pictures will replace the main product image when this product attribute value or attribute combination is selected). Enable this option if you want to display only images of a chosen product attribute value or a attribute combination (other pictures will be hidden). Otherwise, all uploaded pictures will be displayed on the product details page",

            //#374
            ["Admin.IAclSupportedModel.Fields.AclCustomerRoles"] = "Limited to customer roles",
            ["Admin.IAclSupportedModel.Fields.AclCustomerRoles.Hint"] = "Choose one or several customer roles i.e. administrators, vendors, guests, who will be able to use or see this item. If you don't need this option just leave this field empty.",
            ["Admin.Configuration.ACL.NoPermissionsDefined"] = "No permissions defined",
            ["Admin.Configuration.ACL.NoCustomerRolesAvailable"] = "No customer roles available",
            ["Admin.Configuration.ACL.DocumentationReference"] = "Learn more about <a target=\"_blank\" href=\"{0}\">access control list</a>",
            ["Admin.Configuration.ACL.Permission.CategoryName"] = "Category of permissions",
            ["Admin.Configuration.ACL.Permission.Edit"] = "Edit permission rules",
        }, languageId);

        #endregion
    }

    /// <summary>Collects the DOWN migration expressions</summary>
    public override void Down()
    {
        //add the downgrade logic if necessary 
    }
}