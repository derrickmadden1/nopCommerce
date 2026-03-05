$(document).ready(function () {
   function parseValue(selector) {
      var element = $(selector + " .value-summary");
      if (element.length === 0) return 0;
      // Remove currency symbols and commas, but keep decimal point and minus sign
      var text = element[0].innerText.replace(/[£,]/g, "");
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
   $(".order-total").after('<tr><td colspan="2">' + spendMoreMessage + '</td></tr>');
});