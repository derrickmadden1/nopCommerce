using Nop.Web.Framework.Models;

namespace Nop.Plugin.Widgets.ImagePuzzle.Models;

public record RequirementModel : BaseNopModel
{
    public int DiscountId { get; set; }
    public int RequirementId { get; set; }
}
