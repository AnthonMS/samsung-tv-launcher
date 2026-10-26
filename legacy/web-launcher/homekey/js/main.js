// Home key trampoline: the remote's Home button is mapped to this app. It opens the launcher
// with an ordinary launch and closes itself.
// Why: the TV's own Home relaunch (tvctx-manager, __NO_RAISE_WINDOW) left the launcher behind
// its loading screen, whether the launcher had been closed or only paused, while any ordinary
// launch (app row, PC, a second Home) brought it straight up (2026-10-23). Closing itself
// means every Home press starts this app fresh, so this code always runs.
'use strict';

const LAUNCHER_ID = 'HDvLaunchr.Home';
const LOG_SERVER = 'http://192.0.2.10:8766/log';   // Tools/samsung-tv/logserver.mjs
const T0 = Date.now();

function log(msg) {
  const line = new Date().toLocaleTimeString('en-GB') + ' [homekey] ' + msg;
  console.log(line);
  fetch(LOG_SERVER, { method: 'POST', mode: 'no-cors', body: line }).catch(() => {});
}

function exitNow() {
  try { tizen.application.getCurrentApplication().exit(); } catch (e) { /* already exiting */ }
}

// No timer: the TV pauses this page once the launcher covers it, before a timer would fire,
// and a paused trampoline would get the same stuck Home relaunch as the launcher did.
function exitSoon(ms) {
  if (!ms) { exitNow(); return; }
  setTimeout(exitNow, ms);
}

function openLauncher(why) {
  if (typeof tizen === 'undefined') return;
  let data = {};
  try {
    const req = tizen.application.getCurrentApplication().getRequestedAppControl();
    ((req && req.appControl && req.appControl.data) || []).forEach((d) => { data[d.key] = d.value && d.value[0]; });
    log(`${why} by ${(req && req.callerAppId) || 'n/a'}: ${JSON.stringify(data)}`);
  } catch (e) { log(why); }
  try {
    tizen.application.launch(LAUNCHER_ID, () => {
      log(`launcher opened after ${Date.now() - T0} ms, closing`);
      exitSoon(0);
    }, (e) => {
      log(`launcher did not open: ${e.name} ${e.message}`);
      exitSoon(1000);
    });
  } catch (e) {
    log('launch threw: ' + e.message);
    exitSoon(1000);
  }
}

// In case the TV hands a second Home press to this app before it has closed.
window.addEventListener('appcontrol', () => openLauncher('launched again'));

openLauncher('start');
