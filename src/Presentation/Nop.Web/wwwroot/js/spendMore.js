(function () {
   if (window.spendMoreInitialized) return;
   window.spendMoreInitialized = true;

   function updateSpendMoreMessage() {
      // Remove any existing message rows first to avoid duplicates
      $(".spend-more-row").remove();

      var totalInfo = $(".total-info");
      if (totalInfo.length === 0) return;

      // Check if "Pick up in store" is selected
      // Use .attr() instead of .data() to handle dynamic updates more reliably
      var isPickupInStore = totalInfo.attr("data-is-pickup-in-store") === "true";
      if (isPickupInStore) {
         return;
      }

      function parseValue(selector) {
         var element = $(selector + " .value-summary");
         if (element.length === 0) return 0;
         // Remove all non-numeric characters except for decimal point and minus sign
         var text = element[0].innerText.replace(/[^\d.-]/g, "");
         return parseFloat(text) || 0;
      }

      var subtotal = parseValue(".order-subtotal");
      var subtotalDiscount = parseValue(".order-subtotal-discount");
      var orderTotalDiscount = parseValue(".discount-total");

      var effectiveSubtotal = subtotal + subtotalDiscount + orderTotalDiscount;
      var spendMore = 40 - effectiveSubtotal;
      var spendMoreMessage = 'Free delivery on all orders over £40.00';

      if (spendMore <= 10) {
         spendMoreMessage = spendMore <= 0 ? 'Free delivery' : 'Spend another £' + spendMore.toFixed(2) + ' for free delivery';
      }

      $(".order-total").after('<tr class="spend-more-row"><td colspan="2">' + spendMoreMessage + '</td></tr>');
   }

   $(document).ready(function () {
      updateSpendMoreMessage();
   });

   // Re-run when the cart is updated via AJAX
   $(document).ajaxComplete(function (event, xhr, settings) {
      // Trigger for any ShoppingCart or Shipping related updates
      if (settings.url.indexOf("ShoppingCart") !== -1 ||
         settings.url.indexOf("EstimateShipping") !== -1 ||
         settings.url.indexOf("checkout") !== -1) {
         // Delay slightly to ensure DOM is fully updated and rendered
         setTimeout(updateSpendMoreMessage, 200);
      }
   });
})();