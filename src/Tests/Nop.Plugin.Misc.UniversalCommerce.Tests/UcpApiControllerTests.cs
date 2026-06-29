using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Common;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Stores;
using Nop.Plugin.Misc.UniversalCommerce.Controllers;
using Nop.Plugin.Misc.UniversalCommerce.Models;
using Nop.Plugin.Misc.UniversalCommerce.Domain;
using Nop.Services.Catalog;
using Nop.Services.Customers;
using Nop.Services.Directory;
using Nop.Services.Logging;
using Nop.Services.Orders;
using Nop.Services.Seo;
using Xunit;

namespace Nop.Plugin.Misc.UniversalCommerce.Tests
{
    public class UcpApiControllerTests
    {
        private readonly Mock<IProductService> _productServiceMock;
        private readonly Mock<ICustomerService> _customerServiceMock;
        private readonly Mock<IShoppingCartService> _shoppingCartServiceMock;
        private readonly Mock<IOrderProcessingService> _orderProcessingServiceMock;
        private readonly Mock<ICountryService> _countryServiceMock;
        private readonly Mock<IStoreContext> _storeContextMock;
        private readonly Mock<IUrlRecordService> _urlRecordServiceMock;
        private readonly Mock<ILogger> _loggerMock;
        private readonly UcpSettings _ucpSettings;
        private readonly UcpApiController _controller;

        public UcpApiControllerTests()
        {
            _productServiceMock = new Mock<IProductService>();
            _customerServiceMock = new Mock<ICustomerService>();
            _shoppingCartServiceMock = new Mock<IShoppingCartService>();
            _orderProcessingServiceMock = new Mock<IOrderProcessingService>();
            _countryServiceMock = new Mock<ICountryService>();
            _storeContextMock = new Mock<IStoreContext>();
            _urlRecordServiceMock = new Mock<IUrlRecordService>();
            _loggerMock = new Mock<ILogger>();
            _ucpSettings = new UcpSettings { Enabled = true, ProtocolVersion = "1.0", AllowAutonomousCheckout = true };

            _controller = new UcpApiController(
                _productServiceMock.Object,
                _customerServiceMock.Object,
                _shoppingCartServiceMock.Object,
                _orderProcessingServiceMock.Object,
                _countryServiceMock.Object,
                _storeContextMock.Object,
                _urlRecordServiceMock.Object,
                _loggerMock.Object,
                _ucpSettings
            );
            
            // Setup a dummy HttpContext so Response.Headers doesn't throw NullReferenceException
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };
        }

        [Fact]
        public async Task CheckInventory_ReturnsFreeShipping_WhenSubTotalExceedsThreshold()
        {
            // Arrange
            var sku = "TEST-1";
            var request = new UcpInventoryRequest { Sku = sku, Quantity = 2 };
            var product = new Product
            {
                Sku = sku,
                Price = 25.00m,
                Published = true,
                ManageInventoryMethodId = 1, // Track inventory
                StockQuantity = 10
            };
            
            _productServiceMock.Setup(x => x.GetProductBySkuAsync(sku)).ReturnsAsync(product);

            // Act
            var result = await _controller.CheckInventory(request) as JsonResult;

            // Assert
            Assert.NotNull(result);
            dynamic data = result!.Value!;
            
            // Validate availability
            Assert.True((bool)data.GetType().GetProperty("available")!.GetValue(data, null)!);
            
            // Validate shipping option
            var shippingOptions = data.GetType().GetProperty("shipping_options")!.GetValue(data, null)!;
            var firstOption = (shippingOptions as Array)!.GetValue(0)!;
            decimal cost = (decimal)firstOption.GetType().GetProperty("cost")!.GetValue(firstOption, null)!;
            string id = (string)firstOption.GetType().GetProperty("id")!.GetValue(firstOption, null)!;
            
            Assert.Equal(0.00m, cost);
            Assert.Equal("free_shipping", id);
        }

        [Fact]
        public async Task CheckInventory_ReturnsStandardShipping_WhenSubTotalBelowThreshold()
        {
            // Arrange
            var sku = "TEST-2";
            var request = new UcpInventoryRequest { Sku = sku, Quantity = 1 };
            var product = new Product
            {
                Sku = sku,
                Price = 15.00m,
                Published = true,
                ManageInventoryMethodId = 1,
                StockQuantity = 5
            };
            
            _productServiceMock.Setup(x => x.GetProductBySkuAsync(sku)).ReturnsAsync(product);

            // Act
            var result = await _controller.CheckInventory(request) as JsonResult;

            // Assert
            Assert.NotNull(result);
            dynamic data = result!.Value!;
            
            var shippingOptions = data.GetType().GetProperty("shipping_options")!.GetValue(data, null)!;
            var firstOption = (shippingOptions as Array)!.GetValue(0)!;
            decimal cost = (decimal)firstOption.GetType().GetProperty("cost")!.GetValue(firstOption, null)!;
            string id = (string)firstOption.GetType().GetProperty("id")!.GetValue(firstOption, null)!;
            
            Assert.Equal(3.85m, cost);
            Assert.Equal("standard_shipping", id);
        }

