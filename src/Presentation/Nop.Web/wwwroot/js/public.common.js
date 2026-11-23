/*
** nopCommerce custom js functions
*/



function OpenWindow(query, w, h, scroll) {
    var l = (screen.width - w) / 2;
    var t = (screen.height - h) / 2;

    winprops = 'resizable=0, height=' + h + ',width=' + w + ',top=' + t + ',left=' + l + 'w';
    if (scroll) winprops += ',scrollbars=1';
    var f = window.open(query, "_blank", winprops);
}

function setLocation(url) {
    window.location.href = url;
}

function displayAjaxLoading(display) {
    if (display) {
        $('.ajax-loading-block-window').show();
    }
    else {
        $('.ajax-loading-block-window').hide('slow');
    }
}

function displayPopupNotification(message, messagetype, modal) {
    //types: success, error, warning
    var container;
    if (messagetype == 'success') {
        //success
        container = $('#dialog-notifications-success');
    }
    else if (messagetype == 'error') {
        //error
        container = $('#dialog-notifications-error');
    }
    else if (messagetype == 'warning') {
        //warning
        container = $('#dialog-notifications-warning');
    }
    else {
        //other
        container = $('#dialog-notifications-success');
    }

    //we do not encode displayed message
    var htmlcode = '';
    if ((typeof message) == 'string') {
        htmlcode = '<p>' + message + '</p>';
    } else {
        for (var i = 0; i < message.length; i++) {
            htmlcode = htmlcode + '<p>' + message[i] + '</p>';
        }
    }

    container.html(htmlcode);

    var isModal = (modal ? true : false);
    container.dialog({
        modal: isModal,
        width: 350
    });
}
function displayJoinedPopupNotifications(notes) {
    if (Object.keys(notes).length === 0) return;

    var container = $('#dialog-notifications-success');
    var htmlcode = document.createElement('div');

    for (var note in notes) {
        if (notes.hasOwnProperty(note)) {
            var messages = notes[note];

            for (var i = 0; i < messages.length; ++i) {
                var elem = document.createElement("div");
                elem.innerHTML = messages[i];
                elem.classList.add('popup-notification');
                elem.classList.add(note);

                htmlcode.append(elem);
            }
        }
    }

    container.html(htmlcode);
    container.dialog({
        width: 350,
        modal: true
    });
}
function displayPopupContentFromUrl(url, title, modal, width) {
    var isModal = (modal ? true : false);
    var targetWidth = (width ? width : 550);
    var maxHeight = $(window).height() - 20;

    $('<div></div>').load(url)
        .dialog({
            modal: isModal,
            width: targetWidth,
            maxHeight: maxHeight,
            title: title,
            close: function(event, ui) {
                $(this).dialog('destroy').remove();
            }
        });
}

function displayBarNotification(message, messagetype, timeout) {
    var notificationTimeout;

    var messages = typeof message === 'string' ? [message] : message;
    if (messages.length === 0)
        return;

    //types: success, error, warning
    var cssclass = ['success', 'error', 'warning'].indexOf(messagetype) !== -1 ? messagetype : 'success';

    //remove previous CSS classes and notifications
    $('#bar-notification')
      .removeClass('success')
      .removeClass('error')
      .removeClass('warning');
    $('.bar-notification').remove();

    //add new notifications
    var htmlcode = document.createElement('div');

    //IE11 Does not support miltiple parameters for the add() & remove() methods
    htmlcode.classList.add('bar-notification', cssclass);
    htmlcode.classList.add(cssclass);

    //add close button for notification
    var close = document.createElement('span');
    close.classList.add('close');
    close.setAttribute('title', document.getElementById('bar-notification').dataset.close);

    for (var i = 0; i < messages.length; i++) {
        var content = document.createElement('p');
        content.classList.add('content');
        content.innerHTML = messages[i];

      htmlcode.appendChild(content);
    }
    
    htmlcode.appendChild(close);

    $('#bar-notification')
      .append(htmlcode);

    $(htmlcode)
        .fadeIn('slow')
        .on('mouseenter', function() {
            clearTimeout(notificationTimeout);
        });

    //callback for notification removing
    var removeNoteItem = function () {
        $(htmlcode).remove();
    };

    $(close).on('click', function () {
        $(htmlcode).fadeOut('slow', removeNoteItem);
    });

    //timeout (if set)
    if (timeout > 0) {
        notificationTimeout = setTimeout(function () {
            $(htmlcode).fadeOut('slow', removeNoteItem);
        }, timeout);
    }
}

