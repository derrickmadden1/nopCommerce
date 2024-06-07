using Nop.Core.Domain.Catalog;

namespace Nop.Services.Catalog;

/// <summary>
/// Represent event object
/// </summary>
/// <param name="Product">Gets the source product</param>
/// <param name="NewProduct">Gets a new product that has been created</param>
public partial record PostCopyProductEvent(Product Product, Product NewProduct)
{
}
