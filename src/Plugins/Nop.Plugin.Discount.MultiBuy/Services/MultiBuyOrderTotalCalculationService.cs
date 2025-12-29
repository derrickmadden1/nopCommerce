using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Common;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Discounts;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Shipping;
using Nop.Core.Domain.Tax;
using Nop.Services.Attributes;
using Nop.Services.Catalog;
using Nop.Services.Common;
using Nop.Services.Customers;
using Nop.Services.Discounts;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Services.Shipping;
using Nop.Services.Tax;
using Nop.Services.Configuration;
using Nop.Services.Localization;

namespace Nop.Plugin.DiscountRules.MultiBuy.Services
{
    /// <summary>
    /// Extends the core order total calculation to support dynamic multi-buy discounts
    /// for discounts that use the MultiBuy requirement rule.
    /// </summary>
    public class MultiBuyOrderTotalCalculationService : OrderTotalCalculationService
    {
        private readonly IDiscountService _discountService;
        private readonly IShoppingCartService _shoppingCartService;
        private readonly IStoreContext _storeContext;
        private readonly ICustomerService _customerService;
        private readonly ISettingService _settingService;
        private readonly MultiBuyDiscountService _multiBuyDiscountService;

        public MultiBuyOrderTotalCalculationService(
            CatalogSettings catalogSettings,
            IAddressService addressService,
            IAttributeParser<CheckoutAttribute, CheckoutAttributeValue> checkoutAttributeParser,
            ICustomerService customerService,
            IDiscountService discountService,
            IGenericAttributeService genericAttributeService,
            IGiftCardService giftCardService,
            IOrderService orderService,
            IPaymentService paymentService,
            IPriceCalculationService priceCalculationService,
            IProductService productService,
            IRewardPointService rewardPointService,
            IShippingPluginManager shippingPluginManager,
            IShippingService shippingService,
            IShoppingCartService shoppingCartService,
            IStoreContext storeContext,
            ITaxService taxService,
            IWorkContext workContext,
            RewardPointsSettings rewardPointsSettings,
            ShippingSettings shippingSettings,
            ShoppingCartSettings shoppingCartSettings,
            TaxSettings taxSettings,
            ISettingService settingService,
            MultiBuyDiscountService multiBuyDiscountService)
            : base(
                catalogSettings,
                addressService,
                checkoutAttributeParser,
                customerService,
                discountService,
                genericAttributeService,
                giftCardService,
                orderService,
                paymentService,
                priceCalculationService,
                productService,
                rewardPointService,
                shippingPluginManager,
                shippingService,
                shoppingCartService,
                storeContext,
                taxService,
                workContext,
                rewardPointsSettings,
                shippingSettings,
                shoppingCartSettings,
                taxSettings)
        {
            _discountService = discountService;
            _shoppingCartService = shoppingCartService;
            _storeContext = storeContext;
            _customerService = customerService;
            _settingService = settingService;
            _multiBuyDiscountService = multiBuyDiscountService;
        }

