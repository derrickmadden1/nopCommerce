// Add inside the existing paypal.Buttons({...}) config in
// ~/Plugins/Payments.PayPalCommerce/Views/PaymentInfo.cshtml (find it via:
// grep -rn "paypal.Buttons" src/Plugins/Nop.Plugin.Payments.PayPalCommerce/)
//
// IMPORTANT: do not replace the existing callback bodies — the plugin's own
// onApprove/onError logic handles the actual payment flow. Add the
// dataLayer.push / fetch calls as the FIRST lines inside each existing
// callback, keeping everything else intact.

paypal.Buttons({
    onClick: function (data, actions) {
        var eventName = 'checkout_paypal_express_clicked';
        if (data.fundingSource === 'card') eventName = 'checkout_card_express_clicked';
        if (data.fundingSource === 'googlepay') eventName = 'checkout_gpay_clicked';

        if (typeof dataLayer !== 'undefined') {
            dataLayer.push({ event: eventName });
        }

        fetch('/checkout-tracking/record', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            credentials: 'same-origin', // carries the session cookie so
                                         // GetCurrentCustomerAsync resolves correctly
            body: JSON.stringify({ eventName: eventName }),
            keepalive: true // survives the page navigating away right after click
        }).catch(function () { /* non-critical, don't block checkout on tracking */ });

        // ...existing onClick logic continues here, unchanged
    },
    onApprove: function (data, actions) {
        if (typeof dataLayer !== 'undefined') {
            dataLayer.push({ event: 'checkout_paypal_express_approved' });
        }
        // Deliberately no server-side call here — OrderPlacedEvent /
        // OrderPlacedConsumer already reconciles this attempt as completed
        // once the order is actually placed. A redundant call here risks
        // double-counting.

        // ...existing onApprove logic continues here, unchanged
    },
    onCancel: function (data) {
        if (typeof dataLayer !== 'undefined') {
            dataLayer.push({ event: 'checkout_paypal_express_cancelled' });
        }
        // No server call — the attempt already correctly sits "open" at
        // ExpressCheckoutClicked; that's the honest state until it either
        // completes or times out via FlagAbandonedAsync.
    },
    onError: function (err) {
        if (typeof dataLayer !== 'undefined') {
            dataLayer.push({ event: 'checkout_paypal_express_error' });
        }
    }
    // ...rest of existing config (createOrder, style, etc.)
}).render('#paypal-button-container');
