// TV launcher UI: Continue watching, YouTube, Apps and Live TV rows, a player, settings and
// arrange mode. Layout and settings are saved in localStorage on the TV.
'use strict';

const KEYS = { LEFT: 37, UP: 38, RIGHT: 39, DOWN: 40, ENTER: 13, BACK: 10009, EXIT: 10182, CH_UP: 427, CH_DOWN: 428, BLUE: 406, INFO: 457, ZERO: 48 };
const HAS_TIZEN = typeof tizen !== 'undefined';
const SELF_ID = HAS_TIZEN ? tizen.application.getCurrentApplication().appInfo.id : 'HDvLaunchr.Home';

const $ = (id) => document.getElementById(id);

// ---- App catalogue

// Drawn styles for apps we know. Other apps added in Settings get the TV's icon or a letter.
const KNOWN_APPS = {
  '9Ur5IzDKqV.TizenYouTube': { bg: ['#e0291f', '#9e1610'], glyph: 'youtube' },
  'MoonfinApp.moonfin': { bg: ['#5a43b8', '#2f2270'], glyph: 'moonfin' },
  'AprZAARz4r.Jellyfin': { bg: ['#8a4fd0', '#0f86c4'], glyph: 'jellyfin' },
  'MoonLightS.MoonlightWasm': { bg: ['#3a4a5e', '#1b2430'], glyph: 'moonlight' },
  'rJeHak5zRg.Spotify': { bg: ['#1ed760', '#118a3e'], glyph: 'spotify' },
  'Jz7aUrV2Q4.Twitch': { bg: ['#9b5cff', '#5a2bb5'], glyph: 'twitch' },
  'org.tizen.browser': { bg: ['#3d7bd6', '#1f4c94'], glyph: 'browser' },
  'xvvl3S1TT1.TizenTubeStandalone': { bg: ['#3a3a3a', '#121212'], glyph: 'tizentube' },
  'CrKckTV001.KickTV': { bg: ['#1c1c1c', '#050505'], glyph: 'kick' },
};

// Tizen app IDs from `sdb shell 0 vd_applist` (baseline 2026-10-20).
const DEFAULT_APPS = [
  { id: '9Ur5IzDKqV.TizenYouTube', name: 'YouTube' },
  { id: 'MoonfinApp.moonfin', name: 'Moonfin' },
  { id: 'AprZAARz4r.Jellyfin', name: 'Jellyfin' },
  { id: 'MoonLightS.MoonlightWasm', name: 'Moonlight' },
  { id: 'rJeHak5zRg.Spotify', name: 'Spotify' },
  { id: 'Jz7aUrV2Q4.Twitch', name: 'Twitch' },
  { id: 'org.tizen.browser', name: 'Browser' },
];

// Apps that can be told to play a Jellyfin item, matched on the client name they report.
const PLAY_APPS = {
  moonfin: { id: 'MoonfinApp.moonfin', name: 'Moonfin', client: /moonfin/i },
  jellyfin: { id: 'AprZAARz4r.Jellyfin', name: 'Jellyfin', client: /jellyfin/i },
};

// User-visible apps that don't follow the 10-character store prefix.
const EXTRA_VISIBLE = ['org.tizen.browser'];

const PALETTE = [
  ['#5b6b82', '#2c3646'], ['#3d7bd6', '#1f4c94'], ['#2fa36b', '#17603d'], ['#d0573f', '#8a2c1c'],
  ['#8a5cd6', '#4d2c8f'], ['#d49a2a', '#8a5c10'], ['#2a9fa3', '#155c5f'],
];

// 64x64 white symbols; cut-outs use currentColor, which each tile sets to its dark brand colour.
const GLYPHS = {
  youtube: '<rect x="4" y="14" width="56" height="38" rx="12" fill="#fff"/><path d="M27 23.5 43 33 27 42.5z" fill="currentColor"/>',
  tizentube: '<rect x="4" y="14" width="56" height="38" rx="12" fill="#fff"/><path d="M27 23.5 43 33 27 42.5z" fill="#e0291f"/>',
  kick: '<path d="M14 10h12v14h4v-6h6v-8h12v12h-6v6h-6v8h6v6h6v12H36v-8h-6v-6h-4v14H14z" fill="#53fc18"/>',
  moonfin: '<path d="M38 8a24 24 0 1 0 18 40A20 20 0 1 1 38 8z" fill="#fff"/><path d="M41 26l9 16H32z" fill="#fff"/>',
  jellyfin: '<path d="M32 7c3 0 5 2 7 5l17 31c3 6 0 11-7 11H15c-7 0-10-5-7-11L25 12c2-3 4-5 7-5z" fill="#fff"/><path d="M32 27c1.5 0 2.5 1 3.3 2.4l6.5 12c1.4 2.7.2 4.6-2.8 4.6H25c-3 0-4.2-1.9-2.8-4.6l6.5-12C29.5 28 30.5 27 32 27z" fill="currentColor"/>',
  moonlight: '<path d="M14 24h36c6 0 9 4 10 10l2 12c1 6-5 9-9 4l-5-6H16l-5 6c-4 5-10 2-9-4l2-12c1-6 4-10 10-10z" fill="#fff"/><path d="M19 30v4h-4v4h4v4h4v-4h4v-4h-4v-4z" fill="currentColor"/><circle cx="44" cy="33" r="2.6" fill="currentColor"/><circle cx="50" cy="38" r="2.6" fill="currentColor"/>',
  spotify: '<circle cx="32" cy="32" r="27" fill="#fff"/><g fill="none" stroke="currentColor" stroke-linecap="round"><path d="M17 24c10-3.5 21-2.5 30 2.5" stroke-width="5"/><path d="M19.5 33c8-2.5 17-1.8 24 2" stroke-width="4.2"/><path d="M21.5 41c6-1.7 12.5-1.2 17.5 1.4" stroke-width="3.6"/></g>',
  twitch: '<path d="M14 6h44v30L44 50H34l-8 8h-6v-8H8V16z" fill="#fff"/><path d="M28 18h5v14h-5zM41 18h5v14h-5z" fill="currentColor"/>',
  browser: '<circle cx="32" cy="32" r="26" fill="none" stroke="#fff" stroke-width="5"/><ellipse cx="32" cy="32" rx="11" ry="26" fill="none" stroke="#fff" stroke-width="4.5"/><path d="M8 24h48M8 40h48" stroke="#fff" stroke-width="4.5"/>',
  gear: '<g fill="#fff"><rect x="28" y="4" width="8" height="14" rx="2"/><rect x="28" y="46" width="8" height="14" rx="2"/><rect x="4" y="28" width="14" height="8" rx="2"/><rect x="46" y="28" width="14" height="8" rx="2"/>'
    + '<rect x="28" y="4" width="8" height="14" rx="2" transform="rotate(45 32 32)"/><rect x="28" y="46" width="8" height="14" rx="2" transform="rotate(45 32 32)"/>'
    + '<rect x="4" y="28" width="14" height="8" rx="2" transform="rotate(45 32 32)"/><rect x="46" y="28" width="14" height="8" rx="2" transform="rotate(45 32 32)"/>'
    + '<circle cx="32" cy="32" r="18"/></g><circle cx="32" cy="32" r="7.5" fill="currentColor"/>',
};

// ---- Saved layout and settings

