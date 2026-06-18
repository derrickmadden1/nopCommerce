using System.Collections.Generic;

namespace Nop.Plugin.Misc.MegaMenu.Models
{
    public class MegaMenuItemModel
    {
        public string Name { get; set; }
        public string SeName { get; set; }
        public IList<MegaMenuItemModel> Children { get; set; } = new List<MegaMenuItemModel>();
    }
}