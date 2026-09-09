// Add this directly to your theme (NOT via GTM) on cart/checkout pages.
// Uses visibilitychange rather than beforeunload, which is more reliably
// supported for "did the user leave" tracking across browsers.
(function () {
  var cartHasItems = document.querySelector('.cart-item-row') || document.querySelector('.checkout-data');
  if (!cartHasItems) return;

  document.addEventListener('visibilitychange', function () {
    if (document.visibilityState === 'hidden' && typeof clarity === 'function') {
      clarity('event', 'checkout_left_page');
    }
  });
})();
