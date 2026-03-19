/**
 * market-map.js
 */
function initMarketMap() {
    'use strict';
    let msgEl = document.getElementById('market-count');
    if (msgEl) msgEl.textContent = 'Init map script running...';

    const cfg = window.MarketLocatorConfig;
    if (!cfg) {
        if (msgEl) msgEl.textContent = 'ERROR: No MarketLocatorConfig.';
        return;
    }
    if (msgEl) msgEl.textContent = 'Config found, fetching data...';

    let allMarkets = [];
    let markers = {};
    let activeId = null;

    // ── Map init ──────────────────────────────────────────────────────────────
    
    let map = null;
    try {
        if (msgEl) msgEl.textContent = 'Initializing mapping library...';
        map = L.map('market-map', { zoomControl: false })
            .setView([cfg.defaultLat, cfg.defaultLng], cfg.defaultZoom);

        L.control.zoom({ position: 'bottomright' }).addTo(map);

        let tileLayer;
        let isAzure = cfg.useAzure && cfg.azureKey;
        if (isAzure) {
            tileLayer = L.tileLayer(
                `https://atlas.microsoft.com/map/tile?api-version=2.0&tilesetId=microsoft.base.road` +
                `&zoom={z}&x={x}&y={y}&subscription-key=${cfg.azureKey}`,
                { maxZoom: 18, attribution: '© Microsoft Azure Maps' }
            );
        } else {
            tileLayer = L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png', {
                maxZoom: 19,
                attribution: '© OpenStreetMap contributors'
            });
        }
        
        tileLayer.on('tileerror', function(e) {
            if (isAzure) {
                if (msgEl) msgEl.innerHTML = `<span style="color:#c87320; font-weight:bold;">Azure Maps key rejected. Falling back to OpenStreetMap...</span>`;
                isAzure = false;
                map.removeLayer(tileLayer);
                tileLayer = L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png', {
                    maxZoom: 19,
                    attribution: '© OpenStreetMap contributors'
                }).addTo(map);
            } else {
                if (msgEl) msgEl.innerHTML = `<span style="color:#c0392b; font-weight:bold;">Tiling error: Maps blocked by network. URL: ${e?.tile?.src}</span>`;
            }
        });
        
        tileLayer.addTo(map);

        const mapContainer = document.getElementById('market-map');
        if (mapContainer && window.ResizeObserver) {
            new ResizeObserver(() => {
                map.invalidateSize();
            }).observe(mapContainer);
        } else {
            window.addEventListener('resize', () => map.invalidateSize());
        }

    } catch (e) {
        if (msgEl) msgEl.textContent = `Map Init Error: ${e.message}`;
        return;
    }
    if (msgEl) msgEl.textContent = 'Map initialized, fetching data...';

    // ── Marker icon factory ───────────────────────────────────────────────────

    const STATUS_COLOR = { today: '#c0392b', soon: '#c87320', upcoming: '#4a6741' };
    const STATUS_LABEL = { today: 'Today!', soon: 'This Week', upcoming: 'Upcoming' };
    const STATUS_BADGE = { today: 'badge-today', soon: 'badge-soon', upcoming: 'badge-upcoming' };

    function makeIcon(status) {
        const color = STATUS_COLOR[status?.toLowerCase()] || STATUS_COLOR.upcoming;
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
        const statusKey = m.Status?.toLowerCase() || 'upcoming';
        const label = STATUS_LABEL[statusKey] || 'Upcoming';
        const badge = STATUS_BADGE[statusKey] || 'badge-upcoming';
        const extraDates = m.Dates && m.Dates.length > 1
            ? ` <em>+${m.Dates.length - 1} more</em>` : '';
        const mapsUrl = `https://www.google.com/maps/dir/?api=1&destination=${encodeURIComponent(m.Address)}`;
        const icsUrl = `/market-locations/ics/${m.Id}`;

        return `<div class="ml-popup">
            <span class="ml-popup__badge ${badge}">${label}</span>
            <div class="ml-popup__name">${m.Name}</div>
            <div class="ml-popup__meta">
                <span>📅 ${(m.Dates && m.Dates[0]) ? m.Dates[0] : ''}${extraDates}</span>
                <span>🕗 ${m.Hours}</span>
                <span>📍 ${m.Address}</span>
            </div>
            <div class="ml-popup__actions">
                <a href="${icsUrl}" class="ml-btn ml-btn--primary">Add to Calendar</a>
                <a href="${mapsUrl}" target="_blank" class="ml-btn ml-btn--secondary">Get Directions</a>
            </div>
        </div>`;
    }

    // ── Data fetch + render ───────────────────────────────────────────────────

    fetch(cfg.dataUrl)
        .then(r => {
            if (!r.ok) throw new Error(`HTTP ${r.status}`);
            return r.json();
        })
        .then(data => {
            if (msgEl) msgEl.textContent = `Data loaded: ${data.length} records`;
            allMarkets = data;
            data.forEach(addMarker);
            renderCards(data);
            wireFilters();
            
            // Force Leaflet to recalculate its viewport in case the 
            // container size changed after initialization causing a grey background
            setTimeout(() => map.invalidateSize(), 200);
        })
        .catch((e) => {
            if (msgEl) msgEl.textContent = `Fetch error: ${e.message}`;
        });

    function addMarker(m) {
        const marker = L.marker([m.Latitude, m.Longitude], { icon: makeIcon(m.Status) })
            .addTo(map)
            .bindPopup(popupHTML(m), { maxWidth: 300 });

        marker.on('click', () => selectMarket(m.Id));
        markers[m.Id] = marker;
    }

    function renderCards(list) {
        const el = document.getElementById('market-card-list');
        const count = document.getElementById('market-count');
        count.textContent = `${list.length} location${list.length !== 1 ? 's' : ''} found`;

        el.innerHTML = list.map(m => {
            const statusKey = m.Status?.toLowerCase() || 'upcoming';
            const badge = STATUS_BADGE[statusKey] || 'badge-upcoming';
            const label = STATUS_LABEL[statusKey] || 'Upcoming';
            const nextDate = (m.Dates && m.Dates[0]) ? m.Dates[0] : '';
            const extra = m.Dates && m.Dates.length > 1 ? ` +${m.Dates.length - 1} more` : '';
            return `
            <div class="ml-card ${activeId === m.Id ? 'is-active' : ''}"
                 id="ml-card-${m.Id}" data-id="${m.Id}">
                <div class="ml-card__dot status-${statusKey}"></div>
                <div class="ml-card__body">
                    <div class="ml-card__name">${m.Name}</div>
                    <div class="ml-card__date">📅 ${nextDate}${extra}</div>
                    <div class="ml-card__meta">🕗 ${m.Hours} · 📍 ${m.City}</div>
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
        const m = allMarkets.find(x => x.Id === id);
        if (!m) return;

        document.querySelectorAll('.ml-card').forEach(c => c.classList.remove('is-active'));
        const card = document.getElementById(`ml-card-${id}`);
        if (card) {
            card.classList.add('is-active');
            card.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
        }
        map.flyTo([m.Latitude, m.Longitude], 14, { duration: 0.7 });
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
                    const statusKey = m.Status?.toLowerCase();
                    if (filter === 'all') return true;
                    if (filter === 'this-week') return statusKey === 'today' || statusKey === 'soon';
                    if (filter === 'monthly') return m.Frequency === 'Monthly';
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
        if (layout) {
            layout.dataset.view = mode;
        }
    }
}

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', initMarketMap);
} else {
    initMarketMap();
}
