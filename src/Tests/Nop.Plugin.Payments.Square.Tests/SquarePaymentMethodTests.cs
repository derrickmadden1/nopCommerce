using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Moq;
using Nop.Core;
using Nop.Core.Domain.Common;
using Nop.Core.Domain.Directory;
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
    private Mock<ILocalizationService> _localizationService;
    private Mock<IStoreContext> _storeContext;
    private Mock<ISettingService> _settingService;
    private SquarePaymentMethod _paymentMethod;

    [SetUp]
    public void Setup()
    {
        _localizationService = new Mock<ILocalizationService>();
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
            null, // ILogger
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
}
