using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Shipping;
using Nop.Core.Infrastructure;
using Nop.Plugin.Misc.UniversalCommerce.Domain;
using Nop.Plugin.Misc.UniversalCommerce.Extensions;
using Nop.Plugin.Misc.UniversalCommerce.Models;
using Nop.Services.Catalog;
using Nop.Services.Customers;
using Nop.Services.Directory;
using Nop.Services.Logging;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Services.Seo;
using Nop.Services.Shipping;
using Nop.Web.Framework.Controllers;

namespace Nop.Plugin.Misc.UniversalCommerce.Controllers
{
    [EnableRateLimiting("UcpAgentPolicy")] // Enforces limits across all endpoint routes inside this controller
    [IgnoreAntiforgeryToken] // Required because external API callers will not have a valid CSRF token
    public class UcpApiController : BasePluginController
    {
        private readonly IProductService _productService;
        private readonly ICustomerService _customerService;
        private readonly IShoppingCartService _shoppingCartService;
        private readonly IOrderProcessingService _orderProcessingService;
        private readonly IOrderService _orderService;
        private readonly IShipmentService _shipmentService;
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
            IOrderService orderService,
            IShipmentService shipmentService,
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
            _orderService = orderService;
            _shipmentService = shipmentService;
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

            var manifest = new Dictionary<string, object>
            {
                ["ucp"] = new Dictionary<string, object>
                {
                    ["version"] = "2026-04-08",
                    ["services"] = new Dictionary<string, object>
                    {
                        ["dev.ucp.shopping"] = new[]
                        {
                            new Dictionary<string, object>
                            {
                                ["version"] = "2026-04-08",
                                ["spec"] = "https://ucp.dev/specification/shopping/",
                                ["transport"] = "rest",
                                ["endpoint"] = "https://rosecottagecroft.co.uk/api/ucp/v1",
                                ["schema"] = "https://rosecottagecroft.co.uk/api/ucp/openapi.json"
                            }
                        }
                    },
                    ["capabilities"] = new Dictionary<string, object>
                    {
                        ["dev.ucp.shopping.checkout"] = new[]
                        {
                            new Dictionary<string, object>
                            {
                                ["version"] = "2026-04-08",
                                ["spec"] = "https://ucp.dev/specification/shopping/checkout/",
                                ["schema"] = "https://ucp.dev/schemas/shopping/checkout.json"
                            }
                        },
                        ["dev.ucp.shopping.catalog_search"] = new[]
                        {
                            new Dictionary<string, object>
                            {
                                ["version"] = "2026-04-08",
                                ["spec"] = "https://ucp.dev/specification/shopping/catalog_search/",
                                ["schema"] = "https://ucp.dev/schemas/shopping/catalog_search.json"
                            }
                        },
                        ["dev.ucp.shopping.cart"] = new[]
                        {
                            new Dictionary<string, object>
                            {
                                ["version"] = "2026-04-08",
                                ["spec"] = "https://ucp.dev/specification/shopping/cart/",
                                ["schema"] = "https://ucp.dev/schemas/shopping/cart.json"
                            }
                        },
                        ["dev.ucp.shopping.order_management"] = new[]
                        {
                            new Dictionary<string, object>
                            {
                                ["version"] = "2026-04-08",
                                ["spec"] = "https://ucp.dev/specification/shopping/order_management/",
                                ["status_endpoint"] = "https://rosecottagecroft.co.uk/api/ucp/v1/orders/{orderId}?email={email}",
                                ["cancel_endpoint"] = "https://rosecottagecroft.co.uk/api/ucp/v1/orders/{orderId}/cancel"
                            }
                        }
                    },
                    ["payment_handlers"] = new Dictionary<string, object>
                    {
                        ["com.example.processor"] = new[]
                        {
                            new Dictionary<string, object>
                            {
                                ["id"] = "processor-1",
                                ["version"] = "2026-04-08",
                                ["spec"] = "https://example.com/ucp/handler",
                                ["schema"] = "https://example.com/ucp/handler/schema.json",
                                ["config"] = new Dictionary<string, object>
                                {
                                    ["merchant_id"] = "example-merchant-id"
                                }
                            }
                        }
                    }
                },
                ["signing_keys"] = new[]
                {
                    new Dictionary<string, object>
                    {
                        ["kid"] = "key-1",
                        ["kty"] = "OKP",
                        ["crv"] = "Ed25519",
                        ["x"] = "11qYAYbkContentSignalVerificationPlaceholderKey"
                    }
                }
            };

            return Json(manifest);
        }

        public class UcpInventoryRequest
        {
            public string Sku { get; set; } = string.Empty;
            public int Quantity { get; set; }
        }