        /// <summary>
        /// Computes the order subtotal discount, preferring a dynamic MultiBuy discount
        /// when a discount configured with the MultiBuy requirement exists and is valid.
        /// </summary>
        protected virtual async Task<(decimal discountAmount, List<Nop.Core.Domain.Discounts.Discount> appliedDiscounts)>
            GetOrderSubtotalDiscountWithMultiBuyAsync(Customer customer, decimal orderSubTotalExclTax, IList<ShoppingCartItem> cart)
        {
            var discountAmount = decimal.Zero;
            var appliedDiscounts = new List<Nop.Core.Domain.Discounts.Discount>();

            if (customer == null || orderSubTotalExclTax <= 0)
                return (discountAmount, appliedDiscounts);

            var allDiscounts = await _discountService.GetAllDiscountsAsync(DiscountType.AssignedToOrderSubTotal);
            var allowedDiscounts = new List<Nop.Core.Domain.Discounts.Discount>();

            if (allDiscounts?.Any() == true)
            {
                var couponCodesToValidate = await _customerService.ParseAppliedDiscountCouponCodesAsync(customer);
                foreach (var discount in allDiscounts)
                {
                    if (!_discountService.ContainsDiscount(allowedDiscounts, discount) &&
                        (await _discountService.ValidateDiscountAsync(discount, customer, couponCodesToValidate)).IsValid)
                    {
                        allowedDiscounts.Add(discount);
                    }
                }
            }

            if (!allowedDiscounts.Any())
                return (discountAmount, appliedDiscounts);

            // Prefer a MultiBuy discount if one is present
            Nop.Core.Domain.Discounts.Discount multiBuyDiscount = null;
            DiscountRequirement multiBuyRequirement = null;

            foreach (var discount in allowedDiscounts)
            {
                var requirements = await _discountService.GetAllDiscountRequirementsAsync(discount.Id);
                var mbReq = requirements.FirstOrDefault(r =>
                    r.DiscountRequirementRuleSystemName == DiscountRequirementDefaults.SYSTEM_NAME);

                if (mbReq != null)
                {
                    multiBuyDiscount = discount;
                    multiBuyRequirement = mbReq;
                    break;
                }
            }

            if (multiBuyDiscount != null && multiBuyRequirement != null)
            {
                var settingsKey = string.Format(DiscountRequirementDefaults.SETTINGS_KEY, multiBuyRequirement.Id);
                var mbSettingsJson = await _settingService.GetSettingByKeyAsync<string>(settingsKey);

                MultiBuyRequirementSettings mbSettings = null;
                if (!string.IsNullOrEmpty(mbSettingsJson))
                {
                    try
                    {
                        mbSettings = Newtonsoft.Json.JsonConvert.DeserializeObject<MultiBuyRequirementSettings>(mbSettingsJson);
                    }
                    catch
                    {
                        // legacy/corrupted data
                    }
                }

                if (mbSettings != null)
                {
                    var store = await _storeContext.GetCurrentStoreAsync();
                    var cartForStore = cart ?? await _shoppingCartService.GetShoppingCartAsync(customer,
                        ShoppingCartType.ShoppingCart, storeId: store.Id);

                    var mbDiscountAmount =
                        await _multiBuyDiscountService.CalculateDiscountAsync(cartForStore, mbSettings);

                    if (mbDiscountAmount > decimal.Zero)
                    {
                        return (mbDiscountAmount, new List<Nop.Core.Domain.Discounts.Discount> { multiBuyDiscount });
                    }
                }
            }

            // Fallback to core preferred-discount behavior
            appliedDiscounts = _discountService.GetPreferredDiscount(allowedDiscounts, orderSubTotalExclTax,
                out discountAmount);

            if (discountAmount < decimal.Zero)
                discountAmount = decimal.Zero;

            return (discountAmount, appliedDiscounts);
        }

