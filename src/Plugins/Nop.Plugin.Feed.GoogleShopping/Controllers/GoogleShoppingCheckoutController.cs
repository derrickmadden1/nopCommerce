using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Orders;
using Nop.Services.Catalog;
using Nop.Services.Orders;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Routing;

namespace Nop.Plugin.Feed.GoogleShopping.Controllers;

public class GoogleShoppingCheckoutController : BasePluginController
{
    private readonly IProductService _productService;
    private readonly IShoppingCartService _shoppingCartService;
    private readonly INopUrlHelper _nopUrlHelper;
    private readonly IWorkContext _workContext;
    private readonly IStoreContext _storeContext;

    public GoogleShoppingCheckoutController(
        IProductService productService,
        IShoppingCartService shoppingCartService,
        INopUrlHelper nopUrlHelper,
        IWorkContext workContext,
        IStoreContext storeContext)
    {
        _productService = productService;
        _shoppingCartService = shoppingCartService;
        _nopUrlHelper = nopUrlHelper;
        _workContext = workContext;
        _storeContext = storeContext;
    }

    [HttpGet]
    [Route("google-checkout-link")]
    public async Task<IActionResult> CheckoutLink(int productId)
    {
        var product = await _productService.GetProductByIdAsync(productId);
        if (product == null || product.Deleted || !product.Published)
        {
            return RedirectToRoute("Homepage");
        }

        var redirectUrl = await _nopUrlHelper.RouteGenericUrlAsync(product);

        if (product.ProductType != ProductType.SimpleProduct)
        {
            return Redirect(redirectUrl);
        }

        var customer = await _workContext.GetCurrentCustomerAsync();
        var store = await _storeContext.GetCurrentStoreAsync();

        var warnings = await _shoppingCartService.AddToCartAsync(
            customer: customer,
            product: product,
            shoppingCartType: ShoppingCartType.ShoppingCart,
            storeId: store.Id,
            quantity: product.OrderMinimumQuantity > 0 ? product.OrderMinimumQuantity : 1);

        if (warnings.Any())
        {
            return Redirect(redirectUrl);
        }

        return RedirectToRoute("Checkout");
    }
}
