// Jellyfin Live TV: Quick Connect sign-in, channel list, HLS playback.
// Talks to the server directly over the LAN; the token stays in this app's localStorage.
'use strict';

const JF_VERSION = '1.7.1';

const JF = {
  server: localStorage.getItem('jf.server') || 'http://192.168.1.26:8096',
  token: localStorage.getItem('jf.token') || '',
  userId: localStorage.getItem('jf.userId') || '',
  deviceId: localStorage.getItem('jf.deviceId') || '',
};

if (!JF.deviceId) {
  JF.deviceId = 'tvlauncher-' + Math.random().toString(36).slice(2, 12);
  localStorage.setItem('jf.deviceId', JF.deviceId);
}

// Forces HLS in a TS container; Jellyfin remuxes (no re-encode) when codecs already fit.
const JF_PROFILE = {
  Name: 'TV Launcher',
  MaxStreamingBitrate: 40000000,
  MaxStaticBitrate: 40000000,
  DirectPlayProfiles: [],
  TranscodingProfiles: [{
    Container: 'ts',
    Type: 'Video',
    VideoCodec: 'h264,hevc',
    AudioCodec: 'aac,ac3,eac3,mp3',
    Protocol: 'hls',
    Context: 'Streaming',
    MaxAudioChannels: '6',
    MinSegments: 1,
    BreakOnNonKeyFrames: true,
  }],
  ContainerProfiles: [],
  CodecProfiles: [],
  SubtitleProfiles: [],
};

function jfSignedIn() {
  return Boolean(JF.token && JF.userId);
}

function jfSignOut() {
  JF.token = '';
  JF.userId = '';
  localStorage.removeItem('jf.token');
  localStorage.removeItem('jf.userId');
}

function jfAuthHeader() {
  let h = `MediaBrowser Client="TV Launcher", Device="Samsung Livingroom", DeviceId="${JF.deviceId}", Version="${JF_VERSION}"`;
  if (JF.token) h += `, Token="${JF.token}"`;
  return h;
}

async function jf(path, opts = {}) {
  const res = await fetch(JF.server + path, {
    ...opts,
    headers: { Authorization: jfAuthHeader(), 'Content-Type': 'application/json', ...(opts.headers || {}) },
  });
  if (res.status === 401 && JF.token) {
    jfSignOut();
    throw new Error('Jellyfin sign-in expired');
  }
  if (!res.ok) throw new Error(`Jellyfin HTTP ${res.status} on ${path.split('?')[0]}`);
  const text = await res.text();
  return text ? JSON.parse(text) : null;
}

// Shows a code via onCode, then waits (up to 5 min) for it to be approved in Jellyfin.
async function jfQuickConnect(onCode, isCancelled) {
  const init = await jf('/QuickConnect/Initiate', { method: 'POST' });
  onCode(init.Code);
  const deadline = Date.now() + 5 * 60 * 1000;
  while (Date.now() < deadline && !isCancelled()) {
    await new Promise((r) => setTimeout(r, 3000));
    const st = await jf('/QuickConnect/Connect?secret=' + encodeURIComponent(init.Secret));
    if (st && st.Authenticated) {
      const auth = await jf('/Users/AuthenticateWithQuickConnect', {
        method: 'POST',
        body: JSON.stringify({ Secret: init.Secret }),
      });
      JF.token = auth.AccessToken;
      JF.userId = auth.User.Id;
      localStorage.setItem('jf.token', JF.token);
      localStorage.setItem('jf.userId', JF.userId);
      return true;
    }
  }
  return false;
}

async function jfChannels() {
  const q = `userId=${JF.userId}&enableImages=true&enableImageTypes=Primary&addCurrentProgram=true&enableUserData=false&limit=300`;
  const r = await jf('/LiveTv/Channels?' + q);
  return (r.Items || []).map((c) => ({
    id: c.Id,
    name: c.Name,
    number: c.ChannelNumber || c.Number || '',
    tag: c.ImageTags && c.ImageTags.Primary,
    program: c.CurrentProgram ? c.CurrentProgram.Name : '',
  }));
}