const LAYOUT_KEY = 'launcher.layout.v1';
const ROW_NAMES = { resume: 'Continue watching', youtube: 'YouTube', apps: 'Apps', live: 'Live TV' };
const DEFAULT_ROW_ORDER = ['resume', 'youtube', 'apps', 'live'];

function defaultLayout() {
  return {
    apps: DEFAULT_APPS.map((a) => ({ ...a })), channelOrder: [], hiddenChannels: [], hiddenRows: [],
    rowOrder: DEFAULT_ROW_ORDER.slice(),
  };
}

// Keeps a saved row order valid: known rows only, and any new row added at the end.
function cleanRowOrder(order) {
  const valid = (order || []).filter((r, i, a) => DEFAULT_ROW_ORDER.includes(r) && a.indexOf(r) === i);
  return valid.concat(DEFAULT_ROW_ORDER.filter((r) => !valid.includes(r)));
}

function loadLayout() {
  try {
    const l = JSON.parse(localStorage.getItem(LAYOUT_KEY));
    if (l && Array.isArray(l.apps)) {
      return {
        apps: l.apps, channelOrder: l.channelOrder || [], hiddenChannels: l.hiddenChannels || [],
        hiddenRows: l.hiddenRows || [], rowOrder: cleanRowOrder(l.rowOrder),
      };
    }
  } catch (e) { /* fall back to defaults */ }
  return defaultLayout();
}

let layout = loadLayout();

function saveLayout() {
  try { localStorage.setItem(LAYOUT_KEY, JSON.stringify(layout)); } catch (e) { log('layout save failed'); }
}

const setting = (key, fallback) => localStorage.getItem('launcher.' + key) || fallback;
const saveSetting = (key, value) => localStorage.setItem('launcher.' + key, value);

let playVod = setting('playVod', 'moonfin');     // moonfin | jellyfin
let playLive = setting('playLive', 'moonfin');   // moonfin | jellyfin | launcher
let playYt = setting('playYt', 'youtube');       // youtube | tizentube

// ---- Log, kept across restarts so a test can be read back afterwards (Blue or 0 opens it)

const LOG_KEY = 'launcher.log';
let logLines = [];
try { logLines = JSON.parse(localStorage.getItem(LOG_KEY) || '[]'); } catch (e) { logLines = []; }

// For debugging, log lines can also go to logserver.mjs on the PC
// (Settings, "Send log to this PC"). On while the 1.0.x fixes are being checked.
const LOG_SERVER = 'http://192.168.1.10:8766/';
let remoteLog = setting('remoteLog', 'on') === 'on';
let logQueue = [];
let logFlush = null;

function log(msg) {
  const line = new Date().toLocaleTimeString('en-GB') + ' ' + msg;
  console.log(line);
  logLines.push(line);
  if (logLines.length > 150) logLines = logLines.slice(-150);
  try { localStorage.setItem(LOG_KEY, JSON.stringify(logLines)); } catch (e) { /* storage full or blocked */ }
  if (mode === 'debug') renderDebug();
  if (!remoteLog) return;
  logQueue.push(line);
  if (!logFlush) {
    logFlush = setTimeout(() => {
      const body = logQueue.join('\n');
      logQueue = [];
      logFlush = null;
      fetch(LOG_SERVER + 'log', { method: 'POST', mode: 'no-cors', body }).catch(() => {});
    }, 400);
  }
}

function renderDebug() {
  $('debug').textContent = 'Log  (Blue, 0 or Back closes)\n\n'
    + logLines.slice().reverse().join('\n');
}

function toast(msg, ms = 2500) {
  const t = $('toast');
  t.textContent = msg;
  t.hidden = false;
  clearTimeout(toast.timer);
  toast.timer = setTimeout(() => { t.hidden = true; }, ms);
}

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

// What the remote keys currently drive: home | arrange | panel | player | modal | debug
let mode = 'home';

// ---- Rows and focus

const rows = { resume: [], youtube: [], apps: [], live: [], gear: [] };
const focus = { row: 'apps', col: { resume: 0, youtube: 0, apps: 0, live: 0, gear: 0 } };
const GAP = 26;
// Tile widths per row, as in style.css, so moving focus never has to measure the page.
const TILE_W = { resume: 480, youtube: 352, apps: 224, live: 224 };

function makeTile(cls, inner, caption) {
  const el = document.createElement('div');
  el.className = 'tile ' + cls;
  el.innerHTML = `<div class="face">${inner}<span class="gloss"></span></div><div class="caption"></div>`;
  el.querySelector('.caption').textContent = caption;
  return el;
}

// Images decode off the main thread so a row of artwork doesn't stall key presses.
function setImg(img, src) {
  img.decoding = 'async';
  img.src = src;
}

// Section positions, measured once after anything changes the rows and reused per key press.
let sectionTops = null;

function measureSections() {
  sectionTops = { contentH: $('rows').offsetHeight, boxH: $('home').clientHeight };
  layout.rowOrder.forEach((r) => { sectionTops[r] = $('sec-' + r).offsetTop; });
}

const rowsChanged = () => { sectionTops = null; };

function rowShown(r) {
  return r === 'gear' || (!$('sec-' + r).hidden && rows[r].length > 0);
}

function navOrder() {
  return layout.rowOrder.filter(rowShown).concat('gear');
}

// Puts the row sections on the page in the saved order.
function applyRowOrder() {
  layout.rowOrder.forEach((r) => $('rows').appendChild($('sec-' + r)));
  rowsChanged();
}

function setSectionVisible(r, on) {
  const hide = !on || layout.hiddenRows.includes(r);
  if ($('sec-' + r).hidden !== hide) { $('sec-' + r).hidden = hide; rowsChanged(); }
}

let focusedEl = null;
const lastShift = {};   // last transform written per track, so unchanged ones aren't rewritten

function shift(id, value) {
  if (lastShift[id] === value) return;
  lastShift[id] = value;
  $(id).style.transform = value;
}

function setFocus(row, col) {
  const list = rows[row];
  if (!list || !list.length) return;
  col = Math.max(0, Math.min(col, list.length - 1));
  const el = list[col];
  if (focusedEl && focusedEl !== el) focusedEl.classList.remove('focused');
  el.classList.add('focused');
  focusedEl = el;
  focus.row = row;
  focus.col[row] = col;
  if (row === 'gear') return;
  // Scroll the row sideways so the focused tile stays on screen.
  const step = TILE_W[row] + GAP;
  const visible = Math.floor((1920 - 2 * 104) / step);
  const first = Math.max(0, Math.min(col - (visible - 2), list.length - visible));
  shift(row, `translateX(${-first * step}px)`);
  // And the whole page up or down so the focused row is in view.
  if (!sectionTops) measureSections();
  const y = navOrder()[0] === row ? 0
    : Math.max(0, Math.min(sectionTops[row] - 30, sectionTops.contentH - sectionTops.boxH));
  shift('rows', `translateY(${-y}px)`);
}

function refocus() {
  if (!rowShown(focus.row)) focus.row = navOrder()[0];
  setFocus(focus.row, focus.col[focus.row]);
}

// Arrow keys between and along rows. Returns false for keys it doesn't handle.
function navigate(code) {
  const order = navOrder();
  const i = order.indexOf(focus.row);
  const c = focus.col[focus.row];
  if (code === KEYS.LEFT) { if (focus.row !== 'gear') setFocus(focus.row, c - 1); }
  else if (code === KEYS.RIGHT) { if (focus.row !== 'gear') setFocus(focus.row, c + 1); }
  else if (code === KEYS.DOWN) { if (i < order.length - 1) setFocus(order[i + 1], focus.col[order[i + 1]]); }
  else if (code === KEYS.UP) { if (i > 0) setFocus(order[i - 1], focus.col[order[i - 1]]); }
  else return false;
  return true;
}