function htmlEncode(value) {
    return $('<div/>').text(value).html();
}

function htmlDecode(value) {
    return $('<div/>').html(value).text();
}


// CSRF (XSRF) security
function addAntiForgeryToken(data) {
    //if the object is undefined, create a new one.
    if (!data) {
        data = {};
    }
    //add token
    var tokenInput = $('input[name=__RequestVerificationToken]');
    if (tokenInput.length) {
        data.__RequestVerificationToken = tokenInput.val();
    }
    return data;
};

// Checkout & Next buttons: visual loading state & anti-duplicate click UX
$(document).ready(function () {
    var checkoutButtonSelectors = [
        '#checkout',
        '.checkout-button',
        '.checkout-button-modern',
        '.next-step-btn',
        '.new-address-next-step-button',
        '.shipping-method-next-step-button',
        '.payment-method-next-step-button',
        '.payment-info-next-step-button',
        '.confirm-order-btn-modern',
        '.confirm-order-next-step-button'
    ].join(', ');

    function resetButton($btn) {
        if (!$btn || !$btn.length) return;
        $btn.removeClass('is-submitting');
        $btn.css({
            'pointer-events': '',
            'min-width': ''
        });
        $btn.removeAttr('aria-busy');
        var originalHtml = $btn.data('orig-html');
        if (originalHtml) {
            $btn.html(originalHtml);
        }
    }

    $(document).on('click', checkoutButtonSelectors, function (e) {
        var $btn = $(this);

        // Prevent multiple clicks if already submitting or disabled
        if ($btn.hasClass('is-submitting') || $btn.prop('disabled')) {
            e.preventDefault();
            return false;
        }

        // Ignore hidden buttons (e.g. PayPal/Square fallback buttons)
        if ($btn.is(':hidden') || $btn.hasClass('hidden-button')) {
            return;
        }

        // 1. Terms of service check
        var $terms = $('#termsofservice');
        if ($terms.length > 0 && !$terms.is(':checked')) {
            return; // Let the terms validation alert the user; do not lock button
        }

        // 2. Client-side form validation check
        var $form = $btn.closest('form');
        if ($form.length > 0 && typeof $form.valid === 'function') {
            if (!$form.valid()) {
                return; // Form validation failed; do not enter loading state
            }
        }

        // 3. Save original HTML and button width so button dimensions remain stable
        if (!$btn.data('orig-html')) {
            $btn.data('orig-html', $btn.html());
        }
        var btnWidth = $btn.outerWidth();
        if (btnWidth > 0) {
            $btn.css('min-width', btnWidth + 'px');
        }

        // Lock button clicks immediately
        $btn.css('pointer-events', 'none');
        $btn.addClass('is-submitting');
        $btn.attr('aria-busy', 'true');

        // Choose appropriate loading message
        var isProceed = $btn.is('#checkout') || $btn.hasClass('checkout-button') || $btn.hasClass('checkout-button-modern');
        var isConfirm = $btn.hasClass('confirm-order-btn-modern') || $btn.hasClass('confirm-order-next-step-button');
        var loadingText = isProceed ? 'Proceeding...' : (isConfirm ? 'Confirming...' : 'Processing...');

        $btn.html('<span class="btn-spinner" aria-hidden="true"></span> ' + loadingText);

        // Safety timeout in case the request stays on same page (e.g. server validation or latency)
        setTimeout(function () {
            if ($btn.hasClass('is-submitting')) {
                resetButton($btn);
            }
        }, 12000);
    });

    // Reset button state when restoring page from back-forward cache (BFCache)
    window.addEventListener('pageshow', function () {
        $(checkoutButtonSelectors).each(function () {
            var $btn = $(this);
            if ($btn.hasClass('is-submitting')) {
                resetButton($btn);
            }
        });
    });
});
