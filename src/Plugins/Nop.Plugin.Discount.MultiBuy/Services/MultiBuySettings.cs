using System.Collections.Generic;
using Nop.Core.Configuration;

namespace Nop.Plugin.DiscountRules.MultiBuy.Services
{
    public class MultiBuySettings : ISettings
    {
        public int BundleSize { get; set; } = 2;
        public decimal BundlePrice { get; set; } = 0m;
        public bool ApplyAcrossMixedProducts { get; set; } = false;
        public List<int> EligibleProductIds { get; set; } = new List<int>();
    }
}