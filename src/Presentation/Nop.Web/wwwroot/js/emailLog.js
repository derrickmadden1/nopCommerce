(function () {
    const azureUrl = 'https://rcsfunctions.azurewebsites.net/api/PageHit?code=AfDAxdaDaTzYTAcdANjcHu7mNH09dOsFTHKpt0bKi4pjo4mHJF/f6Q==';
    let hitSent = false;

    function sendHit(ip, city) {
        if (hitSent) return;
        hitSent = true;

        const payload = JSON.stringify({
            ip: ip || '',
            city: city || '',
            path: window.location.pathname
        });

        if (typeof fetch === 'function') {
            fetch(azureUrl, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: payload,
                keepalive: true
            }).catch(function () {
                // Ignore network errors on send
            });
        } else if (navigator.sendBeacon) {
            const blob = new Blob([payload], { type: 'application/json' });
            navigator.sendBeacon(azureUrl, blob);
        } else if (typeof $ !== 'undefined' && $.ajax) {
            $.ajax({
                url: azureUrl,
                type: 'POST',
                contentType: 'application/json',
                data: payload
            });
        }
    }

    function logPageHit() {
        const controller = typeof AbortController !== 'undefined' ? new AbortController() : null;
        const timeoutId = controller ? setTimeout(function () { controller.abort(); }, 1500) : null;

        fetch('https://ipapi.co/json/', { signal: controller ? controller.signal : undefined })
            .then(function (response) {
                if (!response.ok) throw new Error('ipapi request failed');
                return response.json();
            })
            .then(function (data) {
                if (timeoutId) clearTimeout(timeoutId);
                sendHit(data ? data.ip : '', data ? data.city : '');
            })
            .catch(function () {
                if (timeoutId) clearTimeout(timeoutId);
                sendHit('', '');
            });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', logPageHit);
    } else {
        logPageHit();
    }
})();