// ---- Apps row

function appStyle(app) {
  if (KNOWN_APPS[app.id]) return KNOWN_APPS[app.id];
  let h = 0;
  for (let i = 0; i < app.id.length; i++) h = (h * 31 + app.id.charCodeAt(i)) >>> 0;
  return { bg: PALETTE[h % PALETTE.length], glyph: null };
}

function renderApps() {
  const track = $('apps');
  track.innerHTML = '';
  rows.apps = [];
  layout.apps.forEach((app) => {
    const st = appStyle(app);
    const inner = st.glyph
      ? `<svg class="glyph" viewBox="0 0 64 64" aria-hidden="true">${GLYPHS[st.glyph]}</svg>`
      : '<div class="letter"></div>';
    const el = makeTile('app', inner, app.name);
    const face = el.querySelector('.face');
    face.style.background = `linear-gradient(135deg, ${st.bg[0]}, ${st.bg[1]})`;
    face.style.color = st.bg[1];
    if (!st.glyph) {
      el.querySelector('.letter').textContent = (app.name || '?').charAt(0).toUpperCase();
      if (app.icon) {
        const img = new Image();
        img.className = 'appicon';
        img.onload = () => { const l = el.querySelector('.letter'); if (l) l.replaceWith(img); };
        img.src = app.icon.startsWith('/') ? 'file://' + app.icon : app.icon;
      }
    }
    el.onEnter = () => launchApp(app);
    track.appendChild(el);
    rows.apps.push(el);
  });
  setSectionVisible('apps', true);
}

function launchApp(app) {
  log('launch ' + app.id);
  if (!HAS_TIZEN) return;
  try {
    tizen.application.launch(app.id, () => log('launched ' + app.id), (e) => {
      log(`launch failed ${app.id}: ${e.name} ${e.message}`);
      toast('Could not open ' + app.name);
    });
  } catch (e) {
    log('launch threw: ' + e.message);
  }
}

// Visible apps on the TV, plus anything already on the home screen so it can be removed.
function getInstalledApps(cb) {
  const withLayout = (list) => {
    const ids = new Set(list.map((a) => a.id));
    layout.apps.forEach((a) => { if (!ids.has(a.id)) list.push({ id: a.id, name: a.name + ' (not installed)', icon: '' }); });
    cb(list.sort((a, b) => a.name.localeCompare(b.name)));
  };
  if (!HAS_TIZEN) { withLayout(DEFAULT_APPS.map((a) => ({ ...a, icon: '' }))); return; }
  try {
    tizen.application.getAppsInfo((infos) => {
      const list = infos
        .filter((i) => i.show && !i.id.startsWith('HDvLaunchr.') && (/^[A-Za-z0-9]{10}\./.test(i.id) || EXTRA_VISIBLE.includes(i.id)))
        .map((i) => ({ id: i.id, name: i.name || i.id, icon: i.iconPath || '' }));
      log('installed apps: ' + list.map((a) => a.id).join(', '));
      withLayout(list);
    }, (e) => { log('getAppsInfo failed: ' + e.message); withLayout([]); });
  } catch (e) {
    log('getAppsInfo error: ' + e.message);
    withLayout([]);
  }
}

// ---- Handing Jellyfin items to Moonfin or the Jellyfin app

let tvIp = '';

async function getTvIp() {
  if (tvIp || !HAS_TIZEN) return tvIp;
  try {
    const j = await getJson(TV_API);
    tvIp = (j && j.device && j.device.ip) || '';
  } catch (e) { /* matching falls back to the device name */ }
  return tvIp;
}

// Opens the chosen app and asks it, through the Jellyfin server, to play the item.
async function playInApp(target, itemId, startTicks, label) {
  const app = PLAY_APPS[target];
  log(`play ${label} in ${app.name}`);
  launchApp({ id: app.id, name: app.name });
  const ok = await jfPlayOnClient(app.client, itemId, startTicks, await getTvIp(), log);
  if (!ok) log(`${app.name} did not show up on the Jellyfin server within 25 s`);
}

// ---- Continue watching row

let resumeItems = [];
let resumeState = 'signedOut';   // signedOut | error | ok

function fmtTime(ticks) {
  const s = Math.floor(ticks / 1e7);
  const h = Math.floor(s / 3600);
  const m = Math.floor((s % 3600) / 60);
  const sec = s % 60;
  const pad = (n) => String(n).padStart(2, '0');
  return h ? `${h}:${pad(m)}:${pad(sec)}` : `${m}:${pad(sec)}`;
}

function addBigAction(text, onEnter) {
  const el = makeTile('big action', '<div class="text"></div>', '');
  el.querySelector('.text').textContent = text;
  el.onEnter = onEnter;
  $('resume').appendChild(el);
  rows.resume.push(el);
}

function renderResume() {
  $('resume').innerHTML = '';
  rows.resume = [];
  if (resumeState === 'signedOut') addBigAction('Connect Jellyfin to see what you were watching', startQuickConnect);
  else if (resumeState === 'error') addBigAction('Jellyfin unavailable. OK to retry', loadJellyfin);
  resumeItems.forEach((it) => {
    const el = makeTile('big', (it.image ? '<img class="art">' : '') + '<span class="shade"></span>'
      + '<div class="info"><div class="title"></div><div class="sub"></div><div class="meta"></div></div>', it.title);
    if (it.image) setImg(el.querySelector('.art'), it.image);
    el.querySelector('.title').textContent = it.title;
    el.querySelector('.sub').textContent = it.subtitle;
    const meta = el.querySelector('.meta');
    if (it.positionTicks && it.runtimeTicks) {
      const pct = Math.min(100, Math.round((it.positionTicks / it.runtimeTicks) * 100));
      meta.innerHTML = `<span class="bar"><span style="width:${pct}%"></span></span><span class="time"></span>`;
      meta.querySelector('.time').textContent = `${fmtTime(it.positionTicks)} / ${fmtTime(it.runtimeTicks)}`;
    } else {
      meta.innerHTML = '<span class="badge">Next up</span><span class="time"></span>';
      if (it.runtimeTicks) meta.querySelector('.time').textContent = `${Math.round(it.runtimeTicks / 6e8)} min`;
    }
    el.onEnter = () => playInApp(playVod, it.id, it.positionTicks, it.title);
    $('resume').appendChild(el);
    rows.resume.push(el);
  });
  setSectionVisible('resume', rows.resume.length > 0);
}

// ---- YouTube row (Recommended, see js/youtube.js)

let ytItems = [];
let ytState = ytSignedIn() ? 'loading' : 'signedOut';   // signedOut | loading | error | empty | ok
let ytLoadedAt = 0;
let ytShown = '';   // what the row currently shows, so an unchanged refresh doesn't rebuild it

function addYtAction(text, onEnter) {
  const el = makeTile('video action', '<div class="text"></div>', '');
  el.querySelector('.text').textContent = text;
  el.onEnter = onEnter;
  $('youtube').appendChild(el);
  rows.youtube.push(el);
}

