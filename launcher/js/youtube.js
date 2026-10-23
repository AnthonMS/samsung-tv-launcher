// YouTube Recommended row, through YouTube's own TV interface (the one the YouTube TV app
// uses). Unofficial: Google can change or block it at any time, and then the row shows an
// error tile instead. Sign-in is the device flow a new TV uses: we show a code, you approve
// it on your phone with your YouTube account. The token stays in this app's localStorage.
'use strict';

// YT_CLIENT_ID and YT_CLIENT_SECRET (the YouTube TV web client's public OAuth client) come from
// js/youtube-client.js, which is kept out of git; see youtube-client.example.js.
const YT_SCOPE = 'http://gdata.youtube.com https://www.googleapis.com/auth/youtube-paid-content';
const YT_TV_VERSION = '7.20250929.15.00';
const YT_OAUTH = 'https://www.youtube.com/o/oauth2/';

const YT = { token: null };
try { YT.token = JSON.parse(localStorage.getItem('yt.token') || 'null'); } catch (e) { YT.token = null; }

function ytSignedIn() {
  return Boolean(YT.token && YT.token.refresh);
}

function ytSignOut() {
  YT.token = null;
  localStorage.removeItem('yt.token');
}

function ytSaveToken(t) {
  YT.token = t;
  localStorage.setItem('yt.token', JSON.stringify(t));
}

function ytUuid() {
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (c) => {
    const r = Math.floor(Math.random() * 16);
    return (c === 'x' ? r : (r % 4) + 8).toString(16);
  });
}

async function ytPost(url, body, headers = {}) {
  const res = await fetch(url, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', ...headers },
    body: JSON.stringify(body),
  });
  const text = await res.text();
  let json = null;
  try { json = JSON.parse(text); } catch (e) { /* not JSON */ }
  return { status: res.status, json, text };
}

// Shows a code via onCode(code, url), then waits for it to be approved on the phone.
async function ytDeviceSignIn(onCode, isCancelled) {
  const r = await ytPost(YT_OAUTH + 'device/code', {
    client_id: YT_CLIENT_ID, scope: YT_SCOPE, device_id: ytUuid(), device_model: 'ytlr::',
  });
  if (!r.json || !r.json.user_code) throw new Error(`code request failed (HTTP ${r.status})`);
  const { device_code: deviceCode, user_code: userCode, verification_url: url, interval, expires_in: expiresIn } = r.json;
  onCode(userCode, url || 'https://www.google.com/device');
  const deadline = Date.now() + (expiresIn || 1800) * 1000;
  let wait = Math.max(5, interval || 5) * 1000;
  while (Date.now() < deadline && !isCancelled()) {
    await new Promise((res) => setTimeout(res, wait));
    const t = await ytPost(YT_OAUTH + 'token', {
      client_id: YT_CLIENT_ID, client_secret: YT_CLIENT_SECRET, code: deviceCode,
      grant_type: 'http://oauth.net/grant_type/device/1.0',
    });
    const j = t.json || {};
    if (j.access_token) {
      ytSaveToken({ access: j.access_token, refresh: j.refresh_token, expires: Date.now() + (j.expires_in || 3600) * 1000 });
      return true;
    }
    if (j.error === 'slow_down') wait += 5000;
    else if (j.error && j.error !== 'authorization_pending') throw new Error(j.error);
  }
  return false;
}

async function ytAccessToken() {
  if (!ytSignedIn()) throw new Error('not signed in to YouTube');
  if (YT.token.access && Date.now() < YT.token.expires - 60000) return YT.token.access;
  const r = await ytPost(YT_OAUTH + 'token', {
    client_id: YT_CLIENT_ID, client_secret: YT_CLIENT_SECRET, refresh_token: YT.token.refresh, grant_type: 'refresh_token',
  });
  const j = r.json || {};
  if (!j.access_token) {
    if (j.error === 'invalid_grant') ytSignOut();
    throw new Error('YouTube sign-in refresh failed: ' + (j.error || 'HTTP ' + r.status));
  }
  ytSaveToken({ access: j.access_token, refresh: YT.token.refresh, expires: Date.now() + (j.expires_in || 3600) * 1000 });
  return YT.token.access;
}

async function ytBrowse(browseId) {
  const token = await ytAccessToken();
  const r = await ytPost('https://www.youtube.com/youtubei/v1/browse?prettyPrint=false', {
    context: { client: { clientName: 'TVHTML5', clientVersion: YT_TV_VERSION, hl: 'en', gl: 'DK' } },
    browseId,
  }, { Authorization: 'Bearer ' + token, 'X-YouTube-Client-Name': '7', 'X-YouTube-Client-Version': YT_TV_VERSION });
  if (r.status !== 200 || !r.json) throw new Error(`YouTube browse ${browseId}: HTTP ${r.status}`);
  return r.json;
}

