using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Web.Framework.Models;

namespace Nop.Web.Models.Catalog;

/// <summary>
/// Represents an all products model
/// </summary>
public partial record AllProductsModel : BaseNopModel
{
    #region Properties

    /// <summary>
    /// Gets or sets the catalog products model
    /// </summary>
    public CatalogProductsModel CatalogProductsModel { get; set; }

    /// <summary>
    /// Gets or sets available categories for filtering
    /// </summary>
    public IList<SelectListItem> AvailableCategories { get; set; }

    /// <summary>
    /// Gets or sets selected category ID
    /// </summary>
    public int? SelectedCategoryId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to show in-stock products only
    /// </summary>
    public bool InStockOnly { get; set; }

    #endregion

    #region Ctor

    public AllProductsModel()
    {
        CatalogProductsModel = new CatalogProductsModel();
        AvailableCategories = new List<SelectListItem>();
    }

    #endregion
}
