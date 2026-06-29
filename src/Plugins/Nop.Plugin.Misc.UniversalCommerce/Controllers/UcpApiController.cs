using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Services.Catalog;
using Nop.Services.Customers;
using Nop.Services.Directory;
using Nop.Services.Logging;
using Nop.Services.Orders;
using Nop.Services.Seo;
using Nop.Core.Infrastructure;
using Nop.Plugin.Misc.UniversalCommerce.Domain;
using Nop.Plugin.Misc.UniversalCommerce.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nop.Web.Framework.Controllers;
using Nop.Plugin.Misc.UniversalCommerce.Extensions;
using Nop.Services.Payments;
using Microsoft.AspNetCore.RateLimiting;

namespace Nop.Plugin.Misc.UniversalCommerce.Controllers
{
    [EnableRateLimiting("UcpAgentPolicy")] // Enforces limits across all endpoint routes inside this controller
    public class UcpApiController : BasePluginController
    {
        private readonly IProductService _productService;
        private readonly ICustomerService _customerService;
        private readonly IShoppingCartService _shoppingCartService;
        private readonly IOrderProcessingService _orderProcessingService;
        private readonly ICountryService _countryService;
        private readonly IStoreContext _storeContext;
        private readonly IUrlRecordService _urlRecordService;
        private readonly ILogger _logger; // Native nopCommerce Logger service reference

        private readonly UcpSettings _ucpSettings;

        public UcpApiController(
            IProductService productService,
            ICustomerService customerService,
            IShoppingCartService shoppingCartService,
            IOrderProcessingService orderProcessingService,
            ICountryService countryService,
            IStoreContext storeContext,
            IUrlRecordService urlRecordService,
            ILogger logger,
            UcpSettings ucpSettings)
        {
            _productService = productService;
            _customerService = customerService;
            _shoppingCartService = shoppingCartService;
            _orderProcessingService = orderProcessingService;
            _countryService = countryService;
            _storeContext = storeContext;
            _urlRecordService = urlRecordService;
            _logger = logger;
            _ucpSettings = ucpSettings;
        }

        [HttpGet]
        public IActionResult GetManifest()
        {
            // Verify if your business has toggled agentic traffic off
            if (!_ucpSettings.Enabled)
            {
                return NotFound(new { error = "Agentic Commerce endpoints are currently disabled." });
            }

            // Construct the standardized schema payload expected by crawling agents
            var manifest = new
            {
                ucp_version = _ucpSettings.ProtocolVersion,
                capabilities = new
                {
                    product_discovery = true,
                    realtime_inventory = true,
                    agent_checkout = _ucpSettings.AllowAutonomousCheckout
                },
                endpoints = new
                {
                    catalog_discovery = "/api/ucp/catalog",
                    inventory_check = "/api/ucp/inventory",
                    cart_management = "/api/ucp/cart",
                    payment_handshake = "/api/ucp/checkout"
                }
            };

            // Enforce response headers to match specific standard security profiles
            Response.Headers["Access-Control-Allow-Origin"] = "*";
            Response.Headers["Cache-Control"] = "public, max-age=86400"; // Cache for 24 hours

            return Json(manifest);
        }

        [HttpPost]
        [Route("api/ucp/inventory")]
        public async Task<IActionResult> CheckInventory([FromBody] UcpInventoryRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Sku))
                return BadRequest(new { error = "Invalid request or missing SKU." });

            var product = await _productService.GetProductBySkuAsync(request.Sku);
            if (product == null)
                return NotFound(new { error = "SKU not found." });

            bool isAvailable = product.Published &&
                               (!product.ManageInventoryMethodId.Equals(1) || product.StockQuantity >= request.Quantity);

            // Evaluate identical delivery rules on inquiry
            decimal subTotal = product.Price * request.Quantity;
            decimal expectedShipping = subTotal >= 40.00m ? 0.00m : 3.85m;

