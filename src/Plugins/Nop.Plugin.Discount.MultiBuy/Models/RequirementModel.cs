using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.DiscountRules.MultiBuy.Models;

public record RequirementModel
{
    public int DiscountId { get; set; }

    public int RequirementId { get; set; }

    [NopResourceDisplayName("Plugins.DiscountRules.MultiBuy.Fields.Products")]
    public string ProductIds { get; set; }
}