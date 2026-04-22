/**
 * ═══════════════════════════════════════════════════════════════════
 *  SEASONAL BACKGROUNDS — nopCommerce
 *  Drop this file into /wwwroot/js/seasonal-bg.js
 *  Then in _Layout.cshtml add just before </body>:
 *    <div id="seasonal-layer"></div>
 *    <script src="~/js/seasonal-bg.js"></script>
 * ═══════════════════════════════════════════════════════════════════
 *
 *  CONFIGURATION
 *  Edit the SEASONS array below to control dates, intensity, and
 *  which themes are active. Date ranges use MM-DD format and support
 *  year-wrap (e.g. Christmas: Dec 1 → Jan 5).
 *
 *  Each season object:
 *  {
 *    id:        string   — unique key
 *    name:      string   — display name (used in console logs)
 *    start:     "MM-DD"  — inclusive start date
 *    end:       "MM-DD"  — inclusive end date
 *    enabled:   boolean  — quick on/off switch
 *    intensity: 1|2|3    — 1=subtle, 2=normal, 3=festive
 *    priority:  number   — if two ranges overlap, higher wins
 *  }
 */

;(function () {
  'use strict';

  /* ═══════════════════════════════════════════════════════════════
     SEASON SCHEDULE  ← Edit this to configure dates
     ═══════════════════════════════════════════════════════════════ */
  const SEASONS = [
    { id: 'valentines',    name: "Valentine's Day",      start: '02-07', end: '02-14', enabled: true,  intensity: 2, priority: 10 },
    { id: 'stpatricks',    name: "St Patrick's Day",     start: '03-10', end: '03-17', enabled: true,  intensity: 2, priority: 10 },
    { id: 'easter',        name: 'Easter',               start: '03-24', end: '04-06', enabled: true,  intensity: 2, priority: 10 },
    { id: 'independence',  name: 'Independence Day',     start: '07-01', end: '07-07', enabled: true,  intensity: 3, priority: 10 },
    { id: 'halloween',     name: 'Halloween',            start: '10-15', end: '10-31', enabled: true,  intensity: 2, priority: 10 },
    { id: 'christmas',     name: 'Christmas',            start: '12-01', end: '01-05', enabled: true,  intensity: 3, priority: 20 },
    { id: 'newyear',       name: "New Year",             start: '12-29', end: '01-02', enabled: true,  intensity: 3, priority: 30 },
  ];

  /* ═══════════════════════════════════════════════════════════════
     SHARED UTILITIES
     ═══════════════════════════════════════════════════════════════ */
  const rnd   = (a, b) => Math.random() * (b - a) + a;
  const rndI  = (a, b) => Math.floor(rnd(a, b + 1));
  const pick  = arr => arr[Math.floor(Math.random() * arr.length)];
  const ns    = 'http://www.w3.org/2000/svg';

  function svgEl(tag, attrs = {}) {
    const e = document.createElementNS(ns, tag);
    Object.entries(attrs).forEach(([k, v]) => e.setAttribute(k, v));
    return e;
  }

  function div(styles = {}, classes = []) {
    const d = document.createElement('div');
    Object.assign(d.style, styles);
    classes.forEach(c => d.classList.add(c));
    return d;
  }

  function makeSVG(w, h, viewBox) {
    return svgEl('svg', { width: w, height: h, viewBox: viewBox || `0 0 ${w} ${h}` });
  }

  function injectCSS(id, css) {
    if (document.getElementById(id)) return;
    const s = document.createElement('style');
    s.id = id;
    s.textContent = css;
    document.head.appendChild(s);
  }

  /* ─── Date matching ───────────────────────────────────────────── */
  function dateInRange(mmddStart, mmddEnd) {
    const now   = new Date();
    const today = (now.getMonth() + 1) * 100 + now.getDate(); // MMDD as int

    const s = parseInt(mmddStart.replace('-', ''), 10);
    const e = parseInt(mmddEnd.replace('-', ''), 10);

    if (s <= e) {
      return today >= s && today <= e;
    } else {
      // Wraps year boundary (e.g. Dec–Jan)
      return today >= s || today <= e;
    }
  }

  /* ─── Intensity multiplier ────────────────────────────────────── */
  function qty(base, intensity) {
    return Math.round(base * [0.5, 1, 1.7][intensity - 1]);
  }

  /* ─── Viewport scale ──────────────────────────────────────────── */
  const vw = window.innerWidth;

  /* ═══════════════════════════════════════════════════════════════
     SHARED COMPONENTS (reused across themes)
     ═══════════════════════════════════════════════════════════════ */

  function makeCloud(layer, opacity = 0.45) {
    const w = rnd(100, 200);
    const d = div({
      position: 'absolute',
      top: rnd(2, 22) + '%',
      left: '-220px',
      opacity,
      zIndex: '0',
      animation: `sbDriftCloud ${rnd(45, 100)}s linear ${rnd(-80, 0)}s infinite`,
    });
    const svg = makeSVG(w, w * 0.5, '0 0 120 60');
    const path = svgEl('path', {
      d: 'M20,50 Q5,50 5,38 Q5,28 15,26 Q14,10 28,9 Q36,2 46,8 Q54,2 65,8 Q78,3 82,14 Q96,12 98,26 Q110,26 110,38 Q110,50 96,50 Z',
      fill: 'white'
    });
    svg.appendChild(path);
    d.appendChild(svg);
    layer.appendChild(d);
  }

  /* ═══════════════════════════════════════════════════════════════
     THEME: EASTER
     ═══════════════════════════════════════════════════════════════ */
  function renderEaster(layer, intensity) {
    layer.style.background = 'linear-gradient(180deg,#e8f5ff 0%,#fdf8f0 70%,#fdf6ee 100%)';
    appendGrass(layer, '#7bc47f', '#5aad5e');

    const eggPalettes = [
      ['#f9e04b','#e05b96'],['#7ed8f6','#e05b96'],['#b5e86a','#f4a732'],
      ['#c89be0','#f9e04b'],['#f7a35c','#7ed8f6'],['#f77171','#b5e86a'],
    ];
    const petalColors = ['#ffb7c5','#ffd6e0','#ffe0a3','#d4f1a3','#c9e8ff','#e8c9ff'];

    // Clouds
    for (let i = 0; i < qty(4, intensity); i++) makeCloud(layer);

    // Eggs
    const eggCount = qty(Math.min(Math.floor(vw / 160), 10), intensity);
    for (let i = 0; i < eggCount; i++) {
      const size = rnd(32, 52);
      const [c1, c2] = pick(eggPalettes);
      const clipId = 'ec' + Math.random().toString(36).slice(2, 7);
      const svg = makeSVG(size, size * 1.28, '0 0 44 56');
      const defs = svgEl('defs');
      const clip = svgEl('clipPath', { id: clipId });
      clip.appendChild(svgEl('ellipse', { cx:22, cy:30, rx:18, ry:24 }));
      defs.appendChild(clip); svg.appendChild(defs);
      const g = svgEl('g', { 'clip-path': `url(#${clipId})` });
      g.appendChild(svgEl('rect', { width:44, height:56, fill:c1 }));
      if (Math.random() > 0.5) {
        for (let y = 10; y < 56; y += 12) g.appendChild(svgEl('rect', { x:0, y, width:44, height:6, fill:c2 }));
      } else {
        for (let r = 0; r < 5; r++) for (let c = 0; c < 4; c++)
          g.appendChild(svgEl('circle', { cx: 6+c*11+(r%2)*5, cy: 10+r*11, r:3, fill:c2 }));
      }
      g.appendChild(svgEl('ellipse', { cx:14, cy:15, rx:5, ry:7, fill:'rgba(255,255,255,0.3)', transform:'rotate(-20 14 15)' }));
      svg.appendChild(g);

      const d = div({
        position: 'absolute', bottom: '38px',
        left: rnd(3, 95) + 'vw', zIndex: '1',
        animation: `sbEggBob ${rnd(2,4)}s ease-in-out ${rnd(-4,0)}s infinite alternate`,
        transformOrigin: 'center bottom',
      });
      d.appendChild(svg);
      layer.appendChild(d);
    }

    // Bunnies
    for (let i = 0; i < qty(Math.min(Math.floor(vw/400)+2, 5), intensity); i++) {
      const hopDist = rnd(80, 200);
      const d = div({
        position: 'absolute', bottom: '34px', zIndex: '2',
        left: rnd(5, 80) + 'vw',
        width: '52px', height: '52px',
        '--hop-dist': hopDist + 'px',
        animation: `sbBunnyHop ${rnd(12,22)}s linear ${rnd(-20,0)}s infinite`,
      });
      d.style.setProperty('--hop-dist', hopDist + 'px');
      d.appendChild(makeBunnySVG());
      layer.appendChild(d);
    }

    // Petals
    for (let i = 0; i < qty(Math.min(Math.floor(vw/80), 20), intensity); i++) {
      const size = rnd(8, 16);
      const d = div({
        position: 'absolute', top: '-30px',
        left: rnd(0, 100) + 'vw',
        width: size+'px', height: size+'px',
        background: pick(petalColors),
        borderRadius: '50% 0 50% 0',
        animation: `sbPetalFall ${rnd(6,14)}s linear ${rnd(-14,0)}s infinite`,
      });
      layer.appendChild(d);
    }

    // Butterflies
    /*for (let i = 0; i < qty(4, intensity); i++) {
      const d = div({
        position: 'absolute',
        top: rnd(8,55)+'vh', left: rnd(5,88)+'vw', zIndex:'1',
        animation: `sbButterfly ${rnd(5,9)}s ease-in-out ${rnd(-9,0)}s infinite`,
      });
      d.appendChild(makeButterflySVG());
      layer.appendChild(d);
    }*/
  }

  /* ═══════════════════════════════════════════════════════════════
     THEME: CHRISTMAS
     ═══════════════════════════════════════════════════════════════ */
  function renderChristmas(layer, intensity) {
    layer.style.background = 'linear-gradient(180deg,#0a1628 0%,#1a2d4a 55%,#0d1f0d 100%)';
    appendSnowGround(layer);

    // Snowflakes
    for (let i = 0; i < qty(Math.min(Math.floor(vw/30), 60), intensity); i++) {
      const size = rnd(4, 14);
      const d = div({
        position: 'absolute',
        top: '-20px',
        left: rnd(0,100) + 'vw',
        width: size+'px', height: size+'px',
        background: 'white',
        borderRadius: '50%',
        opacity: rnd(0.5, 1),
        animation: `sbSnowFall ${rnd(5,18)}s linear ${rnd(-18,0)}s infinite`,
      });
      layer.appendChild(d);
    }

    // Stars
    for (let i = 0; i < qty(30, intensity); i++) {
      const size = rnd(1.5, 4);
      const d = div({
        position: 'absolute',
        top: rnd(2,50)+'vh', left: rnd(0,100)+'vw',
        width: size+'px', height: size+'px',
        background: 'white',
        borderRadius: '50%',
        opacity: rnd(0.3, 0.9),
        animation: `sbTwinkle ${rnd(1.5,4)}s ease-in-out ${rnd(-4,0)}s infinite alternate`,
      });
      layer.appendChild(d);
    }

    // Christmas trees
    for (let i = 0; i < qty(Math.min(Math.floor(vw/250)+1, 6), intensity); i++) {
      const d = div({ position:'absolute', bottom:'18px', left: rnd(2,88)+'vw', zIndex:'2' });
      d.appendChild(makeChristmasTreeSVG());
      layer.appendChild(d);
    }

    // Reindeer
    for (let i = 0; i < qty(1, intensity); i++) {
      const d = div({
        position: 'absolute',
        top: rnd(8,25)+'vh',
        left: '-120px',
        zIndex: '3',
        animation: `sbFlyAcross ${rnd(14,22)}s linear ${rnd(-8,0)}s infinite`,
      });
      d.appendChild(makeReindeersAndSleighSVG());
      layer.appendChild(d);
    }
  }

  /* ═══════════════════════════════════════════════════════════════
     THEME: HALLOWEEN
     ═══════════════════════════════════════════════════════════════ */
  function renderHalloween(layer, intensity) {
    layer.style.background = 'linear-gradient(180deg,#0d0714 0%,#1a0a2e 45%,#2a1005 100%)';
    appendGrass(layer, '#1a2e10', '#0f1e08');

    // Bats
    for (let i = 0; i < qty(8, intensity); i++) {
      const d = div({
        position: 'absolute',
        top: rnd(5,55)+'vh', left: '-80px',
        zIndex: '3',
        animation: `sbBatFly ${rnd(8,20)}s linear ${rnd(-20,0)}s infinite`,
        opacity: rnd(0.6, 1),
      });
      d.appendChild(makeBatSVG());
      layer.appendChild(d);
    }

    // Pumpkins
    for (let i = 0; i < qty(Math.min(Math.floor(vw/220), 7), intensity); i++) {
      const d = div({ position:'absolute', bottom:'34px', left: rnd(3,90)+'vw', zIndex:'2' });
      d.appendChild(makePumpkinSVG());
      layer.appendChild(d);
    }

    // Moon
    const moon = div({ position:'absolute', top:'8vh', right:'8vw', zIndex:'1' });
    moon.appendChild(makeMoonSVG());
    layer.appendChild(moon);

    // Ghosts
    for (let i = 0; i < qty(4, intensity); i++) {
      const d = div({
        position: 'absolute',
        top: rnd(15,65)+'vh', left: rnd(5,85)+'vw',
        zIndex: '2',
        animation: `sbGhostFloat ${rnd(4,8)}s ease-in-out ${rnd(-8,0)}s infinite`,
        opacity: rnd(0.5, 0.85),
      });
      d.appendChild(makeGhostSVG());
      layer.appendChild(d);
    }

    // Stars (creepy purple-ish)
    for (let i = 0; i < 25; i++) {
      const size = rnd(1.5,3.5);
      const d = div({
        position:'absolute', top:rnd(2,45)+'vh', left:rnd(0,100)+'vw',
        width:size+'px', height:size+'px',
        background: pick(['#fff','#d8b4fe','#fb923c']),
        borderRadius:'50%', opacity: rnd(0.3,0.8),
        animation: `sbTwinkle ${rnd(1.5,4)}s ease-in-out ${rnd(-4,0)}s infinite alternate`,
      });
      layer.appendChild(d);
    }
  }

  /* ═══════════════════════════════════════════════════════════════
     THEME: ST PATRICK'S DAY
     ═══════════════════════════════════════════════════════════════ */
  function renderStPatricks(layer, intensity) {
    layer.style.background = 'linear-gradient(180deg,#e8f5e9 0%,#f1f8e9 60%,#e8f5e9 100%)';
    appendGrass(layer, '#43a047', '#2e7d32');

    for (let i = 0; i < qty(5, intensity); i++) makeCloud(layer, 0.5);

    // Shamrocks falling
    for (let i = 0; i < qty(Math.min(Math.floor(vw/70), 22), intensity); i++) {
      const size = rnd(16, 36);
      const d = div({
        position:'absolute', top:'-40px', left: rnd(0,100)+'vw',
        animation: `sbPetalFall ${rnd(6,16)}s linear ${rnd(-16,0)}s infinite`,
        opacity: rnd(0.5,0.9),
      });
      d.appendChild(makeShamrockSVG(size));
      layer.appendChild(d);
    }

    // Rainbow
    const rb = div({ position:'absolute', top:'5vh', left:'50%', transform:'translateX(-50%)', zIndex:'0', opacity:'0.35' });
    rb.appendChild(makeRainbowSVG());
    layer.appendChild(rb);

    // Pot of gold
    const pot = div({ position:'absolute', bottom:'40px', right:'8vw', zIndex:'2' });
    pot.appendChild(makePotOfGoldSVG());
    layer.appendChild(pot);
  }

  /* ═══════════════════════════════════════════════════════════════
     THEME: INDEPENDENCE DAY (4th July)
     ═══════════════════════════════════════════════════════════════ */
  function renderIndependence(layer, intensity) {
    layer.style.background = 'linear-gradient(180deg,#0a0a2e 0%,#0d1b4a 60%,#1a0a05 100%)';

    // Stars background
    for (let i = 0; i < 40; i++) {
      const size = rnd(1,3.5);
      const d = div({
        position:'absolute', top:rnd(0,60)+'vh', left:rnd(0,100)+'vw',
        width:size+'px', height:size+'px', background:'white',
        borderRadius:'50%', opacity:rnd(0.3,0.9),
        animation:`sbTwinkle ${rnd(1.5,4)}s ease-in-out ${rnd(-4,0)}s infinite alternate`,
      });
      layer.appendChild(d);
    }

    // Fireworks
    for (let i = 0; i < qty(5, intensity); i++) {
      const d = div({
        position:'absolute',
        top: rnd(5,55)+'vh', left: rnd(5,90)+'vw',
        zIndex:'2',
        animation: `sbFirework ${rnd(2.5,4.5)}s ease-out ${rnd(-4.5,0)}s infinite`,
        opacity: 0,
      });
      d.appendChild(makeFireworkSVG());
      layer.appendChild(d);
    }

    // Flags
    for (let i = 0; i < qty(Math.min(Math.floor(vw/300)+1,5), intensity); i++) {
      const d = div({
        position:'absolute', bottom:'8px', left: rnd(2,88)+'vw',
        zIndex:'2',
        animation:`sbFlagWave ${rnd(1.8,3)}s ease-in-out ${rnd(-3,0)}s infinite alternate`,
        transformOrigin:'left bottom',
      });
      d.appendChild(makeFlagSVG());
      layer.appendChild(d);
    }

    // Bunting strings
    for (let i = 0; i < qty(2, intensity); i++) {
      const d = div({ position:'absolute', top: (15 + i*20)+'vh', left:'0', width:'100%', zIndex:'1' });
      d.appendChild(makeBuntingSVG());
      layer.appendChild(d);
    }
  }

  /* ═══════════════════════════════════════════════════════════════
     THEME: VALENTINE'S DAY
     ═══════════════════════════════════════════════════════════════ */
  function renderValentines(layer, intensity) {
    layer.style.background = 'linear-gradient(180deg,#fff0f3 0%,#ffe4ea 60%,#fff5f7 100%)';

    // Falling hearts
    const pinks = ['#ff6b8a','#ff8fab','#ffb3c1','#ff4d6d','#c9184a','#ff99aa'];
    for (let i = 0; i < qty(Math.min(Math.floor(vw/55),28), intensity); i++) {
      const size = rnd(14, 32);
      const d = div({
        position:'absolute', top:'-40px', left: rnd(0,100)+'vw',
        animation:`sbPetalFall ${rnd(5,13)}s linear ${rnd(-13,0)}s infinite`,
        opacity: rnd(0.4,0.9),
      });
      d.appendChild(makeHeartSVG(size, pick(pinks)));
      layer.appendChild(d);
    }

    // Big heart decorations (static)
    for (let i = 0; i < qty(3, intensity); i++) {
      const size = rnd(50,90);
      const d = div({
        position:'absolute',
        top: rnd(10,70)+'vh', left: rnd(5,85)+'vw', zIndex:'0',
        opacity: rnd(0.06, 0.14),
        animation:`sbEggBob ${rnd(3,6)}s ease-in-out ${rnd(-6,0)}s infinite alternate`,
      });
      d.appendChild(makeHeartSVG(size, '#ff4d6d'));
      layer.appendChild(d);
    }
  }

  /* ═══════════════════════════════════════════════════════════════
     THEME: NEW YEAR
     ═══════════════════════════════════════════════════════════════ */
  function renderNewYear(layer, intensity) {
    layer.style.background = 'linear-gradient(180deg,#000010 0%,#0a0020 55%,#100510 100%)';

    // Many stars
    for (let i = 0; i < 55; i++) {
      const size = rnd(1,3);
      const d = div({
        position:'absolute', top:rnd(0,55)+'vh', left:rnd(0,100)+'vw',
        width:size+'px', height:size+'px',
        background: pick(['white','#ffd700','#c0c0ff','#ffc0cb']),
        borderRadius:'50%', opacity:rnd(0.3,1),
        animation:`sbTwinkle ${rnd(1,3.5)}s ease-in-out ${rnd(-4,0)}s infinite alternate`,
      });
      layer.appendChild(d);
    }

    // Fireworks (more, brighter)
    for (let i = 0; i < qty(8, intensity); i++) {
      const d = div({
        position:'absolute',
        top: rnd(5,60)+'vh', left: rnd(2,93)+'vw', zIndex:'3',
        animation:`sbFirework ${rnd(2,4)}s ease-out ${rnd(-4,0)}s infinite`,
        opacity:0,
      });
      d.appendChild(makeFireworkSVG(pick(['#ffd700','#ff69b4','#00bfff','#ff4500','#adff2f','#ff1493'])));
      layer.appendChild(d);
    }

    // Confetti
    const confettiColors = ['#ffd700','#ff69b4','#00bfff','#ff4500','#adff2f','#ffffff','#c0c0ff'];
    for (let i = 0; i < qty(Math.min(Math.floor(vw/35),45), intensity); i++) {
      const w = rnd(6,12), h = rnd(3,7);
      const d = div({
        position:'absolute', top:'-20px', left: rnd(0,100)+'vw',
        width:w+'px', height:h+'px',
        background: pick(confettiColors),
        borderRadius: Math.random() > 0.5 ? '50%' : '1px',
        animation:`sbPetalFall ${rnd(4,10)}s linear ${rnd(-10,0)}s infinite`,
        opacity:rnd(0.7,1),
      });
      layer.appendChild(d);
    }
  }

  /* ═══════════════════════════════════════════════════════════════
     SVG ELEMENT BUILDERS
     ═══════════════════════════════════════════════════════════════ */

  function makeBunnySVG() {
    const svg = makeSVG(52, 52, '0 0 52 52');
    const parts = [
      // left ear outer/inner
      svgEl('ellipse',{cx:18,cy:10,rx:5,ry:12,fill:'#f0e0d0'}),
      svgEl('ellipse',{cx:18,cy:10,rx:2.5,ry:8,fill:'#f4b8c0'}),
      svgEl('ellipse',{cx:28,cy:8,rx:5,ry:12,fill:'#f0e0d0'}),
      svgEl('ellipse',{cx:28,cy:8,rx:2.5,ry:8,fill:'#f4b8c0'}),
      // body, head, tail
      svgEl('ellipse',{cx:26,cy:36,rx:14,ry:13,fill:'#f0e0d0'}),
      svgEl('circle', {cx:23,cy:24,r:11,fill:'#f5ece2'}),
      svgEl('circle', {cx:39,cy:34,r:5,fill:'#fff'}),
      // face
      svgEl('circle', {cx:26,cy:22,r:2,fill:'#3a2010'}),
      svgEl('circle', {cx:27,cy:21,r:0.7,fill:'white'}),
      svgEl('ellipse',{cx:29,cy:25,rx:2,ry:1.2,fill:'#f4a0b0'}),
      svgEl('path',   {d:'M27,27 Q29,29 31,27',fill:'none',stroke:'#c47080','stroke-width':1}),
      svgEl('ellipse',{cx:18,cy:46,rx:4,ry:3,fill:'#e8d0bc'}),
    ];
    parts.forEach(p => svg.appendChild(p));
    return svg;
  }

  function makeButterflySVG() {
    const colors = pick([
      ['#f9a8d4','#fbcfe8'],['#93c5fd','#bfdbfe'],
      ['#86efac','#bbf7d0'],['#fcd34d','#fde68a'],
    ]);
    const svg = makeSVG(34, 28, '0 0 34 28');
    [
      svgEl('ellipse',{cx:10,cy:13,rx:10,ry:8,fill:colors[0],class:'sbWingL'}),
      svgEl('ellipse',{cx:24,cy:13,rx:10,ry:8,fill:colors[1],class:'sbWingR'}),
      svgEl('ellipse',{cx:10,cy:21,rx:7, ry:5,fill:colors[0],class:'sbWingL'}),
      svgEl('ellipse',{cx:24,cy:21,rx:7, ry:5,fill:colors[1],class:'sbWingR'}),
      svgEl('ellipse',{cx:17,cy:15,rx:2, ry:8,fill:'#78350f'}),
    ].forEach(p => svg.appendChild(p));
    return svg;
  }

  function makeHeartSVG(size, color) {
    const svg = makeSVG(size, size * 0.9, '0 0 100 90');
    svg.appendChild(svgEl('path', {
      d: 'M50,85 C50,85 5,55 5,28 C5,10 18,2 33,8 C40,11 46,17 50,22 C54,17 60,11 67,8 C82,2 95,10 95,28 C95,55 50,85 50,85 Z',
      fill: color
    }));
    return svg;
  }

  function makeShamrockSVG(size) {
    const svg = makeSVG(size, size, '0 0 60 70');
    const greens = pick([['#2e7d32','#43a047'],['#1b5e20','#388e3c'],['#33691e','#558b2f']]);
    [
      svgEl('circle',{cx:20,cy:22,r:14,fill:greens[0]}),
      svgEl('circle',{cx:40,cy:22,r:14,fill:greens[0]}),
      svgEl('circle',{cx:30,cy:10,r:14,fill:greens[0]}),
      svgEl('rect',  {x:27,y:28,width:6,height:28,rx:3,fill:greens[1]}),
    ].forEach(p => svg.appendChild(p));
    return svg;
  }

  function makeRainbowSVG() {
    const svg = makeSVG(500, 250, '0 0 500 250');
    const colors = ['#ff0000','#ff7700','#ffff00','#00bb00','#0000ff','#8800ff'];
    colors.forEach((c, i) => {
      const r = 220 - i * 18;
      svg.appendChild(svgEl('path', {
        d: `M ${250-r},250 A ${r},${r} 0 0,1 ${250+r},250`,
        fill:'none', stroke:c, 'stroke-width':16, opacity:0.8
      }));
    });
    return svg;
  }

  function makePotOfGoldSVG() {
    const svg = makeSVG(70, 80, '0 0 70 80');
    [
      // rim
      svgEl('ellipse',{cx:35,cy:28,rx:28,ry:8,fill:'#1a1a1a'}),
      // pot body
      svgEl('path',{d:'M10,28 Q7,65 35,72 Q63,65 60,28 Z',fill:'#2a2a2a'}),
      // gold coins
      svgEl('ellipse',{cx:35,cy:26,rx:26,ry:7,fill:'#ffd700'}),
      svgEl('circle',{cx:25,cy:22,r:5,fill:'#ffb700'}),
      svgEl('circle',{cx:35,cy:20,r:5,fill:'#ffd700'}),
      svgEl('circle',{cx:45,cy:22,r:5,fill:'#ffb700'}),
      svgEl('circle',{cx:30,cy:16,r:4,fill:'#ffd700'}),
      svgEl('circle',{cx:40,cy:16,r:4,fill:'#ffb700'}),
    ].forEach(p => svg.appendChild(p));
    return svg;
  }

  function makePumpkinSVG() {
    const svg = makeSVG(60, 65, '0 0 60 65');
    const orange = pick(['#e65c00','#f97316','#ea580c']);
    [
      // stem
      svgEl('rect',{x:27,y:2,width:6,height:10,rx:2,fill:'#4a7c3f'}),
      // lobes
      svgEl('ellipse',{cx:18,cy:36,rx:14,ry:18,fill:orange,opacity:0.9}),
      svgEl('ellipse',{cx:42,cy:36,rx:14,ry:18,fill:orange,opacity:0.9}),
      svgEl('ellipse',{cx:30,cy:34,rx:16,ry:20,fill:orange}),
      // face - eyes
      svgEl('polygon',{points:'22,30 25,24 28,30',fill:'#1a0800'}),
      svgEl('polygon',{points:'32,30 35,24 38,30',fill:'#1a0800'}),
      // mouth
      svgEl('path',{d:'M18,42 Q22,48 30,45 Q38,48 42,42',fill:'#1a0800'}),
      // glow
      svgEl('ellipse',{cx:30,cy:38,rx:10,ry:12,fill:'rgba(255,150,0,0.15)'}),
    ].forEach(p => svg.appendChild(p));
    return svg;
  }

  function makeBatSVG() {
    const svg = makeSVG(60, 30, '0 0 60 30');
    [
      svgEl('path',{d:'M30,15 Q20,2 5,8 Q12,15 10,22 Q20,18 30,15 Z',fill:'#1a0030'}),
      svgEl('path',{d:'M30,15 Q40,2 55,8 Q48,15 50,22 Q40,18 30,15 Z',fill:'#1a0030'}),
      svgEl('circle',{cx:30,cy:16,r:6,fill:'#2a0040'}),
      svgEl('polygon',{points:'27,11 29,8 31,11',fill:'#3a0050'}),
      svgEl('polygon',{points:'31,11 33,8 35,11',fill:'#3a0050'}),
      svgEl('circle',{cx:28,cy:16,r:1.5,fill:'#ff0000'}),
      svgEl('circle',{cx:32,cy:16,r:1.5,fill:'#ff0000'}),
    ].forEach(p => svg.appendChild(p));
    return svg;
  }

  function makeGhostSVG() {
    const svg = makeSVG(45, 55, '0 0 45 55');
    [
      svgEl('path',{d:'M5,30 Q5,5 22,5 Q40,5 40,30 L40,52 Q34,46 30,52 Q26,46 22,52 Q18,46 15,52 Q11,46 5,52 Z',fill:'white',opacity:0.9}),
      svgEl('circle',{cx:16,cy:24,r:4,fill:'#333'}),
      svgEl('circle',{cx:29,cy:24,r:4,fill:'#333'}),
      svgEl('circle',{cx:17,cy:23,r:1.5,fill:'white'}),
      svgEl('circle',{cx:30,cy:23,r:1.5,fill:'white'}),
      svgEl('ellipse',{cx:22,cy:32,rx:4,ry:2.5,fill:'#333'}),
    ].forEach(p => svg.appendChild(p));
    return svg;
  }

  function makeMoonSVG() {
    const svg = makeSVG(90, 90, '0 0 90 90');
    [
      svgEl('circle',{cx:45,cy:45,r:40,fill:'#f5c842'}),
      // craters
      svgEl('circle',{cx:28,cy:35,r:6,fill:'rgba(0,0,0,0.1)'}),
      svgEl('circle',{cx:55,cy:55,r:9,fill:'rgba(0,0,0,0.08)'}),
      svgEl('circle',{cx:60,cy:28,r:4,fill:'rgba(0,0,0,0.09)'}),
      // shadow slice
      svgEl('path',{d:'M45,5 A40,40 0 0,0 45,85 A30,40 0 0,1 45,5 Z',fill:'rgba(0,0,0,0.12)'}),
    ].forEach(p => svg.appendChild(p));
    return svg;
  }

  function makeChristmasTreeSVG() {
    const svg = makeSVG(70, 100, '0 0 70 100');
    const baubleColors = ['#ff4444','#ffd700','#4488ff','#ff88cc','#88ff88'];
    [
      svgEl('rect',{x:28,y:88,width:14,height:12,rx:2,fill:'#5d4037'}),
      svgEl('polygon',{points:'35,5 10,45 60,45',fill:'#2e7d32'}),
      svgEl('polygon',{points:'35,22 8,62 62,62',fill:'#388e3c'}),
      svgEl('polygon',{points:'35,40 5,82 65,82',fill:'#43a047'}),
      svgEl('circle',{cx:35,cy:8,r:5,fill:'#ffd700'}),
    ].forEach(p => svg.appendChild(p));
    // baubles
    [[20,50],[50,50],[14,72],[35,68],[56,72],[28,36],[42,36]].forEach(([x,y]) => {
      svg.appendChild(svgEl('circle',{cx:x,cy:y,r:4,fill:pick(baubleColors)}));
    });
    return svg;
  }

  function makeReindeersAndSleighSVG() {
    const svg = makeSVG(220, 70, '0 0 220 70');
    // Sleigh
    [
      svgEl('path',{d:'M130,35 Q150,20 180,25 Q200,28 210,38 Q200,45 130,45 Z',fill:'#cc1111'}),
      svgEl('path',{d:'M140,44 Q160,55 200,50',fill:'none',stroke:'#8b0000','stroke-width':3}),
      svgEl('path',{d:'M145,44 Q155,58 195,54',fill:'none',stroke:'#8b0000','stroke-width':3}),
      svgEl('rect',{x:155,y:22,width:16,height:12,rx:3,fill:'#ffd700'}),
      // Reindeer (simplified)
      svgEl('ellipse',{cx:80,cy:32,rx:22,ry:10,fill:'#8b5e3c'}),
      svgEl('circle', {cx:55,cy:28,r:9,fill:'#9b6e4c'}),
      svgEl('line',   {x1:60,y1:20,x2:55,y2:10,stroke:'#6b3e2c','stroke-width':2}),
      svgEl('line',   {x1:52,y1:20,x2:48,y2:10,stroke:'#6b3e2c','stroke-width':2}),
      svgEl('line',   {x1:55,y1:10,x2:62,y2:7, stroke:'#6b3e2c','stroke-width':2}),
      svgEl('line',   {x1:48,y1:10,x2:43,y2:7, stroke:'#6b3e2c','stroke-width':2}),
      svgEl('circle', {cx:50,cy:30,r:3.5,fill:'#ff4444'}), // red nose
      // harness line
      svgEl('line',   {x1:98,y1:32,x2:130,y2:36,stroke:'#8b5e3c','stroke-width':2}),
    ].forEach(p => svg.appendChild(p));
    return svg;
  }

  function makeFireworkSVG(color) {
    color = color || pick(['#ff4444','#ffd700','#44aaff','#ff88cc','#88ff44','#ff8844']);
    const svg = makeSVG(80, 80, '0 0 80 80');
    const g = svgEl('g');
    for (let i = 0; i < 12; i++) {
      const angle = (i / 12) * 360;
      const len = rnd(20, 34);
      const rad = angle * Math.PI / 180;
      const x2 = 40 + Math.cos(rad) * len;
      const y2 = 40 + Math.sin(rad) * len;
      g.appendChild(svgEl('line',{x1:40,y1:40,x2,y2,stroke:color,'stroke-width':2.5,'stroke-linecap':'round'}));
      g.appendChild(svgEl('circle',{cx:x2,cy:y2,r:2.5,fill:color}));
    }
    g.appendChild(svgEl('circle',{cx:40,cy:40,r:5,fill:color}));
    svg.appendChild(g);
    return svg;
  }

  function makeFlagSVG() {
    const svg = makeSVG(48, 80, '0 0 48 80');
    [
      svgEl('rect',{x:3,y:0,width:3,height:80,fill:'#888'}),
      // stripes
      ...[0,1,2,3,4,5,6,7,8,9,10,11,12].map((i) =>
        svgEl('rect',{x:6,y:i*3,width:39,height:3,fill: i%2===0 ? '#b22234' : 'white'})
      ),
      // canton
      svgEl('rect',{x:6,y:0,width:17,height:20,fill:'#3c3b6e'}),
    ].forEach(p => svg.appendChild(p));
    // stars (simplified)
    [[9,4],[13,4],[17,4],[11,8],[15,8],[9,12],[13,12],[17,12],[11,16],[15,16]].forEach(([x,y])=>
      svg.appendChild(svgEl('circle',{cx:x,cy:y,r:1.5,fill:'white'}))
    );
    return svg;
  }

  function makeBuntingSVG() {
    const colors = ['#b22234','white','#3c3b6e'];
    const w = window.innerWidth;
    const svg = makeSVG(w, 40, `0 0 ${w} 40`);
    svg.appendChild(svgEl('line',{x1:0,y1:5,x2:w,y2:5,stroke:'#888','stroke-width':1.5}));
    for (let x = 20; x < w; x += 55) {
      svg.appendChild(svgEl('polygon',{
        points:`${x},5 ${x+20},5 ${x+10},35`,
        fill:pick(colors), stroke:'rgba(0,0,0,0.2)', 'stroke-width':0.5
      }));
    }
    return svg;
  }

  /* ─── Scenery helpers ─────────────────────────────────────────── */
  function appendGrass(layer, c1, c2) {
    const d = div({ position:'absolute', bottom:0, left:0, right:0, height:'56px', zIndex:'1' });
    const svg = makeSVG('100%', 56, '0 0 1440 56');
    svg.setAttribute('preserveAspectRatio','none');
    svg.appendChild(svgEl('path',{
      d:'M0,56 L0,32 Q10,8 20,28 Q30,48 40,26 Q50,4 60,24 Q70,44 80,22 Q90,0 100,20 Q110,40 120,18 Q130,0 145,22 Q160,44 175,20 Q190,0 205,24 Q220,48 235,22 Q250,0 265,20 Q280,40 295,18 Q310,0 330,22 Q350,44 370,20 Q390,0 410,22 Q430,44 450,20 Q470,0 490,22 Q510,44 530,18 Q550,0 575,22 Q600,44 625,20 Q650,0 675,22 Q700,44 725,18 Q750,0 775,22 Q800,44 825,20 Q850,0 875,22 Q900,44 925,18 Q950,0 980,22 Q1010,44 1040,20 Q1070,0 1100,22 Q1130,44 1160,18 Q1190,0 1220,22 Q1250,44 1280,20 Q1310,0 1340,22 Q1370,44 1400,18 Q1420,4 1440,20 L1440,56 Z',
      fill: c1
    }));
    svg.appendChild(svgEl('path',{
      d:'M0,56 L0,38 Q15,22 30,36 Q45,50 60,34 Q75,18 90,34 Q105,50 120,34 Q135,18 155,36 Q175,54 195,36 Q215,18 235,34 Q255,50 275,34 Q295,18 320,36 Q345,54 370,36 Q395,18 420,36 Q445,54 470,36 Q495,18 525,36 Q555,54 585,36 Q615,18 645,36 Q675,54 705,34 Q735,14 765,34 Q795,54 825,34 Q855,14 890,36 Q925,58 960,36 Q995,14 1030,34 Q1065,54 1100,34 Q1135,14 1170,36 Q1205,58 1240,36 Q1275,14 1310,34 Q1345,54 1380,36 Q1410,22 1440,34 L1440,56 Z',
      fill: c2, opacity: 0.8
    }));
    d.appendChild(svg);
    layer.appendChild(d);
  }

  function appendSnowGround(layer) {
    const d = div({ position:'absolute', bottom:0, left:0, right:0, height:'60px', zIndex:'1' });
    const svg = makeSVG('100%', 60, '0 0 1440 60');
    svg.setAttribute('preserveAspectRatio','none');
    svg.appendChild(svgEl('path',{
      d:'M0,60 L0,30 Q40,10 80,28 Q120,46 160,22 Q200,0 240,20 Q280,40 320,18 Q360,0 400,22 Q440,44 480,20 Q520,0 560,22 Q600,44 640,20 Q680,0 720,22 Q760,44 800,18 Q840,0 880,22 Q920,44 960,20 Q1000,0 1040,22 Q1080,44 1120,18 Q1160,0 1200,20 Q1240,40 1280,18 Q1320,0 1360,22 Q1400,40 1440,20 L1440,60 Z',
      fill:'#e8f0f8'
    }));
    svg.appendChild(svgEl('path',{
      d:'M0,60 L0,42 Q30,30 60,42 Q90,54 120,40 Q150,28 180,40 Q210,52 240,38 Q270,26 300,40 Q330,52 360,38 Q390,26 420,40 Q450,52 480,38 Q510,26 540,40 Q570,52 600,38 Q630,26 660,40 Q690,52 720,36 Q750,22 780,38 Q810,52 840,36 Q870,22 900,38 Q930,52 960,36 Q990,22 1020,38 Q1050,52 1080,38 Q1110,26 1140,38 Q1170,50 1200,38 Q1230,26 1260,38 Q1290,50 1320,38 Q1350,26 1380,38 Q1410,50 1440,38 L1440,60 Z',
      fill:'white', opacity:0.85
    }));
    d.appendChild(svg);
    layer.appendChild(d);
  }

  /* ═══════════════════════════════════════════════════════════════
     GLOBAL CSS KEYFRAMES (injected once)
     ═══════════════════════════════════════════════════════════════ */
  injectCSS('seasonal-bg-keyframes', `
    #seasonal-layer {
      position: fixed; inset: 0;
      pointer-events: none;
      z-index: -1;
      overflow: hidden;
    }
    @keyframes sbDriftCloud  { from{transform:translateX(-260px)} to{transform:translateX(calc(100vw + 260px))} }
    @keyframes sbEggBob      { from{transform:rotate(-3deg) translateY(0)} to{transform:rotate(3deg) translateY(-5px)} }
    @keyframes sbPetalFall   { 0%{transform:translateY(0) rotate(0deg) translateX(0);opacity:.7} 50%{transform:translateY(45vh) rotate(180deg) translateX(30px)} 100%{transform:translateY(105vh) rotate(360deg) translateX(-20px);opacity:0} }
    @keyframes sbBunnyHop    { 0%{transform:translateX(0) scaleX(1);bottom:34px} 4%{bottom:60px} 8%{bottom:34px} 12%{bottom:54px} 16%{bottom:34px} 50%{transform:translateX(var(--hop-dist)) scaleX(1);bottom:34px} 50.1%{transform:translateX(var(--hop-dist)) scaleX(-1)} 54%{bottom:60px} 58%{bottom:34px} 62%{bottom:54px} 66%{bottom:34px} 100%{transform:translateX(0) scaleX(-1);bottom:34px} }
    @keyframes sbButterfly   { 0%{transform:translate(0,0)} 25%{transform:translate(18px,-22px)} 50%{transform:translate(35px,5px)} 75%{transform:translate(10px,20px)} 100%{transform:translate(0,0)} }
    @keyframes sbSnowFall    { 0%{transform:translateY(0) translateX(0) rotate(0deg);opacity:1} 50%{transform:translateY(50vh) translateX(25px) rotate(180deg)} 100%{transform:translateY(105vh) translateX(-15px) rotate(360deg);opacity:0} }
    @keyframes sbTwinkle     { from{opacity:.2} to{opacity:1} }
    @keyframes sbBatFly      { 0%{transform:translateX(0) scaleY(1)} 5%{transform:translateX(5vw) scaleY(0.7)} 10%{transform:translateX(10vw) scaleY(1)} 15%{transform:translateX(15vw) scaleY(0.7)} to{transform:translateX(110vw) scaleY(1)} }
    @keyframes sbGhostFloat  { 0%{transform:translate(0,0) rotate(-3deg)} 33%{transform:translate(12px,-18px) rotate(3deg)} 66%{transform:translate(-8px,-8px) rotate(-2deg)} 100%{transform:translate(0,0) rotate(-3deg)} }
    @keyframes sbFlyAcross   { from{transform:translateX(0)} to{transform:translateX(calc(100vw + 280px))} }
    @keyframes sbFirework    { 0%{transform:scale(0.1);opacity:0} 20%{opacity:1} 60%{transform:scale(1.2);opacity:0.9} 100%{transform:scale(1.5);opacity:0} }
    @keyframes sbFlagWave    { from{transform:rotate(-4deg)} to{transform:rotate(4deg)} }
    .sbWingL { animation: sbWingFlapL 0.3s ease-in-out infinite alternate; transform-origin: right center; }
    .sbWingR { animation: sbWingFlapR 0.3s ease-in-out infinite alternate; transform-origin: left center; }
    @keyframes sbWingFlapL   { from{transform:scaleX(1)} to{transform:scaleX(0.2)} }
    @keyframes sbWingFlapR   { from{transform:scaleX(-1)} to{transform:scaleX(-0.2)} }
    @media (prefers-reduced-motion: reduce) {
      #seasonal-layer * { animation: none !important; }
    }
  `);

  /* ═══════════════════════════════════════════════════════════════
     MAIN — find active season and render
     ═══════════════════════════════════════════════════════════════ */
  function init() {
    const layer = document.getElementById('seasonal-layer');
    if (!layer) return;

    // Find highest-priority active season
    const active = SEASONS
      .filter(s => s.enabled && dateInRange(s.start, s.end))
      .sort((a, b) => b.priority - a.priority)[0];

    if (!active) return; // no theme today

    console.log(`[SeasonalBG] Rendering: ${active.name} (intensity ${active.intensity})`);

    const renderers = {
      easter:       renderEaster,
      christmas:    renderChristmas,
      halloween:    renderHalloween,
      stpatricks:   renderStPatricks,
      independence: renderIndependence,
      valentines:   renderValentines,
      newyear:      renderNewYear,
    };

    const fn = renderers[active.id];
    if (fn) fn(layer, active.intensity);
  }

  // Run after DOM is ready
  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', init);
  } else {
    init();
  }

})();