// ---- Continue watching (resume) followed by Next up, as one list

function jfItemImage(it) {
  const s = JF.server;
  const q = 'fillWidth=720&quality=85';
  if (it.ImageTags && it.ImageTags.Thumb) return `${s}/Items/${it.Id}/Images/Thumb?${q}&tag=${it.ImageTags.Thumb}`;
  if (it.ParentThumbItemId && it.ParentThumbImageTag) return `${s}/Items/${it.ParentThumbItemId}/Images/Thumb?${q}&tag=${it.ParentThumbImageTag}`;
  if (it.BackdropImageTags && it.BackdropImageTags.length) return `${s}/Items/${it.Id}/Images/Backdrop?${q}&tag=${it.BackdropImageTags[0]}`;
  if (it.ParentBackdropItemId && it.ParentBackdropImageTags && it.ParentBackdropImageTags.length) {
    return `${s}/Items/${it.ParentBackdropItemId}/Images/Backdrop?${q}&tag=${it.ParentBackdropImageTags[0]}`;
  }
  if (it.ImageTags && it.ImageTags.Primary) return `${s}/Items/${it.Id}/Images/Primary?${q}&tag=${it.ImageTags.Primary}`;
  return '';
}

function jfMapItem(it, resumable) {
  const ud = it.UserData || {};
  const episode = it.Type === 'Episode';
  return {
    id: it.Id,
    title: episode ? (it.SeriesName || it.Name) : it.Name,
    subtitle: episode ? `S${it.ParentIndexNumber || 0}:E${it.IndexNumber || 0}  ${it.Name}` : (it.ProductionYear ? String(it.ProductionYear) : ''),
    image: jfItemImage(it),
    positionTicks: resumable ? (ud.PlaybackPositionTicks || 0) : 0,
    runtimeTicks: it.RunTimeTicks || 0,
    nextUp: !resumable,
  };
}

async function jfContinueWatching() {
  const common = `userId=${JF.userId}&limit=16&enableImageTypes=Primary,Backdrop,Thumb&imageTypeLimit=1&enableUserData=true`;
  const [resume, next] = await Promise.all([
    jf(`/UserItems/Resume?${common}&mediaTypes=Video`),
    jf(`/Shows/NextUp?${common}&enableResumable=false&enableRewatching=false`),
  ]);
  const seen = new Set();
  const seriesResuming = new Set();
  const out = [];
  (resume.Items || []).forEach((it) => {
    seen.add(it.Id);
    if (it.SeriesId) seriesResuming.add(it.SeriesId);
    out.push(jfMapItem(it, true));
  });
  (next.Items || []).forEach((it) => {
    if (seen.has(it.Id) || (it.SeriesId && seriesResuming.has(it.SeriesId))) return;
    out.push(jfMapItem(it, false));
  });
  return out;
}

// ---- Play on another client (Moonfin or the Jellyfin app), like "Play on" in Jellyfin web

// Several devices can run Moonfin, so prefer the session connecting from this TV's IP,
// then one that calls itself a Samsung or Tizen TV.
function jfSessionScore(s, tvIp) {
  return ((tvIp && (s.RemoteEndPoint || '').indexOf(tvIp) === 0) ? 2 : 0)
    + (/samsung|tizen/i.test(`${s.DeviceName} ${s.Client}`) ? 1 : 0);
}