const ytText = (t) => (!t ? '' : (t.simpleText || (t.runs || []).map((x) => x.text).join('')));

function ytBestThumb(thumbs, id) {
  const list = (thumbs || []).filter((t) => t.url).sort((a, b) => (a.width || 0) - (b.width || 0));
  const pick = list.find((t) => (t.width || 0) >= 400) || list[list.length - 1];
  let url = pick ? pick.url : `https://i.ytimg.com/vi/${id}/mqdefault.jpg`;
  if (url.startsWith('//')) url = 'https:' + url;
  return url;
}

// TV layout: tileRenderer with header (thumbnail, duration) and metadata (title, lines).
function ytFromTile(t) {
  if (t.contentType && t.contentType !== 'TILE_CONTENT_TYPE_VIDEO') return null;
  const watch = t.onSelectCommand && t.onSelectCommand.watchEndpoint;
  const id = (watch && watch.videoId) || t.contentId;
  if (!id) return null;
  const head = (t.header && t.header.tileHeaderRenderer) || {};
  const meta = (t.metadata && t.metadata.tileMetadataRenderer) || {};
  const lines = (meta.lines || []).map((l) => ((l.lineRenderer && l.lineRenderer.items) || [])
    .map((i) => ytText(i.lineItemRenderer && i.lineItemRenderer.text)).filter((s) => s && s.trim() !== '•').join(' '));
  const overlay = (head.thumbnailOverlays || []).find((o) => o.thumbnailOverlayTimeStatusRenderer);
  return {
    id,
    title: ytText(meta.title),
    channel: lines[0] || '',
    info: lines[1] || '',
    duration: overlay ? ytText(overlay.thumbnailOverlayTimeStatusRenderer.text) : '',
    thumb: ytBestThumb(head.thumbnail && head.thumbnail.thumbnails, id),
  };
}

// Web-style layouts, in case the TV response uses them.
function ytFromVideoRenderer(v) {
  return {
    id: v.videoId,
    title: ytText(v.title),
    channel: ytText(v.ownerText || v.shortBylineText || v.longBylineText),
    info: ytText(v.shortViewCountText || v.viewCountText),
    duration: ytText(v.lengthText),
    thumb: ytBestThumb(v.thumbnail && v.thumbnail.thumbnails, v.videoId),
  };
}

// Walks the whole response and keeps every video it recognises, in order, without repeats.
function ytCollect(node, out, seen) {
  if (!node || typeof node !== 'object' || out.length >= 30) return;
  if (Array.isArray(node)) { node.forEach((n) => ytCollect(n, out, seen)); return; }
  let v = null;
  if (node.tileRenderer) v = ytFromTile(node.tileRenderer);
  else if (node.videoRenderer || node.gridVideoRenderer || node.compactVideoRenderer) {
    v = ytFromVideoRenderer(node.videoRenderer || node.gridVideoRenderer || node.compactVideoRenderer);
  }
  if (v || node.tileRenderer) {
    if (v && v.id && v.title && !seen.has(v.id)) { seen.add(v.id); out.push(v); }
    return;
  }
  Object.keys(node).forEach((k) => ytCollect(node[k], out, seen));
}

async function ytRecommended(onRaw) {
  const json = await ytBrowse('default');
  if (onRaw) onRaw(json);
  const out = [];
  ytCollect(json, out, new Set());
  return out;
}

// Apps that can play a YouTube video handed over by the launcher.
const YT_PLAYERS = {
  youtube: { id: '9Ur5IzDKqV.TizenYouTube', name: 'YouTube' },
  tizentube: { id: 'xvvl3S1TT1.TizenTubeStandalone', name: 'TizenTube' },
};

// Starts a video in the chosen app. Each takes the video in its own launch-request format:
// - YouTube: PAYLOAD "v=<id>" (measured 2026-10-21: switches YouTube to that video; DIAL,
//   what the phone app uses, is refused from inside the TV with 401 whatever the origin).
// - TizenTube Standalone: its index.html reads JSON {"args": "..."} from the first data item
//   and appends args to the YouTube TV page it loads, so "v=<id>" opens that video.
function ytPlay(id, target = 'youtube') {
  const payload = target === 'tizentube' ? JSON.stringify({ args: 'v=' + id }) : 'v=' + id;
  return new Promise((resolve, reject) => {
    const ac = new tizen.ApplicationControl('http://tizen.org/appcontrol/operation/default', null, null, null,
      [new tizen.ApplicationControlData('PAYLOAD', [payload])]);
    tizen.application.launchAppControl(ac, YT_PLAYERS[target].id, resolve, (e) => reject(new Error(e.message)));
  });
}
