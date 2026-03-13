/**
 * market-map.js
 * Leaflet-based map for the Market Locator plugin.
 * Swaps tile provider based on MarketLocatorConfig.useAzure.
 */
(function () {
    'use strict';

    const cfg = window.MarketLocatorConfig;
    let allMarkets = [];
    let markers = {};
    let activeId = null;

    // ── Map init ──────────────────────────────────────────────────────────────

    const map = L.map('market-map', { zoomControl: false })
        .setView([cfg.defaultLat, cfg.defaultLng], cfg.defaultZoom);

    L.control.zoom({ position: 'bottomright' }).addTo(map);

    if (cfg.useAzure && cfg.azureKey) {
        L.tileLayer(
            `https://atlas.microsoft.com/map/tile?api-version=2.0&tilesetId=microsoft.base.road` +
            `&zoom={z}&x={x}&y={y}&subscription-key=${cfg.azureKey}`,
            { maxZoom: 18, attribution: '© Microsoft Azure Maps' }
        ).addTo(map);
    } else {
        L.tileLayer('https://{s}.basemaps.cartocdn.com/rastertiles/voyager/{z}/{x}/{y}{r}.png', {
            maxZoom: 18,
            attribution: '© OpenStreetMap contributors © CartoDB'
        }).addTo(map);
    }

    // ── Marker icon factory ───────────────────────────────────────────────────

    const STATUS_COLOR = { today: '#c0392b', soon: '#c87320', upcoming: '#4a6741' };
    const STATUS_LABEL = { today: 'Today!', soon: 'This Week', upcoming: 'Upcoming' };
    const STATUS_BADGE = { today: 'badge-today', soon: 'badge-soon', upcoming: 'badge-upcoming' };

    function makeIcon(status) {
        const color = STATUS_COLOR[status] || STATUS_COLOR.upcoming;
        return L.divIcon({
            html: `<div style="
                width:28px;height:28px;border-radius:50% 50% 50% 0;
                transform:rotate(-45deg);background:${color};
                border:2.5px solid white;box-shadow:0 3px 10px rgba(0,0,0,.3)
            "></div>`,
            iconSize: [28, 28],
            iconAnchor: [14, 28],
            popupAnchor: [0, -30],
            className: ''
        });
    }

    function popupHTML(m) {
        const label = STATUS_LABEL[m.status] || 'Upcoming';
        const badge = STATUS_BADGE[m.status] || 'badge-upcoming';
        const extraDates = m.dates.length > 1
            ? ` <em>+${m.dates.length - 1} more</em>` : '';
        const mapsUrl = `https://www.google.com/maps/dir/?api=1&destination=${encodeURIComponent(m.address)}`;
        const icsUrl = `/market-locations/ics/${m.id}`;

        return `<div class="ml-popup">
            <span class="ml-popup__badge ${badge}">${label}</span>
            <div class="ml-popup__name">${m.name}</div>
            <div class="ml-popup__meta">
                <span>📅 ${m.dates[0] || ''}${extraDates}</span>
                <span>🕗 ${m.hours}</span>
                <span>📍 ${m.address}</span>
            </div>
            <div class="ml-popup__actions">
                <a href="${icsUrl}" class="ml-btn ml-btn--primary">Add to Calendar</a>
                <a href="${mapsUrl}" target="_blank" class="ml-btn ml-btn--secondary">Get Directions</a>
            </div>
        </div>`;
    }

    // ── Data fetch + render ───────────────────────────────────────────────────

    fetch(cfg.dataUrl)
        .then(r => r.json())
        .then(data => {
            allMarkets = data;
            data.forEach(addMarker);
            renderCards(data);
            wireFilters();
        })
        .catch(() => {
            document.getElementById('market-count').textContent = 'Could not load market data.';
        });

    function addMarker(m) {
        const marker = L.marker([m.latitude, m.longitude], { icon: makeIcon(m.status) })
            .addTo(map)
            .bindPopup(popupHTML(m), { maxWidth: 300 });

        marker.on('click', () => selectMarket(m.id));
        markers[m.id] = marker;
    }

    function renderCards(list) {
        const el = document.getElementById('market-card-list');
        const count = document.getElementById('market-count');
        count.textContent = `${list.length} location${list.length !== 1 ? 's' : ''} found`;

        el.innerHTML = list.map(m => {
            const badge = STATUS_BADGE[m.status] || 'badge-upcoming';
            const label = STATUS_LABEL[m.status] || 'Upcoming';
            const nextDate = m.dates[0] || '';
            const extra = m.dates.length > 1 ? ` +${m.dates.length - 1} more` : '';
            return `
            <div class="ml-card ${activeId === m.id ? 'is-active' : ''}"
                 id="ml-card-${m.id}" data-id="${m.id}">
                <div class="ml-card__dot status-${m.status}"></div>
                <div class="ml-card__body">
                    <div class="ml-card__name">${m.name}</div>
                    <div class="ml-card__date">📅 ${nextDate}${extra}</div>
                    <div class="ml-card__meta">🕗 ${m.hours} · 📍 ${m.city}</div>
                </div>
                <span class="ml-card__badge ${badge}">${label}</span>
            </div>`;
        }).join('');

        el.querySelectorAll('.ml-card').forEach(card => {
            card.addEventListener('click', () => selectMarket(+card.dataset.id));
        });
    }

    function selectMarket(id) {
        activeId = id;
        const m = allMarkets.find(x => x.id === id);
        if (!m) return;

        document.querySelectorAll('.ml-card').forEach(c => c.classList.remove('is-active'));
        const card = document.getElementById(`ml-card-${id}`);
        if (card) {
            card.classList.add('is-active');
            card.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
        }
        map.flyTo([m.latitude, m.longitude], 14, { duration: 0.7 });
        markers[id]?.openPopup();
    }

    // ── Filters ───────────────────────────────────────────────────────────────

    function wireFilters() {
        document.querySelectorAll('.mf-btn').forEach(btn => {
            btn.addEventListener('click', () => {
                document.querySelectorAll('.mf-btn').forEach(b => b.classList.remove('active'));
                btn.classList.add('active');

                const filter = btn.dataset.filter;
                const filtered = allMarkets.filter(m => {
                    if (filter === 'all') return true;
                    if (filter === 'this-week') return m.status === 'today' || m.status === 'soon';
                    if (filter === 'monthly') return m.frequency === 'Monthly';
                    return true;
                });

                // Rebuild markers
                Object.values(markers).forEach(mk => map.removeLayer(mk));
                markers = {};
                filtered.forEach(addMarker);
                renderCards(filtered);
            });
        });

        document.getElementById('btn-map').addEventListener('click', () => setView('map'));
        document.getElementById('btn-list').addEventListener('click', () => setView('list'));
    }

    function setView(mode) {
        document.getElementById('btn-map').classList.toggle('active', mode === 'map');
        document.getElementById('btn-list').classList.toggle('active', mode === 'list');
        const layout = document.getElementById('market-layout');
        layout.dataset.view = mode;
    }

}());
