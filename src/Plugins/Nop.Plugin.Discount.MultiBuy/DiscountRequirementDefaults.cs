
namespace Nop.Plugin.DiscountRules.MultiBuy;

/// <summary>
/// Represents constants for the discount requirement rule
/// </summary>
public static class DiscountRequirementDefaults
{
    /// <summary>
    /// The HTML field prefix for discount requirements
    /// </summary>
    public const string HTML_FIELD_PREFIX = "DiscountRulesMultiBuy{0}";

    /// <summary>
    /// The key of the settings to save restricted product identifiers
    /// </summary>
    public const string SETTINGS_KEY = "DiscountRequirement.RestrictedProductIds-{0}";

    /// <summary>
    /// The system name of the discount requirement rule
    /// </summary>
    public const string SYSTEM_NAME = "DiscountRequirement.MultiBuy";
}