        [HttpPost]
        [Route("api/ucp/inventory")]
        public async Task<IActionResult> CheckInventory([FromBody] UcpInventoryRequest request)
        {
            var product = await _productService.GetProductBySkuAsync(request.Sku);
            if (product == null)
            {
                return NotFound(new { available = false, reason = "SKU not found." });
            }

            bool isAvailable = product.StockQuantity >= request.Quantity;

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

        public class UcpCatalogSearchRequest
        {
            public string Query { get; set; } = string.Empty;
        }

        [HttpPost]
        [Route("api/ucp/v1/catalog/search")]
        public async Task<IActionResult> CatalogSearch([FromBody] UcpCatalogSearchRequest request, [FromQuery] int pageIndex = 0, [FromQuery] int pageSize = 50)
        {
            return await GetCatalogInternal(pageIndex, pageSize, request?.Query ?? string.Empty);
        }

        [HttpGet]
        [Route("api/ucp/v1/products")]
        public async Task<IActionResult> ProductsGet([FromQuery] string? search = null, [FromQuery] int pageIndex = 0, [FromQuery] int pageSize = 50)
        {
            return await GetCatalogInternal(pageIndex, pageSize, search ?? string.Empty);
        }

        private async Task<IActionResult> GetCatalogInternal(int pageIndex, int pageSize, string keywords)
        {
            var store = await _storeContext.GetCurrentStoreAsync();
            var productsPage = await _productService.SearchProductsAsync(
                pageIndex: pageIndex,
                pageSize: pageSize,
                keywords: keywords,
                storeId: store.Id,
                visibleIndividuallyOnly: true
            );

            var items = new List<UcpCatalogItem>();
            foreach (var product in productsPage)
            {
                var slug = await _urlRecordService.GetSeNameAsync(product);

                items.Add(new UcpCatalogItem
                {
                    Sku = product.Sku,
                    Name = product.Name,
                    Description = product.ShortDescription,
                    Price = product.Price,
                    Currency = "GBP",
                    InStock = product.StockQuantity > 0,
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

        #region Post-Purchase Order Status & Cancellation Endpoints

        [HttpGet]
        [Route("api/ucp/v1/orders/{orderId:int}")]
        public async Task<IActionResult> GetOrderStatus(int orderId, [FromQuery] string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return BadRequest(new { error = "The 'email' query parameter is required for order verification." });
            }

            var order = await _orderService.GetOrderByIdAsync(orderId);
            if (order == null)
            {
                return NotFound(new { error = $"Order #{orderId} not found." });
            }

            var customer = await _customerService.GetCustomerByIdAsync(order.CustomerId);
            if (customer == null || string.IsNullOrWhiteSpace(customer.Email) ||
                !customer.Email.Equals(email.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return Unauthorized(new { error = "Email verification failed for this order." });
            }

            var shipments = await _shipmentService.GetShipmentsByOrderIdAsync(order.Id);
            var shipmentList = shipments.Select(s => new
            {
                shipment_id = s.Id,
                tracking_number = s.TrackingNumber,
                shipped_date_utc = s.ShippedDateUtc,
                delivery_date_utc = s.DeliveryDateUtc,
                tracking_url = !string.IsNullOrWhiteSpace(s.TrackingNumber) 
                    ? $"https://www.royalmail.com/track-your-item#/tracking-results/{s.TrackingNumber}"
                    : null
            }).ToList();

            var orderItems = await _orderService.GetOrderItemsAsync(order.Id);
            var itemsList = new List<object>();
            foreach (var item in orderItems)
            {
                var product = await _productService.GetProductByIdAsync(item.ProductId);
                itemsList.Add(new
                {
                    sku = product?.Sku ?? "N/A",
                    name = product?.Name ?? "Item",
                    quantity = item.Quantity,
                    unit_price = item.UnitPriceInclTax,
                    total_price = item.PriceInclTax
                });
            }

            return Ok(new
            {
                order_id = order.Id,
                order_guid = order.OrderGuid,
                status = order.OrderStatus.ToString(),
                payment_status = order.PaymentStatus.ToString(),
                shipping_status = order.ShippingStatus.ToString(),
                shipping_method = order.ShippingMethod,
                order_total = order.OrderTotal,
                currency = "GBP",
                created_on_utc = order.CreatedOnUtc,
                items = itemsList,
                shipments = shipmentList
            });
        }

        [HttpPost]
        [Route("api/ucp/v1/orders/{orderId:int}/cancel")]
        public async Task<IActionResult> CancelOrder(int orderId, [FromBody] UcpCancelOrderRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Email))
            {
                return BadRequest(new { error = "The 'email' field is required for order cancellation." });
            }

            var order = await _orderService.GetOrderByIdAsync(orderId);
            if (order == null)
            {
                return NotFound(new { error = $"Order #{orderId} not found." });
            }

            var customer = await _customerService.GetCustomerByIdAsync(order.CustomerId);
            if (customer == null || string.IsNullOrWhiteSpace(customer.Email) ||
                !customer.Email.Equals(request.Email.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return Unauthorized(new { error = "Email verification failed for this order." });
            }

            if (!_orderProcessingService.CanCancelOrder(order))
            {
                return BadRequest(new
                {
                    error = "Order cannot be cancelled in its current state.",
                    order_status = order.OrderStatus.ToString(),
                    shipping_status = order.ShippingStatus.ToString(),
                    detail = order.ShippingStatus == ShippingStatus.Shipped 
                        ? "Order has already shipped and must be handled via the returns process upon delivery." 
                        : "Order status does not permit cancellation."
                });
            }

            await _orderProcessingService.CancelOrderAsync(order, notifyCustomer: true);

            await _logger.LogAgentActivityAsync(
                activityType: "OrderCancelled",
                sku: "N/A",
                message: $"Order #{order.Id} cancelled by agent. Reason: {request.Reason ?? "Not specified"}",
                customer: customer
            );

            return Ok(new
            {
                success = true,
                order_id = order.Id,
                status = OrderStatus.Cancelled.ToString(),
                message = $"Order #{order.Id} was cancelled successfully."
            });
        }

        #endregion
    }

    public record Ap2CheckoutRequest(string Sku, int Quantity, string Email, string PaymentToken, Ap2Addr ShippingAddress);
    public record Ap2Addr(string FirstName, string LastName, string Address1, string City, string ZipPostalCode, string CountryTwoLetterIsoCode);
    public record UcpCancelOrderRequest(string Email, string? Reason);
}