function renderYouTube() {
  $('youtube').innerHTML = '';
  rows.youtube = [];
  if (ytState === 'signedOut') addYtAction('Sign in to YouTube to see your recommendations', startYouTubeSignIn);
  else if (ytState === 'error') addYtAction('YouTube unavailable. OK to retry', loadYouTube);
  else if (ytState === 'empty') addYtAction('No recommendations came back. OK to retry', loadYouTube);
  ytItems.forEach((v) => {
    const el = makeTile('video', '<img class="art"><span class="shade"></span><span class="dur"></span>'
      + '<div class="info"><div class="title"></div><div class="sub"></div></div>', v.title);
    setImg(el.querySelector('.art'), v.thumb);
    el.querySelector('.title').textContent = v.title;
    el.querySelector('.sub').textContent = v.channel;
    const dur = el.querySelector('.dur');
    if (v.duration) dur.textContent = v.duration; else dur.remove();
    el.onEnter = () => playYouTube(v);
    $('youtube').appendChild(el);
    rows.youtube.push(el);
  });
  setSectionVisible('youtube', rows.youtube.length > 0);
}

async function loadYouTube() {
  if (!ytSignedIn()) { ytState = 'signedOut'; ytItems = []; renderYouTube(); return; }
  try {
    ytItems = await ytRecommended();
    ytState = ytItems.length ? 'ok' : 'empty';
    ytLoadedAt = Date.now();
    log('youtube: ' + ytItems.length + ' recommended');
  } catch (e) {
    log('youtube failed: ' + e.message);
    ytState = ytSignedIn() ? 'error' : 'signedOut';
    ytItems = [];
  }
  const sig = ytState + JSON.stringify(ytItems.map((v) => v.id));
  if (sig === ytShown) return;   // nothing new: keep the row as it is
  ytShown = sig;
  renderYouTube();
  if (mode === 'home') refocus();
}

// A cold-started YouTube (or TizenTube) drops the video it was opened with, while a running one
// plays it straight away (both seen 2026-10-21). So after a cold start, wait until it shows and
// has had time to load, then send the video once more.
const YT_LOAD_MS = 5000;
let resendTimer = null;

function resendWhenLoaded(v, target) {
  clearInterval(resendTimer);
  const app = YT_PLAYERS[target];
  const t0 = Date.now();
  // Either ID can be the one reporting visible: with TizenTube installed the TV reports
  // YouTube's screen as TizenTube visible (measured 2026-10-21).
  const isVisible = async () => {
    try { return (await sampleTv(YT_PLAYER_IDS)).visible.length > 0; } catch (e) { return false; }
  };
  // YouTube covers us a second or so after the request; only stop once we're back in front
  // after that (2026-10-21: checking too early cancelled the resend before YouTube showed).
  let covered = false;
  resendTimer = setInterval(async () => {
    if (document.hidden) covered = true;
    if ((covered && !document.hidden) || Date.now() - t0 > 30000) { clearInterval(resendTimer); return; }
    if (!covered || !(await isVisible())) return;
    clearInterval(resendTimer);
    setTimeout(async () => {
      // Home pressed in the meantime: don't pull the app back to the front.
      if (!document.hidden || !(await isVisible())) return;
      log(`${app.name} has loaded, sending ${v.id} again`);
      ytPlay(v.id, target).catch((e) => log('resend failed: ' + e.message));
    }, YT_LOAD_MS);
  }, 1000);
}

async function playYouTube(v, target = playYt) {
  const app = YT_PLAYERS[target];
  // Only a cold start needs the resend.
  let wasRunning = false;
  try { wasRunning = (await sampleTv([app.id])).running.includes(app.id); } catch (e) { /* treat as a cold start */ }
  log(`youtube play ${v.id} in ${app.name}${wasRunning ? '' : ' (cold start)'}`);
  ytPlay(v.id, target)
    .then(() => { log(`${app.name} asked to play ${v.id}`); if (!wasRunning) resendWhenLoaded(v, target); })
    .catch((e) => {
      log(`${app.name} play failed: ${e.message}`);
      if (target !== 'youtube') {
        // TizenTube missing or broken: fall back to the official app rather than doing nothing.
        toast(`${app.name} didn't open, playing in YouTube instead`, 3500);
        playYouTube(v, 'youtube');
      } else {
        launchApp(YT_PLAYERS.youtube);
      }
    });
}

let ytCancelled = false;

