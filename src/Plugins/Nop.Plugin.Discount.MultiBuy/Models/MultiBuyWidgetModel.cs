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
    }
}
