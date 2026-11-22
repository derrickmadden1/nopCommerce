using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Moq;
using Nop.Core;
using Nop.Core.Domain.Common;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Stores;
using Nop.Plugin.Payments.Square;
using Nop.Plugin.Payments.Square.Domain;
using Nop.Plugin.Payments.Square.Models;
using Nop.Plugin.Payments.Square.Services;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Customers;
using Nop.Services.Directory;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Services.Plugins;
using Nop.Services.ScheduleTasks;
using Nop.Tests;
using NUnit.Framework;

namespace Nop.Plugin.Payments.Square.Tests;

[TestFixture]
public class SquarePaymentMethodTests : BaseNopTest
{
    private Mock<ILocalizationService> _localizationService = null!;
    private Mock<ILogger> _logger = null!;
    private Mock<IStoreContext> _storeContext = null!;
    private Mock<ISettingService> _settingService = null!;
    private SquarePaymentMethod _paymentMethod = null!;

    [SetUp]
    public void Setup()
    {
        _localizationService = new Mock<ILocalizationService>();
        _logger = new Mock<ILogger>();
        _storeContext = new Mock<IStoreContext>();
        _settingService = new Mock<ISettingService>();

        // Mock localization resources
        _localizationService.Setup(x => x.GetResourceAsync("Plugins.Payments.Square.Fields.Token.Key")).ReturnsAsync("ApplicationToken");
        _localizationService.Setup(x => x.GetResourceAsync("Plugins.Payments.Square.Fields.CardNonce.Key")).ReturnsAsync("CardNonce");
        _localizationService.Setup(x => x.GetResourceAsync("Plugins.Payments.Square.Fields.StoredCard.Key")).ReturnsAsync("StoredCard");
        _localizationService.Setup(x => x.GetResourceAsync("Plugins.Payments.Square.Fields.SaveCard.Key")).ReturnsAsync("SaveCard");
        _localizationService.Setup(x => x.GetResourceAsync("Plugins.Payments.Square.Fields.PostalCode.Key")).ReturnsAsync("PostalCode");

        // Pass nulls for unused dependencies in GetPaymentInfoAsync
        _paymentMethod = new SquarePaymentMethod(
            null, // CurrencySettings
            null, // ICountryService
            null, // ICurrencyService
            null, // ICustomerService
            null, // IGenericAttributeService
            _localizationService.Object,
            _logger.Object,
            null, // IOrderTotalCalculationService
            null, // INopHtmlHelper
            _settingService.Object,
            null, // IScheduleTaskService
            null, // IStateProvinceService
            null, // IWebHelper
            null, // SquarePaymentManager
            new SquarePaymentSettings(),
            _storeContext.Object
        );
    }

    [Test]
    public async Task GetPaymentInfoAsync_Should_Add_SaveCard_When_True()
    {
        // Arrange
        var form = new FormCollection(new Dictionary<string, StringValues>
        {
            { nameof(PaymentInfoModel.SaveCard), new StringValues("true") }
        });

        // Act
        var request = await _paymentMethod.GetPaymentInfoAsync(form);

        // Assert
        request.CustomValues.Should().Contain(cv => cv.Name == "SaveCard" && cv.Value == "True");
    }

    [Test]
    public async Task GetPaymentInfoAsync_Should_Not_Add_SaveCard_When_False()
    {
        // Arrange
        var form = new FormCollection(new Dictionary<string, StringValues>
        {
            { nameof(PaymentInfoModel.SaveCard), new StringValues("false") }
        });

        // Act
        var request = await _paymentMethod.GetPaymentInfoAsync(form);

        // Assert
        request.CustomValues.Should().NotContain(cv => cv.Name == "SaveCard");
    }

    [Test]
    public async Task GetPaymentInfoAsync_Should_Add_Token()
    {
        // Arrange
        var tokenValue = "test_token";
        var form = new FormCollection(new Dictionary<string, StringValues>
        {
            { nameof(PaymentInfoModel.Token), new StringValues(tokenValue) }
        });

        // Act
        var request = await _paymentMethod.GetPaymentInfoAsync(form);

        // Assert
        request.CustomValues.Should().Contain(cv => cv.Name == "ApplicationToken" && cv.Value == tokenValue);
    }

    [Test]
    public async Task GetPaymentInfoAsync_Should_Add_CardNonce()
    {
        // Arrange
        var nonceValue = "test_nonce";
        var form = new FormCollection(new Dictionary<string, StringValues>
        {
            { nameof(PaymentInfoModel.CardNonce), new StringValues(nonceValue) }
        });

        // Act
        var request = await _paymentMethod.GetPaymentInfoAsync(form);

        // Assert
        request.CustomValues.Should().Contain(cv => cv.Name == "CardNonce" && cv.Value == nonceValue);
    }