        /// <summary>
        /// Gets shopping cart subtotal, extended to support dynamic MultiBuy discount amounts.
        /// Method body is based on the core implementation with the discount-calculation
        /// section replaced to call <see cref="GetOrderSubtotalDiscountWithMultiBuyAsync"/>.
        /// </summary>
        public override async Task<(decimal discountAmountInclTax, decimal discountAmountExclTax,
            List<Nop.Core.Domain.Discounts.Discount> appliedDiscounts, decimal subTotalWithoutDiscountInclTax,
            decimal subTotalWithoutDiscountExclTax, decimal subTotalWithDiscountInclTax,
            decimal subTotalWithDiscountExclTax, SortedDictionary<decimal, decimal> taxRates)>
            GetShoppingCartSubTotalsAsync(IList<ShoppingCartItem> cart)
        {
            var discountAmountExclTax = decimal.Zero;
            var discountAmountInclTax = decimal.Zero;
            var appliedDiscounts = new List<Nop.Core.Domain.Discounts.Discount>();
            var subTotalWithoutDiscountExclTax = decimal.Zero;
            var subTotalWithoutDiscountInclTax = decimal.Zero;

            var subTotalWithDiscountExclTax = decimal.Zero;
            var subTotalWithDiscountInclTax = decimal.Zero;

            var taxRates = new SortedDictionary<decimal, decimal>();

            if (!cart.Any())
                return (discountAmountInclTax, discountAmountExclTax, appliedDiscounts,
                    subTotalWithoutDiscountInclTax, subTotalWithoutDiscountExclTax,
                    subTotalWithDiscountInclTax, subTotalWithDiscountExclTax, taxRates);

            //get the customer 
            var customer = await _customerService.GetShoppingCartCustomerAsync(cart);

            //sub totals
            foreach (var shoppingCartItem in cart)
            {
                var sciSubTotal = (await _shoppingCartService.GetSubTotalAsync(shoppingCartItem, true)).subTotal;
                var product = await _productService.GetProductByIdAsync(shoppingCartItem.ProductId);

                var (sciExclTax, taxRate) = await _taxService.GetProductPriceAsync(product, sciSubTotal, false, customer);
                var (sciInclTax, _) = await _taxService.GetProductPriceAsync(product, sciSubTotal, true, customer);

                subTotalWithoutDiscountExclTax += sciExclTax;
                subTotalWithoutDiscountInclTax += sciInclTax;

                //tax rates
                var sciTax = sciInclTax - sciExclTax;
                if (taxRate <= decimal.Zero || sciTax <= decimal.Zero)
                    continue;

                if (!taxRates.ContainsKey(taxRate))
                    taxRates.Add(taxRate, sciTax);
                else
                    taxRates[taxRate] += sciTax;
            }

            //checkout attributes
            if (customer != null)
            {
                var store = await _storeContext.GetCurrentStoreAsync();
                var checkoutAttributesXml = await _genericAttributeService.GetAttributeAsync<string>(customer,
                    NopCustomerDefaults.CheckoutAttributes, store.Id);
                var attributeValues = _checkoutAttributeParser.ParseAttributeValues(checkoutAttributesXml);
                if (attributeValues != null)
                {
                    await foreach (var (attribute, values) in attributeValues)
                    {
                        await foreach (var attributeValue in values)
                        {
                            var (caExclTax, taxRate) =
                                await _taxService.GetCheckoutAttributePriceAsync(attribute, attributeValue, false,
                                    customer);
                            var (caInclTax, _) =
                                await _taxService.GetCheckoutAttributePriceAsync(attribute, attributeValue, true,
                                    customer);

                            subTotalWithoutDiscountExclTax += caExclTax;
                            subTotalWithoutDiscountInclTax += caInclTax;

                            //tax rates
                            var caTax = caInclTax - caExclTax;
                            if (taxRate <= decimal.Zero || caTax <= decimal.Zero)
                                continue;

                            if (!taxRates.ContainsKey(taxRate))
                                taxRates.Add(taxRate, caTax);
                            else
                                taxRates[taxRate] += caTax;
                        }
                    }
                }
            }

            if (subTotalWithoutDiscountExclTax < decimal.Zero)
                subTotalWithoutDiscountExclTax = decimal.Zero;

            if (subTotalWithoutDiscountInclTax < decimal.Zero)
                subTotalWithoutDiscountInclTax = decimal.Zero;

            if (_shoppingCartSettings.RoundPricesDuringCalculation)
            {
                subTotalWithoutDiscountInclTax =
                    await _priceCalculationService.RoundPriceAsync(subTotalWithoutDiscountInclTax);
                subTotalWithoutDiscountExclTax =
                    await _priceCalculationService.RoundPriceAsync(subTotalWithoutDiscountExclTax);
            }

            // calculate discount amount ('Assigned to order subtotal' discounts),
            // preferring a dynamic MultiBuy discount if available
            (discountAmountExclTax, appliedDiscounts) =
                await GetOrderSubtotalDiscountWithMultiBuyAsync(customer, subTotalWithoutDiscountExclTax, cart);

            if (subTotalWithoutDiscountExclTax < discountAmountExclTax)
                discountAmountExclTax = subTotalWithoutDiscountExclTax;

            discountAmountInclTax = discountAmountExclTax;

            //subtotal with discount (excl tax)
            subTotalWithDiscountExclTax = subTotalWithoutDiscountExclTax - discountAmountExclTax;
            subTotalWithDiscountInclTax = subTotalWithDiscountExclTax;

            //add tax for shopping items & checkout attributes
            var tempTaxRates = new Dictionary<decimal, decimal>(taxRates);
            foreach (var kvp in tempTaxRates)
            {
                var taxRate = kvp.Key;
                var taxValue = kvp.Value;

                if (taxValue == decimal.Zero)
                    continue;

                //discount the tax amount that applies to subtotal items
                if (subTotalWithoutDiscountExclTax > decimal.Zero)
                {
                    var discountTax = taxRates[taxRate] * (discountAmountExclTax / subTotalWithoutDiscountExclTax);
                    discountAmountInclTax += discountTax;
                    taxValue = taxRates[taxRate] - discountTax;
                    if (_shoppingCartSettings.RoundPricesDuringCalculation)
                        taxValue = await _priceCalculationService.RoundPriceAsync(taxValue);
                }

                taxRates[taxRate] = taxValue;
            }

            if (subTotalWithDiscountInclTax < decimal.Zero)
                subTotalWithDiscountInclTax = decimal.Zero;

            if (subTotalWithDiscountExclTax < decimal.Zero)
                subTotalWithDiscountExclTax = decimal.Zero;

            if (_shoppingCartSettings.RoundPricesDuringCalculation)
            {
                subTotalWithDiscountInclTax =
                    await _priceCalculationService.RoundPriceAsync(subTotalWithDiscountInclTax);
                subTotalWithDiscountExclTax =
                    await _priceCalculationService.RoundPriceAsync(subTotalWithDiscountExclTax);
                discountAmountInclTax = await _priceCalculationService.RoundPriceAsync(discountAmountInclTax);
                discountAmountExclTax = await _priceCalculationService.RoundPriceAsync(discountAmountExclTax);
            }

            return (discountAmountInclTax, discountAmountExclTax, appliedDiscounts,
                subTotalWithoutDiscountInclTax, subTotalWithoutDiscountExclTax,
                subTotalWithDiscountInclTax, subTotalWithDiscountExclTax, taxRates);
        }
    }
}


