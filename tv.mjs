// Living room Samsung TV (QE65Q60C, Tizen 9) from the PC, using only what proved to work:
// the TV's REST API on port 8001 (status, open, close) and DIAL on 8080 for YouTube.
// The WebSocket remote channel is not used: the TV never shows its Allow prompt for new
// devices (tested 2026-10-20), so remote button presses go through Home Assistant instead.
//
//   node tv.mjs info                  device info (power state, model, Developer mode IP)
//   node tv.mjs status                running/visible for the known apps
//   node tv.mjs open <app>            open an app (name below or a Tizen/Smart Hub ID)
//   node tv.mjs close <app>           close an app
//   node tv.mjs youtube <url|id>      open YouTube and play that video
//   node tv.mjs apps                  every user-visible app installed (via sdb)
//
// App names: launcher youtube moonfin jellyfin tizentube kick spotify twitch browser moonlight
// TV_IP env var overrides the default IP.

import { execFileSync } from 'node:child_process';

const IP = process.env.TV_IP || '192.168.1.46';
const API = `http://${IP}:8001/api/v2/`;
const SDB = `${process.env.USERPROFILE}\\tizen-studio\\tools\\sdb.exe`;

// Store apps answer the REST API under their Smart Hub number, sideloaded ones under their Tizen ID.
const APPS = {
  launcher: 'HDvLaunchr.Home',
  youtube: '111299001912',
  moonfin: 'MoonfinApp.moonfin',
  jellyfin: 'AprZAARz4r.Jellyfin',
  tizentube: 'xvvl3S1TT1.TizenTubeStandalone',
  kick: 'CrKckTV001.KickTV',
  spotify: '3201606009684',
  twitch: '3202203026841',
  browser: '3202010022079',
  moonlight: 'MoonLightS.MoonlightWasm',
};

const appId = (name) => APPS[String(name).toLowerCase()] || name;

async function call(path, method = 'GET', opts = {}) {
  const res = await fetch(path.startsWith('http') ? path : API + path, { method, signal: AbortSignal.timeout(6000), ...opts });
  const text = await res.text();
  try { return { status: res.status, body: JSON.parse(text) }; } catch { return { status: res.status, body: text }; }
}

function videoId(input) {
  const m = String(input).match(/(?:v=|youtu\.be\/|\/shorts\/|\/live\/|\/embed\/)([A-Za-z0-9_-]{11})/);
  if (m) return m[1];
  if (/^[A-Za-z0-9_-]{11}$/.test(input)) return input;
  throw new Error(`Not a YouTube URL or video ID: ${input}`);
}

const [cmd, ...args] = process.argv.slice(2);

try {
  switch (cmd) {
    case 'info': {
      const { body } = await call('');
      const d = body.device || {};
      console.log(`${d.name} (${d.modelName}, ${d.model}), power ${d.PowerState}, ${d.networkType} ${d.ip}`);
      console.log(`Developer mode ${d.developerMode === '1' ? 'on' : 'off'}, host PC IP ${d.developerIP}`);
      break;
    }
    case 'status': {
      for (const [name, id] of Object.entries(APPS)) {
        const { status, body } = await call('applications/' + encodeURIComponent(id));
        const state = status !== 200 ? 'not installed' : `${body.running ? 'running' : 'stopped'}${body.visible ? ', in front' : ''}`;
        console.log(`${name.padEnd(10)} ${state}`);
      }
      break;
    }
    case 'open':
    case 'close': {
      const id = appId(args[0]);
      const { status, body } = await call('applications/' + encodeURIComponent(id), cmd === 'open' ? 'POST' : 'DELETE');
      console.log(`${cmd} ${id}: HTTP ${status} ${JSON.stringify(body)}`);
      if (status !== 200) process.exitCode = 1;
      break;
    }
    case 'youtube': {
      const id = videoId(args[0]);
      // DIAL is what the YouTube phone app uses to start playback on a TV. The TV only accepts it
      // with YouTube's own origin (measured 2026-10-20: 201 with it, 403 without), and only from
      // outside the TV: from the TV itself it answers 401.
      const res = await call(`http://${IP}:8080/ws/apps/YouTube`, 'POST', {
        headers: { 'Content-Type': 'text/plain; charset=utf-8', Origin: 'https://www.youtube.com' },
        body: `v=${id}`,
      });
      console.log(`YouTube v=${id}: HTTP ${res.status}${res.status === 201 || res.status === 200 ? ' (playing)' : ''}`);
      if (res.status !== 201 && res.status !== 200) process.exitCode = 1;
      break;
    }
    case 'apps': {
      execFileSync(SDB, ['connect', IP], { stdio: 'ignore' });
      const out = execFileSync(SDB, ['-s', `${IP}:26101`, 'shell', '0', 'vd_applist'], { encoding: 'utf8' });
      for (const blk of out.split(/-{90,}/)) {
        const f = (k) => (blk.match(new RegExp(`-{5,}${k}\\s*=(.*?)-{5,}`)) || [])[1];
        if (f('app_id')) console.log(`${(f('app_title') || '').padEnd(32)} ${f('app_tizen_id')}  (REST: ${f('app_id')})  v${f('app_version')}`);
      }
      break;
    }
    default:
      console.log('Commands: info | status | open <app> | close <app> | youtube <url|id> | apps');
  }
} catch (e) {
  console.error('Error:', e.message);
  process.exitCode = 1;
}
