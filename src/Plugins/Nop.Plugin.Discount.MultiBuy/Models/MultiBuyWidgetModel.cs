using Nop.Web.Framework.Models;
using System.Collections.Generic;

namespace Nop.Plugin.DiscountRules.MultiBuy.Models
{
    public record MultiBuyWidgetModel : BaseNopModel
    {
        public string Message { get; set; }
        public bool IsProductPage { get; set; }
        public bool IsCatalogPage { get; set; }
        public bool IsCartPage { get; set; }
        public string TotalSavings { get; set; }
        public Dictionary<int, string> ItemMessages { get; set; } = new Dictionary<int, string>();
        public Dictionary<int, MultiBuyCartItemPromptModel> ItemPrompts { get; set; } = new();
    }

    public class MultiBuyCartItemPromptModel
    {
        public string Message { get; set; }
        public bool IsAllBundle { get; set; }
        public List<MissingBundleProductModel> MissingProducts { get; set; } = new();
    }

    public class MissingBundleProductModel
    {
        public int ProductId { get; set; }
        public string Name { get; set; }
        public string AddToCartUrl { get; set; }
        public string ProductUrl { get; set; }
    }
}
