using Nop.Web.Framework.Models;

namespace Nop.Web.Areas.Admin.Models.Security;

/// <summary>
/// Represents a configuration model
/// </summary>
public record ConfigurationModel : BaseNopModel
{
    #region Ctor

    public ConfigurationModel()
    {
        PermissionCategorySearchModel = new PermissionCategorySearchModel
        {
            Length = int.MaxValue
        };
    }

    #endregion

    #region Properties

    public bool IsPermissionsAvailable { get; set; }

    public bool AreCustomerRolesAvailable { get; set; }

    public PermissionCategorySearchModel PermissionCategorySearchModel { get; set; }

    #endregion
}