        [Fact]
        public async Task AgentCheckout_ReturnsBadRequest_WhenAp2TokenIsMissing()
        {
            // Arrange
            var sku = "TEST-3";
            var request = new Nop.Plugin.Misc.UniversalCommerce.Controllers.Ap2CheckoutRequest(sku, 1, "test@test.com", null!,  new Nop.Plugin.Misc.UniversalCommerce.Controllers.Ap2Addr("John", "Doe", "123 Main St", "London", "W1", "GB"));
            var product = new Product { Sku = sku };
            
            _productServiceMock.Setup(x => x.GetProductBySkuAsync(sku)).ReturnsAsync(product);

            // Act
            var result = await _controller.AgentCheckout(request) as BadRequestObjectResult;

            // Assert
            Assert.NotNull(result);
            dynamic data = result!.Value!;
            Assert.Equal("AP2 token required.", (string)data.GetType().GetProperty("error")!.GetValue(data, null)!);
        }

        [Fact]
        public async Task AgentCheckout_PlacesOrderSuccessfully_WithCorrectShipping()
        {
            // Arrange
            var sku = "TEST-4";
            var request = new Nop.Plugin.Misc.UniversalCommerce.Controllers.Ap2CheckoutRequest(sku, 1, "test@test.com", "TOKEN_XYZ", new Nop.Plugin.Misc.UniversalCommerce.Controllers.Ap2Addr("John", "Doe", "123 Main St", "London", "W1", "GB"));
            var product = new Product { Sku = sku, Price = 10.00m };
            var customer = new Customer { Id = 1, Email = "test@test.com" };
            var country = new Country { Id = 1, TwoLetterIsoCode = "GB" };
            
            _productServiceMock.Setup(x => x.GetProductBySkuAsync(sku)).ReturnsAsync(product);
            _customerServiceMock.Setup(x => x.GetCustomerByEmailAsync(request.Email)).ReturnsAsync(customer);
            _countryServiceMock.Setup(x => x.GetCountryByTwoLetterIsoCodeAsync(request.ShippingAddress.CountryTwoLetterIsoCode)).ReturnsAsync(country);
            
            // Mock PlaceOrder to succeed
            var order = new Order { Id = 1234 };
            var placeOrderResult = new PlaceOrderResult();
            placeOrderResult.PlacedOrder = order;
            _orderProcessingServiceMock.Setup(x => x.PlaceOrderAsync(It.IsAny<Nop.Services.Payments.ProcessPaymentRequest>()))
                .ReturnsAsync(placeOrderResult)
                .Callback<Nop.Services.Payments.ProcessPaymentRequest>(req => 
                {
                    // Assert total cost matches price + shipping (£10 + £3.85)
                    Assert.Equal(13.85m, req.OrderTotal);
                    Assert.Equal("TOKEN_XYZ", req.CustomValues["Ap2Token"]);
                });

            // Act
            var result = await _controller.AgentCheckout(request) as OkObjectResult;

            // Assert
            Assert.NotNull(result);
            dynamic data = result!.Value!;
            Assert.True((bool)data.GetType().GetProperty("success")!.GetValue(data, null)!);
            Assert.Equal(1234, (int)data.GetType().GetProperty("order_id")!.GetValue(data, null)!);
            Assert.Equal(13.85m, (decimal)data.GetType().GetProperty("charged")!.GetValue(data, null)!);
            
            // Verify AddToCart was called
            _shoppingCartServiceMock.Verify(x => x.AddToCartAsync(customer, product, ShoppingCartType.ShoppingCart, It.IsAny<int>(), It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), request.Quantity, true), Times.Once);
        }
        [Fact]
        public async Task CheckInventory_ReturnsAvailable_WhenInStock()
        {
            var sku = "INV-1";
            var request = new UcpInventoryRequest { Sku = sku, Quantity = 1 };
            var product = new Product { Sku = sku, Price = 10m, Published = true, ManageInventoryMethodId = 1, StockQuantity = 1 };
            
            _productServiceMock.Setup(x => x.GetProductBySkuAsync(sku)).ReturnsAsync(product);

            var result = await _controller.CheckInventory(request) as JsonResult;
            dynamic data = result!.Value!;
            
            Assert.True((bool)data.GetType().GetProperty("available")!.GetValue(data, null)!);
        }

        [Fact]
        public async Task CheckInventory_ReturnsNotAvailable_WhenOutOfStock()
        {
            var sku = "INV-2";
            var request = new UcpInventoryRequest { Sku = sku, Quantity = 2 };
            var product = new Product { Sku = sku, Price = 10m, Published = true, ManageInventoryMethodId = 1, StockQuantity = 1 };
            
            _productServiceMock.Setup(x => x.GetProductBySkuAsync(sku)).ReturnsAsync(product);

            var result = await _controller.CheckInventory(request) as JsonResult;
            dynamic data = result!.Value!;
            
            Assert.False((bool)data.GetType().GetProperty("available")!.GetValue(data, null)!);
        }

        [Fact]
        public async Task CheckInventory_ReturnsNotAvailable_WhenUnpublished()
        {
            var sku = "INV-3";
            var request = new UcpInventoryRequest { Sku = sku, Quantity = 1 };
            var product = new Product { Sku = sku, Price = 10m, Published = false, ManageInventoryMethodId = 1, StockQuantity = 10 };
            
            _productServiceMock.Setup(x => x.GetProductBySkuAsync(sku)).ReturnsAsync(product);

            var result = await _controller.CheckInventory(request) as JsonResult;
            dynamic data = result!.Value!;
            
            Assert.False((bool)data.GetType().GetProperty("available")!.GetValue(data, null)!);
        }

        [Fact]
        public async Task CheckInventory_ReturnsAvailable_WhenTrackingDisabled()
        {
            var sku = "INV-4";
            var request = new UcpInventoryRequest { Sku = sku, Quantity = 100 };
            var product = new Product { Sku = sku, Price = 10m, Published = true, ManageInventoryMethodId = 0, StockQuantity = 0 };
            
            _productServiceMock.Setup(x => x.GetProductBySkuAsync(sku)).ReturnsAsync(product);

            var result = await _controller.CheckInventory(request) as JsonResult;
            dynamic data = result!.Value!;
            
            Assert.True((bool)data.GetType().GetProperty("available")!.GetValue(data, null)!);
        }
        [Fact]
        public void GetManifest_ReturnsNotFound_WhenDisabled()
        {
            _ucpSettings.Enabled = false;

            var result = _controller.GetManifest() as NotFoundObjectResult;

            Assert.NotNull(result);
            dynamic data = result!.Value!;
            Assert.Equal("Agentic Commerce endpoints are currently disabled.", (string)data.GetType().GetProperty("error")!.GetValue(data, null)!);
        }

        [Fact]
        public void GetManifest_ReturnsManifest_WhenEnabled()
        {
            _ucpSettings.Enabled = true;
            _ucpSettings.ProtocolVersion = "1.0";
            _ucpSettings.AllowAutonomousCheckout = true;

            var result = _controller.GetManifest() as JsonResult;

            Assert.NotNull(result);
            dynamic data = result!.Value!;
            Assert.Equal("1.0", (string)data.GetType().GetProperty("ucp_version")!.GetValue(data, null)!);
            
            var capabilities = data.GetType().GetProperty("capabilities")!.GetValue(data, null)!;
            Assert.True((bool)capabilities.GetType().GetProperty("agent_checkout")!.GetValue(capabilities, null)!);
            
            Assert.Equal("*", _controller.Response.Headers["Access-Control-Allow-Origin"]);
            Assert.Equal("public, max-age=86400", _controller.Response.Headers["Cache-Control"]);
        }

        [Fact]
        public void UcpApiController_HasUcpAgentPolicyRateLimitingAttribute()
        {
            var type = typeof(UcpApiController);
            var attribute = type.GetCustomAttributes(typeof(Microsoft.AspNetCore.RateLimiting.EnableRateLimitingAttribute), true)
                .Cast<Microsoft.AspNetCore.RateLimiting.EnableRateLimitingAttribute>()
                .FirstOrDefault();

            Assert.NotNull(attribute);
            Assert.Equal("UcpAgentPolicy", attribute.PolicyName);
        }
        [Fact]
        public async Task CheckInventory_ReturnsNotFound_WhenSkuDoesNotExist()
        {
            var request = new UcpInventoryRequest { Sku = "INVALID", Quantity = 1 };
            _productServiceMock.Setup(x => x.GetProductBySkuAsync("INVALID")).ReturnsAsync((Product)null!);

            var result = await _controller.CheckInventory(request) as NotFoundObjectResult;

            Assert.NotNull(result);
            dynamic data = result!.Value!;
            Assert.Equal("SKU not found.", (string)data.GetType().GetProperty("error")!.GetValue(data, null)!);
        }

        [Fact]
        public async Task AgentCheckout_ReturnsNotFound_WhenSkuDoesNotExist()
        {
            var request = new Nop.Plugin.Misc.UniversalCommerce.Controllers.Ap2CheckoutRequest("INVALID", 1, "test@test.com", "TOKEN", new Nop.Plugin.Misc.UniversalCommerce.Controllers.Ap2Addr("A", "B", "C", "D", "E", "F"));
            _productServiceMock.Setup(x => x.GetProductBySkuAsync("INVALID")).ReturnsAsync((Product)null!);

            var result = await _controller.AgentCheckout(request) as NotFoundObjectResult;

            Assert.NotNull(result);
            dynamic data = result!.Value!;
            Assert.Equal("SKU invalid.", (string)data.GetType().GetProperty("error")!.GetValue(data, null)!);
        }

        [Fact]
        public async Task AgentCheckout_CreatesGuestCustomer_IfEmailNotFound()
        {
            var sku = "TEST-GUEST";
            var request = new Nop.Plugin.Misc.UniversalCommerce.Controllers.Ap2CheckoutRequest(sku, 1, "guest@test.com", "TOKEN_XYZ", new Nop.Plugin.Misc.UniversalCommerce.Controllers.Ap2Addr("John", "Doe", "123", "City", "ZIP", "GB"));
            var product = new Product { Sku = sku, Price = 10.00m };
            var guestCustomer = new Customer { Id = 99 };
            var country = new Country { Id = 1, TwoLetterIsoCode = "GB" };

            _productServiceMock.Setup(x => x.GetProductBySkuAsync(sku)).ReturnsAsync(product);
            _customerServiceMock.Setup(x => x.GetCustomerByEmailAsync("guest@test.com")).ReturnsAsync((Customer)null!);
            _customerServiceMock.Setup(x => x.InsertGuestCustomerAsync()).ReturnsAsync(guestCustomer);
            _countryServiceMock.Setup(x => x.GetCountryByTwoLetterIsoCodeAsync("GB")).ReturnsAsync(country);

            var placeOrderResult = new PlaceOrderResult { PlacedOrder = new Order { Id = 123 } };
            _orderProcessingServiceMock.Setup(x => x.PlaceOrderAsync(It.IsAny<Nop.Services.Payments.ProcessPaymentRequest>())).ReturnsAsync(placeOrderResult);

            var result = await _controller.AgentCheckout(request) as OkObjectResult;

            Assert.NotNull(result);
            _customerServiceMock.Verify(x => x.InsertGuestCustomerAsync(), Times.Once);
            _customerServiceMock.Verify(x => x.UpdateCustomerAsync(guestCustomer), Times.Once);
        }

        [Fact]
        public async Task AgentCheckout_ReturnsBadRequest_WhenOrderFails()
        {
            var sku = "TEST-FAIL";
            var request = new Nop.Plugin.Misc.UniversalCommerce.Controllers.Ap2CheckoutRequest(sku, 1, "test@test.com", "TOKEN_XYZ", new Nop.Plugin.Misc.UniversalCommerce.Controllers.Ap2Addr("A", "B", "C", "D", "E", "F"));
            var product = new Product { Sku = sku, Price = 10.00m };
            var customer = new Customer { Id = 1, Email = "test@test.com" };

            _productServiceMock.Setup(x => x.GetProductBySkuAsync(sku)).ReturnsAsync(product);
            _customerServiceMock.Setup(x => x.GetCustomerByEmailAsync("test@test.com")).ReturnsAsync(customer);

            var placeOrderResult = new PlaceOrderResult();
            placeOrderResult.AddError("Payment declined");
            _orderProcessingServiceMock.Setup(x => x.PlaceOrderAsync(It.IsAny<Nop.Services.Payments.ProcessPaymentRequest>())).ReturnsAsync(placeOrderResult);

            var result = await _controller.AgentCheckout(request) as BadRequestObjectResult;

            Assert.NotNull(result);
            dynamic data = result!.Value!;
            Assert.Equal("Order failed.", (string)data.GetType().GetProperty("error")!.GetValue(data, null)!);
        }
    }
}