    [Test]
    public async Task GetPaymentInfoAsync_Should_Add_StoredCardId()
    {
        // Arrange
        var cardId = Guid.NewGuid().ToString();
        var form = new FormCollection(new Dictionary<string, StringValues>
        {
            { nameof(PaymentInfoModel.StoredCardId), new StringValues(cardId) }
        });

        // Act
        var request = await _paymentMethod.GetPaymentInfoAsync(form);

        // Assert
        request.CustomValues.Should().Contain(cv => cv.Name == "StoredCard" && cv.Value == cardId);
    }

    [Test]
    public async Task GetPaymentInfoAsync_Should_Ignore_Empty_StoredCardId()
    {
        // Arrange
        var cardId = Guid.Empty.ToString();
        var form = new FormCollection(new Dictionary<string, StringValues>
        {
            { nameof(PaymentInfoModel.StoredCardId), new StringValues(cardId) }
        });

        // Act
        var request = await _paymentMethod.GetPaymentInfoAsync(form);

        // Assert
        request.CustomValues.Should().NotContain(cv => cv.Name == "StoredCard");
    }

    [Test]
    public async Task HidePaymentMethodAsync_Should_Return_True_When_LocationId_Is_Empty()
    {
        var store = new Store { Id = 1 };
        _storeContext.Setup(x => x.GetCurrentStoreAsync()).ReturnsAsync(store);

        var settings = new SquarePaymentSettings
        {
            ApplicationId = "sq0idp-test",
            AccessToken = "EAAA-test",
            LocationId = ""
        };
        _settingService.Setup(x => x.LoadSettingAsync<SquarePaymentSettings>(1)).ReturnsAsync(settings);

        var result = await _paymentMethod.HidePaymentMethodAsync(new List<ShoppingCartItem>());
        result.Should().BeTrue();
    }

    [Test]
    public async Task HidePaymentMethodAsync_Should_Return_True_When_LocationId_Is_Zero()
    {
        var store = new Store { Id = 1 };
        _storeContext.Setup(x => x.GetCurrentStoreAsync()).ReturnsAsync(store);

        var settings = new SquarePaymentSettings
        {
            ApplicationId = "sq0idp-test",
            AccessToken = "EAAA-test",
            LocationId = "0"
        };
        _settingService.Setup(x => x.LoadSettingAsync<SquarePaymentSettings>(1)).ReturnsAsync(settings);

        var result = await _paymentMethod.HidePaymentMethodAsync(new List<ShoppingCartItem>());
        result.Should().BeTrue();
    }

    [Test]
    public async Task HidePaymentMethodAsync_Should_Return_True_When_AccessToken_Is_Empty()
    {
        var store = new Store { Id = 1 };
        _storeContext.Setup(x => x.GetCurrentStoreAsync()).ReturnsAsync(store);

        var settings = new SquarePaymentSettings
        {
            ApplicationId = "sq0idp-test",
            AccessToken = "",
            LocationId = "L123"
        };
        _settingService.Setup(x => x.LoadSettingAsync<SquarePaymentSettings>(1)).ReturnsAsync(settings);

        var result = await _paymentMethod.HidePaymentMethodAsync(new List<ShoppingCartItem>());
        result.Should().BeTrue();
    }

    [Test]
    public async Task HidePaymentMethodAsync_Should_Return_True_When_ApplicationId_Is_Empty()
    {
        var store = new Store { Id = 1 };
        _storeContext.Setup(x => x.GetCurrentStoreAsync()).ReturnsAsync(store);

        var settings = new SquarePaymentSettings
        {
            ApplicationId = "",
            AccessToken = "EAAA-test",
            LocationId = "L123"
        };
        _settingService.Setup(x => x.LoadSettingAsync<SquarePaymentSettings>(1)).ReturnsAsync(settings);

        var result = await _paymentMethod.HidePaymentMethodAsync(new List<ShoppingCartItem>());
        result.Should().BeTrue();
    }

    [Test]
    public async Task ValidatePaymentFormAsync_Should_Sanitize_Raw_Errors_To_User_Friendly_Message()
    {
        var form = new FormCollection(new Dictionary<string, StringValues>
        {
            { nameof(PaymentInfoModel.Errors), new StringValues("HTTP 403 Forbidden|Category: AUTHENTICATION_ERROR") }
        });

        var errors = await _paymentMethod.ValidatePaymentFormAsync(form);
        errors.Should().NotBeEmpty();
        errors.Should().ContainSingle();
        errors.First().Should().Be("There was an issue processing your payment details. Please check your information and try again.");
    }

    [Test]
    public async Task ValidatePaymentFormAsync_Should_Return_Empty_When_No_Errors()
    {
        var form = new FormCollection(new Dictionary<string, StringValues>());
        var errors = await _paymentMethod.ValidatePaymentFormAsync(form);
        errors.Should().BeEmpty();
    }
}
