using Nop.Core.Domain.Localization;
using Nop.Core.Domain.Security;
using Nop.Services.Localization;

namespace Nop.Services.Security;

/// <summary>
/// Extend ILocalizationService by adding method for permission localization
/// </summary>
public static class LocalizationServiceExtensions
{
    #region Methods

    /// <summary>
    /// Get localized value of permission.
    /// We don't have UI to manage permission localizable name. That's why we're using this method
    /// </summary>
    /// <param name="localizationService">Localization manager</param>
    /// <param name="permissionRecord">Permission record</param>
    /// <param name="language">Language</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the localized value
    /// </returns>
    public static async Task<string> GetLocalizedPermissionNameAsync(this ILocalizationService localizationService, PermissionRecord permissionRecord, Language language)
    {
        ArgumentNullException.ThrowIfNull(permissionRecord);

        //localized value
        var resourceName = $"{NopSecurityDefaults.PermissionLocaleStringResourcesPrefix}{permissionRecord.SystemName}";
        var result = await localizationService.GetResourceAsync(resourceName, language.Id, false, string.Empty, true);

        //set default value if required
        if (string.IsNullOrEmpty(result))
            result = permissionRecord.Name;

        return result;
    }

    /// <summary>
    /// Save localized name of a permission
    /// </summary>
    /// <param name="localizationService">Localization manager</param>
    /// <param name="languages">All languages</param>
    /// <param name="permissionRecord">Permission record</param>
    public static void SaveLocalizedPermissionName(this ILocalizationService localizationService, IEnumerable<Language> languages, PermissionRecord permissionRecord)
    {
        ArgumentNullException.ThrowIfNull(permissionRecord);

        var resourceName = $"{NopSecurityDefaults.PermissionLocaleStringResourcesPrefix}{permissionRecord.SystemName}";
        var resourceValue = permissionRecord.Name;

        foreach (var lang in languages)
        {
            var lsr = localizationService.GetLocaleStringResourceByName(resourceName, lang.Id, false);

            if (lsr == null)
            {
                lsr = new LocaleStringResource
                {
                    LanguageId = lang.Id,
                    ResourceName = resourceName,
                    ResourceValue = resourceValue
                };

                localizationService.InsertLocaleStringResource(lsr);
            }
            else
            {
                lsr.ResourceValue = resourceValue;
                localizationService.UpdateLocaleStringResource(lsr);
            }
        }
    }

    /// <summary>
    /// Save localized name of a permission
    /// </summary>
    /// <param name="localizationService">Localization manager</param>
    /// <param name="languages">All languages</param>
    /// <param name="permissionRecord">Permission record</param>
    public static async Task SaveLocalizedPermissionNameAsync(this ILocalizationService localizationService, IEnumerable<Language> languages, PermissionRecord permissionRecord)
    {
        ArgumentNullException.ThrowIfNull(permissionRecord);

        var resourceName = $"{NopSecurityDefaults.PermissionLocaleStringResourcesPrefix}{permissionRecord.SystemName}";
        var resourceValue = permissionRecord.Name;

        foreach (var lang in languages)
        {
            var lsr = await localizationService.GetLocaleStringResourceByNameAsync(resourceName, lang.Id, false);

            if (lsr == null)
            {
                lsr = new LocaleStringResource
                {
                    LanguageId = lang.Id,
                    ResourceName = resourceName,
                    ResourceValue = resourceValue
                };

                await localizationService.InsertLocaleStringResourceAsync(lsr);
            }
            else
            {
                lsr.ResourceValue = resourceValue;
                await localizationService.UpdateLocaleStringResourceAsync(lsr);
            }
        }
    }

    /// <summary>
    /// Delete a localized name of a permission
    /// </summary>
    /// <param name="localizationService">Localization manager</param>
    /// <param name="languages">All languages</param>
    /// <param name="permissionRecord">Permission record</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public static async Task DeleteLocalizedPermissionNameAsync(this ILocalizationService localizationService, IEnumerable<Language> languages, PermissionRecord permissionRecord)
    {
        ArgumentNullException.ThrowIfNull(permissionRecord);

        var resourceName = $"{NopSecurityDefaults.PermissionLocaleStringResourcesPrefix}{permissionRecord.SystemName}";

        foreach (var lang in languages)
        {
            var lsr = await localizationService.GetLocaleStringResourceByNameAsync(resourceName, lang.Id, false);

            if (lsr != null)
                await localizationService.DeleteLocaleStringResourceAsync(lsr);
        }
    }

    #endregion
}