// Waits (up to 25 s) for the client to show up on the server, then sends PlayNow.
async function jfPlayOnClient(clientRe, itemId, startTicks, tvIp, onLog) {
  const deadline = Date.now() + 25000;
  let listed = false;
  let listedAll = false;
  while (Date.now() < deadline) {
    let all = [];
    try {
      const list = await jf(`/Sessions?controllableByUserId=${JF.userId}&activeWithinSeconds=900`);
      if (!listedAll) {
        listedAll = true;
        onLog('all sessions: ' + (list || []).map((s) => `${s.Client} / ${s.DeviceName} / ${s.RemoteEndPoint} / v${s.ApplicationVersion} / remote=${s.SupportsRemoteControl}`).join(' | '));
      }
      all = (list || []).filter((s) => s.DeviceId !== JF.deviceId && s.SupportsRemoteControl && clientRe.test(s.Client || ''));
    } catch (e) {
      onLog('sessions failed: ' + e.message);
    }
    if (all.length && !listed) {
      onLog('sessions: ' + all.map((s) => `${s.Client} / ${s.DeviceName} / ${s.RemoteEndPoint}`).join(' | '));
      listed = true;
    }
    all.sort((a, b) => jfSessionScore(b, tvIp) - jfSessionScore(a, tvIp) || Date.parse(b.LastActivityDate) - Date.parse(a.LastActivityDate));
    const best = all[0];
    if (best && jfSessionScore(best, tvIp) > 0) {
      // A freshly started client needs a moment before it acts on commands.
      await new Promise((r) => setTimeout(r, 1500));
      const q = `playCommand=PlayNow&itemIds=${itemId}` + (startTicks ? `&startPositionTicks=${startTicks}` : '');
      await jf(`/Sessions/${best.Id}/Playing?${q}`, { method: 'POST' });
      onLog(`play sent to ${best.Client} on ${best.DeviceName}`);
      return true;
    }
    await new Promise((r) => setTimeout(r, 1000));
  }
  return false;
}

function jfLogo(ch) {
  return ch.tag ? `${JF.server}/Items/${ch.id}/Images/Primary?fillHeight=180&quality=90&tag=${ch.tag}` : '';
}

function withKey(url) {
  if (/[?&](api_key|ApiKey)=/.test(url)) return url;
  return url + (url.includes('?') ? '&' : '?') + `ApiKey=${JF.token}&api_key=${JF.token}`;
}

// Opens the live stream on the server. Returns a session with an HLS url for <video>;
// pass the same session to jfStopStream to close it.
async function jfOpenStream(ch) {
  const info = await jf(`/Items/${ch.id}/PlaybackInfo?userId=${JF.userId}`, {
    method: 'POST',
    body: JSON.stringify({
      UserId: JF.userId,
      AutoOpenLiveStream: true,
      IsPlayback: true,
      MaxStreamingBitrate: JF_PROFILE.MaxStreamingBitrate,
      DeviceProfile: JF_PROFILE,
    }),
  });
  const src = info.MediaSources && info.MediaSources[0];
  if (!src) throw new Error('No stream for ' + ch.name);
  const url = src.TranscodingUrl
    ? JF.server + src.TranscodingUrl
    : `${JF.server}/Videos/${ch.id}/master.m3u8?MediaSourceId=${src.Id}&LiveStreamId=${src.LiveStreamId || ''}&PlaySessionId=${info.PlaySessionId}&VideoCodec=h264&AudioCodec=aac&DeviceId=${JF.deviceId}`;
  const session = { url: withKey(url), itemId: ch.id, mediaSourceId: src.Id, liveStreamId: src.LiveStreamId, playSessionId: info.PlaySessionId };
  jf('/Sessions/Playing', {
    method: 'POST',
    body: JSON.stringify({
      ItemId: ch.id,
      MediaSourceId: src.Id,
      PlaySessionId: info.PlaySessionId,
      LiveStreamId: src.LiveStreamId,
      PlayMethod: 'Transcode',
      CanSeek: false,
    }),
  }).catch(() => {});
  return session;
}

async function jfStopStream(p) {
  if (!p) return;
  const quiet = (promise) => promise.catch(() => {});
  await quiet(jf('/Sessions/Playing/Stopped', {
    method: 'POST',
    body: JSON.stringify({ ItemId: p.itemId, MediaSourceId: p.mediaSourceId, PlaySessionId: p.playSessionId, LiveStreamId: p.liveStreamId }),
  }));
  if (p.liveStreamId) await quiet(jf('/LiveStreams/Close?liveStreamId=' + encodeURIComponent(p.liveStreamId), { method: 'POST' }));
  await quiet(jf(`/Videos/ActiveEncodings?deviceId=${JF.deviceId}&playSessionId=${p.playSessionId}`, { method: 'DELETE' }));
}
