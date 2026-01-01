using Nop.Web.Framework.Models;

namespace Nop.Web.Models.Common;

public partial record LogoModel : BaseNopModel
{
    public string StoreName { get; set; }

    public string LogoPath { get; set; }

    // optional intrinsic dimensions populated when available
    public int? Width { get; set; }
    public int? Height { get; set; }

    // optional responsive srcset generated when logo comes from picture service
    public string SrcSet { get; set; }
}