            return Json(new
            {
                sku = product.Sku,
                available = isAvailable,
                price = product.Price,
                currency = "GBP",
                shipping_options = new[] {
            new {
                id = expectedShipping == 0.00m ? "free_shipping" : "standard_shipping",
                label = expectedShipping == 0.00m ? "Free Delivery" : "Standard Delivery",
                cost = expectedShipping
            }
        }
            });
        }

        [HttpGet]
        [Route("api/ucp/catalog")]
        public async Task<IActionResult> GetCatalog([FromQuery] int pageIndex = 0, [FromQuery] int pageSize = 50)
        {
            // Enforce maximum safe boundaries on bulk size demands
            if (pageSize > 250)
                pageSize = 250;
            if (pageIndex < 0)
                pageIndex = 0;

            var currentStore = await _storeContext.GetCurrentStoreAsync();

            // Fetch an optimized page slice targeting simple published database entities
            var productsPage = await _productService.SearchProductsAsync(
                pageIndex: pageIndex,
                pageSize: pageSize,
                storeId: currentStore.Id,
                visibleIndividuallyOnly: true,
                overridePublished: true // Passing true ensures it enforces the 'Published = true' filter internally
            );

            var items = new List<UcpCatalogItem>();

            foreach (var product in productsPage)
            {
                // Discard entries missing identifiers required by AI agents
                if (string.IsNullOrWhiteSpace(product.Sku))
                    continue;

                // Evaluate inventory state using your simple logic parameters
                bool inStock = !product.ManageInventoryMethodId.Equals(1) || product.StockQuantity > 0;

                // Retrieve the SEO-friendly URL Slug for linking references directly
                var slug = await _urlRecordService.GetSeNameAsync(product);

                items.Add(new UcpCatalogItem
                {
                    Sku = product.Sku,
                    Name = product.Name,
                    Description = product.ShortDescription ?? product.Name,
                    Price = product.Price,
                    InStock = inStock,
                    UrlSlug = slug
                });
            }

            var response = new UcpCatalogResponse
            {
                TotalItems = productsPage.TotalCount,
                PageIndex = productsPage.PageIndex,
                HasNextPage = productsPage.HasNextPage,
                Products = items
            };

            return Json(response);
        }

        [HttpPost]
        [Route("api/ucp/checkout")]
        public async Task<IActionResult> AgentCheckout([FromBody] Ap2CheckoutRequest request)
        {
            // Log the initial checkout intent step
            await _logger.LogAgentActivityAsync("CheckoutIntent", request.Sku, $"Agent initiated a transaction request for user {request.Email}");

            var product = await _productService.GetProductBySkuAsync(request.Sku);
            if (product == null)
            {
                await _logger.LogAgentActivityAsync("CheckoutFailed", request.Sku, $"Transaction rejected: SKU does not exist.");
                return NotFound(new { error = "SKU invalid." });
            }

            if (string.IsNullOrEmpty(request.PaymentToken))
                return BadRequest(new { error = "AP2 token required." });

            // Enforce your precise £3.85 / Free if >= £40 business rule
            decimal subTotal = product.Price * request.Quantity;
            decimal shippingCost = subTotal >= 40.00m ? 0.00m : 3.85m;
            decimal totalCost = subTotal + shippingCost;

            var customer = await _customerService.GetCustomerByEmailAsync(request.Email);
            if (customer == null)
            {
                customer = await _customerService.InsertGuestCustomerAsync();
                customer.Email = request.Email;
                await _customerService.UpdateCustomerAsync(customer);
            }

            var country = await _countryService.GetCountryByTwoLetterIsoCodeAsync(request.ShippingAddress.CountryTwoLetterIsoCode);
            var address = new Nop.Core.Domain.Common.Address
            {
                FirstName = request.ShippingAddress.FirstName,
                LastName = request.ShippingAddress.LastName,
                Address1 = request.ShippingAddress.Address1,
                City = request.ShippingAddress.City,
                ZipPostalCode = request.ShippingAddress.ZipPostalCode,
                CountryId = country?.Id ?? 0,
                CreatedOnUtc = DateTime.UtcNow
            };
            customer.BillingAddressId = address.Id;
            customer.ShippingAddressId = address.Id;

            await _shoppingCartService.AddToCartAsync(customer, product, ShoppingCartType.ShoppingCart, 1, quantity: request.Quantity);

            var processPaymentRequest = new ProcessPaymentRequest
            {
                OrderGuid = Guid.NewGuid(),
                CustomerId = customer.Id,
                PaymentMethodSystemName = "Payments.AgentUniversal",
                OrderTotal = totalCost
            };
            processPaymentRequest.CustomValues["Ap2Token"] = request.PaymentToken;
            processPaymentRequest.CustomValues["TargetSku"] = request.Sku;

            var placeOrderResult = await _orderProcessingService.PlaceOrderAsync(processPaymentRequest);

            if (placeOrderResult.Success)
            {
                var order = placeOrderResult.PlacedOrder;
                order.OrderShippingInclTax = shippingCost;
                order.OrderShippingExclTax = shippingCost;
                order.ShippingMethod = shippingCost == 0.00m ? "Free Shipping" : "Standard Shipping";

                // Log the final checkout success milestone along with customer details
                await _logger.LogAgentActivityAsync(
                    activityType: "CheckoutSuccess",
                    sku: request.Sku,
                    message: $"Order #{order.Id} placed successfully via AP2 handshake.",
                    customer: customer
                );

                return Ok(new { success = true, order_id = order.Id, charged = totalCost });
            }

            // Log any errors that caused the transaction to fail
            string errorsList = string.Join(" | ", placeOrderResult.Errors);
            await _logger.LogAgentActivityAsync("CheckoutFailed", request.Sku, $"Order placement failed with errors: {errorsList}", customer);

            return BadRequest(new { error = "Order failed.", details = placeOrderResult.Errors });
        }
    }

    public record Ap2CheckoutRequest(string Sku, int Quantity, string Email, string PaymentToken, Ap2Addr ShippingAddress);
    public record Ap2Addr(string FirstName, string LastName, string Address1, string City, string ZipPostalCode, string CountryTwoLetterIsoCode);
}