async function startYouTubeSignIn() {
  ytCancelled = false;
  mode = 'modal';
  const m = $('modal');
  m.innerHTML = '<div>Asking YouTube for a sign-in code...</div>';
  m.hidden = false;
  try {
    const ok = await ytDeviceSignIn((code, url) => {
      m.innerHTML = '<div>On your phone, open</div><div class="url"></div>'
        + '<div>and enter this code with your main YouTube account:</div>'
        + `<div class="code">${code}</div><div class="hint">Back cancels</div>`;
      m.querySelector('.url').textContent = url.replace(/^https?:\/\//, '');
    }, () => ytCancelled);
    m.hidden = true;
    mode = 'home';
    if (ok) { log('youtube sign-in ok'); toast('YouTube connected'); loadYouTube(); }
    else if (!ytCancelled) toast('Code expired, try again');
  } catch (e) {
    m.hidden = true;
    mode = 'home';
    log('youtube sign-in failed: ' + e.message);
    toast('YouTube: ' + e.message);
  }
}

// ---- Live TV row

let allChannels = [];   // as the server lists them
let channels = [];      // visible ones, in the saved order
let liveState = 'signedOut';

function orderChannels() {
  const hidden = new Set(layout.hiddenChannels);
  const pos = new Map(layout.channelOrder.map((id, i) => [id, i]));
  channels = allChannels
    .filter((c) => !hidden.has(c.id))
    .map((c, i) => ({ c, k: pos.has(c.id) ? pos.get(c.id) : 1e6 + i }))
    .sort((a, b) => a.k - b.k)
    .map((x) => x.c);
}

function saveChannelOrder() {
  const visible = channels.map((c) => c.id);
  layout.channelOrder = visible.concat(layout.channelOrder.filter((id) => !visible.includes(id)));
  saveLayout();
}

function addActionTile(text, onEnter) {
  const el = makeTile('action', '<div class="text"></div>', '');
  el.querySelector('.text').textContent = text;
  el.onEnter = onEnter;
  $('live').appendChild(el);
  rows.live.push(el);
}

function addChannelTile(ch, i) {
  const logo = jfLogo(ch);
  const el = makeTile('channel', '<div class="num"></div>' + (logo ? '<img class="logo">' : '<div class="cname"></div>') + '<div class="now"></div>', ch.name);
  el.querySelector('.num').textContent = ch.number;
  if (logo) setImg(el.querySelector('.logo'), logo);
  else el.querySelector('.cname').textContent = ch.name;
  el.querySelector('.now').textContent = ch.program || '';
  el.onEnter = () => {
    if (playLive === 'launcher') playChannel(i);
    else playInApp(playLive, ch.id, 0, ch.name);
  };
  $('live').appendChild(el);
  rows.live.push(el);
}

function renderLive() {
  $('live').innerHTML = '';
  rows.live = [];
  // Signed out, the Continue watching row already offers the Jellyfin sign-in.
  if (liveState === 'error') addActionTile('Live TV unavailable. OK to retry', loadJellyfin);
  else if (liveState === 'empty') addActionTile('No Live TV channels', loadJellyfin);
  else if (liveState === 'ok' && !channels.length) addActionTile('All channels hidden. OK to choose', openChannelPicker);
  else channels.forEach(addChannelTile);
  setSectionVisible('live', rows.live.length > 0);
}

let jfLoading = false;
// What each row currently shows; a refresh only rebuilds a row whose content changed, so
// coming back from an app doesn't redraw artwork right when you start pressing keys.
let resumeShown = '';
let liveShown = '';

async function loadJellyfin() {
  if (jfLoading) return;
  jfLoading = true;
  try {
    if (jfSignedIn()) {
      const [res, live] = await Promise.all([
        jfContinueWatching().then((v) => ({ v }), (e) => ({ e })),
        jfChannels().then((v) => ({ v }), (e) => ({ e })),
      ]);
      if (res.e) { log('continue watching failed: ' + res.e.message); resumeState = jfSignedIn() ? 'error' : 'signedOut'; resumeItems = []; }
      else { resumeState = 'ok'; resumeItems = res.v; }
      if (live.e) { log('live load failed: ' + live.e.message); liveState = jfSignedIn() ? 'error' : 'signedOut'; }
      else { allChannels = live.v; liveState = allChannels.length ? 'ok' : 'empty'; }
      log(`jellyfin: ${resumeItems.length} to continue, ${allChannels.length} channels`);
    } else {
      resumeState = 'signedOut';
      liveState = 'signedOut';
      resumeItems = [];
      allChannels = [];
    }
    orderChannels();
    if (mode === 'arrange' && arrangePicked) return; // don't rebuild under a tile being moved
    const resumeSig = resumeState + JSON.stringify(resumeItems.map((it) => [it.id, it.positionTicks, it.image]));
    const liveSig = liveState + JSON.stringify(channels.map((c) => [c.id, c.program, c.tag]));
    let changed = false;
    if (resumeSig !== resumeShown) { resumeShown = resumeSig; renderResume(); changed = true; }
    if (liveSig !== liveShown) { liveShown = liveSig; renderLive(); changed = true; }
    if (changed && (mode === 'home' || mode === 'arrange')) refocus();
  } finally {
    jfLoading = false;
  }
}

let qcCancelled = false;

async function startQuickConnect() {
  qcCancelled = false;
  mode = 'modal';
  const m = $('modal');
  m.innerHTML = '<div>Connecting to Jellyfin...</div>';
  m.hidden = false;
  try {
    const ok = await jfQuickConnect((code) => {
      m.innerHTML = '<div>In Jellyfin, open your profile and choose Quick Connect. Enter:</div>'
        + `<div class="code">${code}</div><div class="hint">Back cancels</div>`;
    }, () => qcCancelled);
    m.hidden = true;
    mode = 'home';
    if (ok) {
      log('quick connect ok');
      toast('Jellyfin connected');
      loadJellyfin();
    } else if (!qcCancelled) {
      toast('Code expired, try again');
    }
  } catch (e) {
    m.hidden = true;
    mode = 'home';
    log('quick connect failed: ' + e.message);
    toast('Jellyfin: ' + e.message);
  }
}

// ---- Built-in live TV player (used when "Play live TV in" is set to Launcher)

let current = null;   // stream session on screen
let playIndex = -1;
let zapSeq = 0;
let osdTimer = null;

function showOsd(ch) {
  const o = $('osd');
  o.innerHTML = '<div class="ch"></div><div class="prog"></div>';
  o.querySelector('.ch').textContent = `${ch.number}  ${ch.name}`;
  o.querySelector('.prog').textContent = ch.program || '';
  o.hidden = false;
  clearTimeout(osdTimer);
  osdTimer = setTimeout(() => { o.hidden = true; }, 4000);
}

function stopVideo() {
  const v = $('player');
  v.pause();
  v.removeAttribute('src');
  v.load();
}

async function playChannel(i) {
  if (!channels.length) return;
  i = (i + channels.length) % channels.length;
  playIndex = i;
  mode = 'player';
  const seq = ++zapSeq;
  const ch = channels[i];
  const v = $('player');
  v.hidden = false;
  showOsd(ch);
  stopVideo();
  if (current) { jfStopStream(current); current = null; }
  try {
    const s = await jfOpenStream(ch);
    if (seq !== zapSeq || v.hidden) { jfStopStream(s); return; }
    current = s;
    log('play ' + ch.name);
    v.src = s.url;
    await v.play();
  } catch (e) {
    if (seq !== zapSeq) return;
    log('play failed: ' + e.message);
    toast('Could not play ' + ch.name);
  }
}

function closePlayer() {
  zapSeq++;
  stopVideo();
  $('player').hidden = true;
  $('osd').hidden = true;
  if (current) { jfStopStream(current); current = null; }
  mode = 'home';
  if (playIndex >= 0 && rows.live.length) setFocus('live', playIndex);
}

$('player').addEventListener('error', () => {
  const err = $('player').error;
  if (err) log('video error code ' + err.code);
});

function onPlayerKey(code) {
  if (code === KEYS.BACK) closePlayer();
  else if (code === KEYS.CH_UP || code === KEYS.UP) playChannel(playIndex + 1);
  else if (code === KEYS.CH_DOWN || code === KEYS.DOWN) playChannel(playIndex - 1);
  else if (code === KEYS.ENTER || code === KEYS.INFO) showOsd(channels[playIndex]);
}

// ---- Arrange mode: OK picks a tile up, Left/Right moves it, OK drops, Back undoes the move.
// Only Apps and Live TV can be arranged; the other rows follow Jellyfin and YouTube.

const ARRANGEABLE = ['apps', 'live'];
let arrangePicked = null;  // { row, from }

function startArrange() {
  mode = 'arrange';
  const b = $('banner');
  b.textContent = 'Arrange: OK picks up an app or channel, Left and Right move it, OK puts it down. Back when done.';
  b.hidden = false;
  if (!ARRANGEABLE.includes(focus.row)) focus.row = 'apps';
  refocus();
}

function endArrange() {
  mode = 'home';
  $('banner').hidden = true;
  refocus();
}

function rowList(r) {
  return r === 'apps' ? layout.apps : channels;
}

function rerenderRow(r) {
  if (r === 'apps') renderApps(); else renderLive();
}

function moveTile(r, from, to) {
  const list = rowList(r);
  if (to < 0 || to >= list.length) return;
  const [item] = list.splice(from, 1);
  list.splice(to, 0, item);
  rerenderRow(r);
  rows[r][to].classList.add('picked');
  setFocus(r, to);
}

function dropTile(keep) {
  const { row, from } = arrangePicked;
  const at = focus.col[row];
  if (!keep && at !== from) {
    const list = rowList(row);
    const [item] = list.splice(at, 1);
    list.splice(from, 0, item);
  }
  arrangePicked = null;
  if (row === 'apps') saveLayout(); else saveChannelOrder();
  rerenderRow(row);
  setFocus(row, keep ? at : from);
}

function onArrangeKey(code) {
  const r = focus.row;
  const c = focus.col[r];
  if (!arrangePicked) {
    if (code === KEYS.BACK) { endArrange(); return; }
    if (code === KEYS.ENTER) {
      if (r === 'gear') { endArrange(); return; }
      if (!ARRANGEABLE.includes(r) || (r === 'live' && liveState !== 'ok') || !rows[r][c]) return;
      arrangePicked = { row: r, from: c };
      rows[r][c].classList.add('picked');
      return;
    }
    navigate(code);
    return;
  }
  if (code === KEYS.LEFT) moveTile(r, c, c - 1);
  else if (code === KEYS.RIGHT) moveTile(r, c, c + 1);
  else if (code === KEYS.ENTER) dropTile(true);
  else if (code === KEYS.BACK) dropTile(false);
}

// ---- Side panel used by Settings and the pickers

const panelStack = [];
let panel = null;  // { title, items: [{ label, checked?, run }], sel, hint }

function openPanel(p) {
  if (panel) panelStack.push(panel);
  panel = { sel: 0, ...p };
  mode = 'panel';
  renderPanel();
}

function closePanel() {
  panel = panelStack.pop() || null;
  if (panel) { renderPanel(); return; }
  $('panel').hidden = true;
  mode = 'home';
  refocus();
}

function closeAllPanels() {
  panelStack.length = 0;
  panel = null;
  $('panel').hidden = true;
  mode = 'home';
}

function renderPanel() {
  const el = $('panel');
  el.hidden = false;
  el.querySelector('h3').textContent = panel.title;
  el.querySelector('.hint').textContent = panel.hint || 'OK selects. Back goes back.';
  const box = el.querySelector('.items');
  box.innerHTML = '';
  panel.items.forEach((it, i) => {
    const row = document.createElement('div');
    row.className = 'item' + (i === panel.sel ? ' sel' : '') + (i === panel.sel && panel.moving ? ' moving' : '');
    const label = document.createElement('span');
    label.textContent = typeof it.label === 'function' ? it.label() : it.label;
    row.appendChild(label);
    if ('checked' in it) {
      const c = document.createElement('span');
      c.className = 'check' + (it.checked ? ' on' : '');
      row.appendChild(c);
    }
    box.appendChild(row);
  });
  const ITEM = 86;
  const visible = Math.floor(790 / ITEM);
  const first = Math.max(0, Math.min(panel.sel - (visible - 2), panel.items.length - visible));
  box.style.transform = `translateY(${-first * ITEM}px)`;
}

function onPanelKey(code) {
  // Moving an item (panels with onMove): Up/Down carries it, OK, Back, Left or Right puts it down.
  if (panel.moving) {
    if (code === KEYS.UP || code === KEYS.DOWN) {
      const to = panel.sel + (code === KEYS.UP ? -1 : 1);
      if (to >= 0 && to < panel.items.length) {
        const [it] = panel.items.splice(panel.sel, 1);
        panel.items.splice(to, 0, it);
        panel.sel = to;
        panel.onMove();
      }
    } else if ([KEYS.ENTER, KEYS.BACK, KEYS.LEFT, KEYS.RIGHT].includes(code)) {
      panel.moving = false;
    }
    renderPanel();
    return;
  }
  if (code === KEYS.RIGHT && panel.onMove) { panel.moving = true; renderPanel(); return; }
  if (code === KEYS.UP) panel.sel = Math.max(0, panel.sel - 1);
  else if (code === KEYS.DOWN) panel.sel = Math.min(panel.items.length - 1, panel.sel + 1);
  else if (code === KEYS.BACK) { closePanel(); return; }
  else if (code === KEYS.ENTER) {
    const it = panel.items[panel.sel];
    if (it && it.run) it.run(it);
    if (mode === 'panel' && panel) renderPanel();
    return;
  } else return;
  renderPanel();
}

const PLAY_NAMES = { moonfin: 'Moonfin', jellyfin: 'Jellyfin', launcher: 'Launcher' };
let resetArmed = false;

function openSettings() {
  openPanel({
    title: 'Settings',
    items: [
      { label: 'Arrange apps and channels', run: () => { closeAllPanels(); startArrange(); } },
      { label: 'Add or remove apps', run: openAppPicker },
      { label: 'Show or hide channels', run: openChannelPicker },
      { label: 'Rows on the home screen', run: openRowPicker },
      {
        label: () => `Play shows and films in: ${PLAY_NAMES[playVod]}`,
        run: () => {
          playVod = playVod === 'moonfin' ? 'jellyfin' : 'moonfin';
          saveSetting('playVod', playVod);
        },
      },
      {
        label: () => `Play live TV in: ${PLAY_NAMES[playLive]}`,
        run: () => {
          playLive = { moonfin: 'jellyfin', jellyfin: 'launcher', launcher: 'moonfin' }[playLive];
          saveSetting('playLive', playLive);
        },
      },
      {
        label: () => `Play YouTube videos in: ${YT_PLAYERS[playYt].name}`,
        run: () => {
          playYt = playYt === 'youtube' ? 'tizentube' : 'youtube';
          saveSetting('playYt', playYt);
        },
      },
      {
        label: () => (jfSignedIn() ? 'Sign out of Jellyfin' : 'Connect Jellyfin'),
        run: () => {
          if (jfSignedIn()) { jfSignOut(); loadJellyfin(); toast('Signed out of Jellyfin'); }
          else { closeAllPanels(); startQuickConnect(); }
        },
      },
      {
        label: () => (ytSignedIn() ? 'Sign out of YouTube' : 'Sign in to YouTube'),
        run: () => {
          if (ytSignedIn()) { ytSignOut(); loadYouTube(); toast('Signed out of YouTube'); }
          else { closeAllPanels(); startYouTubeSignIn(); }
        },
      },
      {
        label: () => `Send log to this PC (192.168.1.10): ${remoteLog ? 'On' : 'Off'}`,
        run: () => {
          remoteLog = !remoteLog;
          saveSetting('remoteLog', remoteLog ? 'on' : 'off');
        },
      },
      {
        label: () => (resetArmed ? 'Press OK again to reset' : 'Reset layout'),
        run: () => {
          if (!resetArmed) {
            resetArmed = true;
            setTimeout(() => { resetArmed = false; if (mode === 'panel') renderPanel(); }, 4000);
            return;
          }
          resetArmed = false;
          layout = defaultLayout();
          saveLayout();
          applyRowOrder();
          renderApps();
          orderChannels();
          renderResume();
          renderYouTube();
          renderLive();
          toast('Layout reset');
        },
      },
      { label: 'Show log', run: () => { closeAllPanels(); openDebug(); } },
    ],
  });
}

function openAppPicker() {
  getInstalledApps((apps) => {
    const onHome = new Set(layout.apps.map((a) => a.id));
    openPanel({
      title: 'Apps on the home screen',
      hint: 'OK adds or removes. New apps go to the end of the row.',
      items: apps.map((a) => ({
        label: a.name,
        checked: onHome.has(a.id),
        run: (item) => {
          const idx = layout.apps.findIndex((x) => x.id === a.id);
          if (idx >= 0) layout.apps.splice(idx, 1);
          else layout.apps.push({ id: a.id, name: a.name.replace(/ \(not installed\)$/, ''), icon: a.icon });
          item.checked = idx < 0;
          saveLayout();
          renderApps();
        },
      })),
    });
  });
}

function openChannelPicker() {
  if (!allChannels.length) { toast(jfSignedIn() ? 'No channels loaded yet' : 'Connect Jellyfin first'); return; }
  const hidden = new Set(layout.hiddenChannels);
  const shown = new Set(channels.map((c) => c.id));
  const ordered = channels.concat(allChannels.filter((c) => !shown.has(c.id)));
  openPanel({
    title: 'Channels on the home screen',
    hint: 'OK shows or hides a channel.',
    items: ordered.map((c) => ({
      label: `${c.number ? c.number + '  ' : ''}${c.name}`,
      checked: !hidden.has(c.id),
      run: (item) => {
        const i = layout.hiddenChannels.indexOf(c.id);
        if (i >= 0) layout.hiddenChannels.splice(i, 1); else layout.hiddenChannels.push(c.id);
        item.checked = i >= 0;
        saveLayout();
        orderChannels();
        renderLive();
      },
    })),
  });
}

function openRowPicker() {
  openPanel({
    title: 'Rows on the home screen',
    hint: 'OK shows or hides a row. Right picks a row up, Up and Down move it, OK puts it down.',
    // Moving a row in the list moves it on the home screen.
    onMove: () => {
      layout.rowOrder = panel.items.map((it) => it.key);
      saveLayout();
      applyRowOrder();
    },
    items: layout.rowOrder.map((r) => ({
      key: r,
      label: ROW_NAMES[r],
      checked: !layout.hiddenRows.includes(r),
      run: (item) => {
        const i = layout.hiddenRows.indexOf(r);
        if (i >= 0) layout.hiddenRows.splice(i, 1); else layout.hiddenRows.push(r);
        item.checked = i >= 0;
        saveLayout();
        renderApps();
        renderResume();
        renderYouTube();
        renderLive();
      },
    })),
  });
}

// ---- Debug log overlay

let modeBeforeDebug = 'home';

function openDebug() {
  modeBeforeDebug = mode;
  mode = 'debug';
  $('debug').hidden = false;
  renderDebug();
}

function closeDebug() {
  $('debug').hidden = true;
  mode = modeBeforeDebug === 'debug' ? 'home' : modeBeforeDebug;
  if (mode === 'home') refocus();
}

// ---- Background apps
// The TV never tells apps which app is in front (its status listener stays silent), but
// its own local API on port 8001 reports power state and, per app, running and visible.
// The launcher asks it whether YouTube is running or has started. Measured 2026-10-20: store
// apps (YouTube) report visible only under their Smart Hub number (js/appids.js).

const TV_API = 'http://127.0.0.1:8001/api/v2/';
const apiId = (id) => (typeof STORE_IDS !== 'undefined' && STORE_IDS[id]) || id;

// null for an HTTP error (the TV API says 404 for apps it doesn't know); throws on timeout.
async function getJson(url, ms = 3000) {
  const ctl = new AbortController();
  const timer = setTimeout(() => ctl.abort(), ms);
  try {
    const r = await fetch(url, { signal: ctl.signal });
    return r.ok ? await r.json() : null;
  } finally {
    clearTimeout(timer);
  }
}

// One look at the TV: power, plus running/visible for the given apps. power is null when
// the TV API didn't answer in time; apps that timed out land in `slow` rather than `untracked`.
async function sampleTv(ids) {
  const t0 = Date.now();
  const TIMEOUT = {};
  const [dev, ...apps] = await Promise.all([
    getJson(TV_API).catch(() => TIMEOUT),
    ...ids.map((id) => getJson(TV_API + 'applications/' + encodeURIComponent(apiId(id))).catch(() => TIMEOUT)),
  ]);
  const power = dev && dev !== TIMEOUT && dev.device ? dev.device.PowerState : null;
  const s = { power, on: power === 'on', visible: [], running: [], untracked: [], slow: [], ms: 0 };
  ids.forEach((id, i) => {
    const st = apps[i];
    if (st === TIMEOUT) { s.slow.push(id); return; }
    if (!st) { s.untracked.push(id); return; }
    if (st.visible) s.visible.push(id);
    if (st.running) s.running.push(id);
  });
  s.ms = Date.now() - t0;
  return s;
}

// YouTube and TizenTube: with TizenTube installed the TV reports YouTube's screen as TizenTube.
const YT_PLAYER_IDS = Object.values(YT_PLAYERS).map((p) => p.id);

// Once we're back in front: wait a moment so the first key presses stay smooth, then refresh
// the rows (progress and Next up after watching something).
let settleTimer = null;

function settleInFront() {
  clearTimeout(settleTimer);
  settleTimer = setTimeout(() => {
    if (document.hidden) return;
    if (jfSignedIn()) loadJellyfin();
    if (ytSignedIn() && Date.now() - ytLoadedAt > 10 * 60 * 1000) loadYouTube();
  }, 1500);
}

// ---- Holding Back closes the launcher for good
// The TV only hides an app on a held Back. This remote sends Exit for it (logged 2026-10-23),
// registered in registerKeys so it reaches us; a Back held for a second counts too.

let backDownAt = 0;   // when Back went down, 0 once it's up again
let closing = false;

function closeForGood(why) {
  if (closing || !HAS_TIZEN) return;
  closing = true;
  log(`held Back (${why}): closing the launcher`);
  // A moment first, so the log line above still reaches the PC.
  setTimeout(() => {
    try { tizen.application.getCurrentApplication().exit(); } catch (e) { log('exit failed: ' + e.message); }
  }, 600);
}

// What the page looks like to the TV right now, for the log.
const pageState = () => `hidden=${document.hidden} vis=${document.visibilityState} focus=${document.hasFocus()}`;

document.addEventListener('visibilitychange', () => {
  if (!document.hidden) {
    log('visible (' + pageState() + ')');
    settleInFront();
    return;
  }
  log('hidden');
  clearTimeout(settleTimer);
  if (mode === 'player') closePlayer();
  if (arrangePicked) dropTile(false);
});

// background-support is off (config.xml), so the TV pauses this page while it's hidden; these
// two are only cheap diagnostics.
document.addEventListener('freeze', () => log('page frozen by the TV'));
document.addEventListener('resume', () => log('page resumed by the TV'));

// ---- Keys

function registerKeys() {
  if (!HAS_TIZEN || !tizen.tvinputdevice) return;
  // Exit, in case the remote sends it for a held Back: then it reaches us rather than the TV.
  ['ChannelUp', 'ChannelDown', 'ColorF3Blue', 'Info', '0', 'Exit'].forEach((k) => {
    try { tizen.tvinputdevice.registerKey(k); } catch (e) { log('registerKey ' + k + ' failed'); }
  });
}

function onHomeKey(code) {
  if (navigate(code)) return;
  if (code === KEYS.ENTER) {
    const el = rows[focus.row][focus.col[focus.row]];
    if (el && el.onEnter) el.onEnter();
  }
  // Back on the home screen does nothing: a launcher shouldn't close itself.
}

// Time from a key press to the next drawn frame, logged as an average every 30 presses, so
// responsiveness on the TV can be judged from numbers.
const keyStats = { n: 0, sum: 0, max: 0 };

function measureKey(t0) {
  requestAnimationFrame(() => requestAnimationFrame(() => {
    const dt = performance.now() - t0;
    keyStats.n++;
    keyStats.sum += dt;
    keyStats.max = Math.max(keyStats.max, dt);
    if (keyStats.n === 30) {
      log(`keys: ${Math.round(keyStats.sum / keyStats.n)} ms on average to the next frame, worst ${Math.round(keyStats.max)} ms (30 presses)`);
      keyStats.n = 0;
      keyStats.sum = 0;
      keyStats.max = 0;
    }
  }));
}

let lastMoveAt = 0;
const ARROWS = [KEYS.LEFT, KEYS.RIGHT, KEYS.UP, KEYS.DOWN];

document.addEventListener('keydown', (e) => {
  const code = e.keyCode;
  const t0 = performance.now();
  // Which keys a held Back sends isn't known yet, so log every key except the busy ones.
  if (!e.repeat && !ARROWS.includes(code) && code !== KEYS.ENTER) log('key ' + code);
  if (code === KEYS.EXIT) { closeForGood('Exit key'); return; }
  if (code === KEYS.BACK) {
    if (!e.repeat) backDownAt = Date.now();
    else if (backDownAt && Date.now() - backDownAt > 1000) { closeForGood('held for a second'); return; }
    // A held Back shouldn't close one panel after another.
    if (e.repeat) return;
  }
  // A held arrow key repeats faster than the tiles can animate; keep it to a steady pace.
  if (ARROWS.includes(code) && (mode === 'home' || mode === 'arrange' || mode === 'panel')) {
    if (e.repeat && t0 - lastMoveAt < 110) return;
    lastMoveAt = t0;
    measureKey(t0);
  }
  if (mode === 'debug') {
    if (code === KEYS.BACK || code === KEYS.BLUE || code === KEYS.ZERO) closeDebug();
    return;
  }
  if (code === KEYS.BLUE || code === KEYS.ZERO) { openDebug(); return; }
  if (mode === 'modal') {
    if (code === KEYS.BACK) { qcCancelled = true; ytCancelled = true; $('modal').hidden = true; mode = 'home'; refocus(); }
    return;
  }
  if (mode === 'player') onPlayerKey(code);
  else if (mode === 'panel') onPanelKey(code);
  else if (mode === 'arrange') onArrangeKey(code);
  else onHomeKey(code);
});

document.addEventListener('keyup', (e) => {
  if (e.keyCode !== KEYS.BACK || !backDownAt) return;
  log(`Back up after ${Date.now() - backDownAt} ms`);
  backDownAt = 0;
});

// ---- Start

function buildGear() {
  const g = $('gear');
  g.innerHTML = `<div class="face"><svg class="glyph" viewBox="0 0 64 64" aria-hidden="true">${GLYPHS.gear}</svg><span class="gloss"></span></div><div class="caption">Settings</div>`;
  g.onEnter = openSettings;
  rows.gear = [g];
}

function tickClock() {
  const d = new Date();
  $('clock').textContent = d.toLocaleDateString('en-GB', { weekday: 'long', day: 'numeric', month: 'long' })
    + '   ' + d.toLocaleTimeString('en-GB', { hour: '2-digit', minute: '2-digit' });
}

// Home pressed while the TV had closed us (it does while YouTube runs) starts us with
// __NO_RAISE_WINDOW: the page loads but its window stays behind the loading screen (2026-10-23).
// Launching an app that already runs always brings its window forward, which is why a second
// Home worked. So right after start, launch ourselves once, and again if still not in front.
function raiseSelf(attempt) {
  if (document.hasFocus()) { log(`in front${attempt > 1 ? ' after ' + (attempt - 1) + ' raise(s)' : ''} (${pageState()})`); return; }
  if (attempt > 3) { log(`still not in front after 3 raises (${pageState()})`); return; }
  log(`Home start, not in front (${pageState()}): raising (${attempt})`);
  try {
    tizen.application.launch(SELF_ID, () => setTimeout(() => raiseSelf(attempt + 1), 1000),
      (e) => log('raise failed: ' + e.message));
  } catch (e) { log('raise threw: ' + e.message); }
}

// The TV's loading screen stuck at its end on Home cold starts, with the page running behind it
// (2026-10-23). Rows add ~30 network images; added before the page's load event they hold it up,
// and a slow one holds it for good. So the rows wait for load, and then we ask the TV to drop
// its loading screen with window.screen.show() (Tizen's call for ready_when="custom").
const T0 = Date.now();
const since = () => `${Date.now() - T0} ms`;

function hideLoadingScreen(why) {
  const fn = window.screen && window.screen.show;
  if (typeof fn !== 'function') { log(`screen.show() not available (${why})`); return; }
  try { fn.call(window.screen); log(`screen.show() called (${why}, ${since()}, ${pageState()})`); }
  catch (e) { log(`screen.show() threw (${why}): ${e.message}`); }
}

let rowsStarted = false;

function startRows(why) {
  if (rowsStarted) return;
  rowsStarted = true;
  log(`loading rows (${why}, ${since()})`);
  loadJellyfin();
  loadYouTube();
}

function waitForLoad() {
  document.addEventListener('DOMContentLoaded', () => log(`DOMContentLoaded after ${since()}`));
  const onLoad = () => {
    log(`page loaded after ${since()} (${pageState()})`);
    hideLoadingScreen('load');
    setTimeout(() => hideLoadingScreen('load + 2 s'), 2000);
    setTimeout(() => startRows('after load'), 100);
  };
  if (document.readyState === 'complete') onLoad();
  else window.addEventListener('load', onLoad);
  // Every second until load, so a stuck start shows how far it got; rows start anyway at 8 s.
  const watch = setInterval(() => {
    if (document.readyState === 'complete') { clearInterval(watch); return; }
    log(`not loaded yet at ${since()}: readyState=${document.readyState} (${pageState()})`);
    if (Date.now() - T0 > 8000) { clearInterval(watch); startRows('load never came'); hideLoadingScreen('no load'); }
  }, 1000);
}

window.addEventListener('focus', () => log(`focus at ${since()} (${pageState()})`));
window.addEventListener('blur', () => log(`blur at ${since()} (${pageState()})`));
window.addEventListener('pageshow', () => log(`pageshow at ${since()} (${pageState()})`));
// A launch while we run (a second Home, the app row) arrives as this event instead of a reload.
window.addEventListener('appcontrol', () => {
  try {
    const req = tizen.application.getCurrentApplication().getRequestedAppControl();
    const data = {};
    ((req && req.appControl && req.appControl.data) || []).forEach((d) => { data[d.key] = d.value && d.value[0]; });
    log(`launched again by ${(req && req.callerAppId) || 'n/a'}: ${JSON.stringify(data)} (${pageState()})`);
  } catch (e) { log('launched again'); }
});

function init() {
  log(`start v${JF_VERSION} (${pageState()})`);
  if (HAS_TIZEN) {
    try {
      const req = tizen.application.getCurrentApplication().getRequestedAppControl();
      const data = {};
      ((req && req.appControl && req.appControl.data) || []).forEach((d) => { data[d.key] = d.value && d.value[0]; });
      log(`started by: ${(req && req.callerAppId) || 'n/a'}, ${JSON.stringify(data)}`);
      // A prelaunch in the background (__K_BG_LAUNCH) must stay there.
      if (data.__NO_RAISE_WINDOW === 'enable' && data.__K_BG_LAUNCH !== 'enable') {
        // A window kept behind may never draw a frame, so a timer backs up the frame callback.
        let raised = false;
        const once = () => { if (!raised) { raised = true; raiseSelf(1); } };
        requestAnimationFrame(() => requestAnimationFrame(once));
        setTimeout(once, 500);
      }
    } catch (e) { /* not started through an app control */ }
  }
  registerKeys();
  buildGear();
  applyRowOrder();
  renderApps();
  renderResume();
  renderYouTube();
  renderLive();
  focus.row = 'apps';
  refocus();
  waitForLoad();
  tickClock();
  setInterval(tickClock, 15000);
  // Refresh rows while the home screen is showing: Jellyfin every 5 minutes, YouTube every 30.
  setInterval(() => { if (!document.hidden && mode === 'home' && jfSignedIn()) loadJellyfin(); }, 5 * 60 * 1000);
  setInterval(() => { if (!document.hidden && mode === 'home' && ytSignedIn()) loadYouTube(); }, 30 * 60 * 1000);
}

init();
