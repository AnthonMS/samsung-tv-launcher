// Store apps only report "visible" in the TV API under their Smart Hub number, not their
// Tizen ID. launcher-deploy.ps1 regenerates this from `sdb shell 0 vd_applist` on every
// install; this copy is the 2026-10-20 baseline so the source also works on its own.
const STORE_IDS = {
  '9Ur5IzDKqV.TizenYouTube': '111299001912',
  'com.samsung.tv.bixby-promotion': '3202205027543',
  'org.tizen.browser': '3202010022079',
  'rJeHak5zRg.Spotify': '3201606009684',
  '25emanual2.emanual': '20252100002',
  'Jz7aUrV2Q4.Twitch': '3202203026841',
};
