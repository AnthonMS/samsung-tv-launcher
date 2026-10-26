# Samsung TV launcher and privacy setup

My Samsung Q60C now opens a native, customizable home screen directly from Home and at startup. This repository contains the launcher source, setup tools, the commands behind the changes, recovery instructions and the older web-launcher experiment.

The current launcher is a fork of [quiet-tizen](https://github.com/jss8649/quiet-tizen). Its design also carries the upstream Free My TV attribution. This is a documented setup for **one inspected TV**, not a universal installer or a replacement operating system.

## Read this first

| Guide | Purpose |
| --- | --- |
| [Manual installation](docs/MANUAL.md) | Prerequisites, inventory, root transport, backups, build, installation and Home assignment |
| [Media and daily use](docs/MEDIA.md) | Dock editing, clock/theme, YouTube sign-in, Jellyfin Quick Connect and playback |
| [Remote buttons](docs/REMOTE.md) | Scoped permission, four branded buttons, Moonfin guide and channel rocker |
| [Privacy and boot persistence](docs/PRIVACY.md) | Exact 47-component profile, service markers, boot root, token bridge and verification |
| [Recovery](docs/RECOVERY.md) | Undo each layer without replacing live databases with stale copies |
| [Engineering journal](docs/JOURNAL.md) | What changed, what failed, why the final approach works and remaining limits |
| [Command inventory](docs/COMMAND-INDEX.md) | Index of retained operator scripts and recorded outcomes |
| [Architecture](docs/ARCHITECTURE.md) | Files, data flow, runtime choices and maintenance |
| [Personal blog](https://mlgaard.dk/en/blog/byg-din-egen-tv-launcher) | The story and guided overview |

For a new TV, follow the manual in order. Do not start by running every script. A successful root probe does not establish that its package names, APIs, permissions or firmware match this case study.

## Tested device and evidence

- Model: `TQ65Q60CAUXXC`, Q60C family, 2023.
- Displayed firmware: `T-NKLCDEUC-0090-2220.4`. The live `/etc/prd_info.ini` version was also checked.
- Platform: Tizen 9, ARMv7, .NET 6.0.9, build `20260318_182206`.
- Home and startup use the repurposed Bixby Promotion package. The old trampoline is unnecessary.
- Full restart and a separate 30-second standby/remote-wake test passed.
- All eight exclusive remote grabs, launcher account connections, display, networking and audio remained available.
- Forty-seven reviewed optional application records were disabled. Four low-level services were masked, inactive and had PID 0 after boot and standby.

The owner tested media playback and responsiveness incrementally. Channel selection/wrap is implemented and tested; at the last channel check, the media server had separate stream failures, so final successful stream delivery was not re-certified. This is not a latency benchmark or a zero-telemetry certificate.

## Current features

- Native NUI interface with light/dark themes and configurable time/date formats.
- Dock containing apps and numbered HDMI tiles. Hold OK to edit; OK picks up a tile, Left/Right moves it, Up toggles visibility, Back finishes.
- Continuous media rows with minimal scrolling to reveal the focused row.
- YouTube recommendations with phone/code sign-in, duration badges and selectable YouTube/TizenTube playback.
- Jellyfin Continue watching/Next up and live channels, paired with Quick Connect; Moonfin or Jellyfin playback.
- Configurable Netflix, TV Plus, Disney+ and Prime Video buttons across apps.
- Moonfin channel Up/Down with wraparound and guide on the middle channel press.
- Optional TV-only startup root and automatic privacy-mask reapplication.

## Layout

`quiet-tizen/` is the current native source and retained upstream toolkit. `tools/` adds portable operator commands and a private boot-package builder. `docs/` describes the Q60C setup. `legacy/web-launcher/` preserves the previous web launcher and trampoline. `.local/` holds private operator material and is never published.

The original web-launcher Git history is preserved. The quiet-tizen upstream Git database is retained privately, not imported as unrelated author history. Licensing and attribution remain in [NOTICE](quiet-tizen/NOTICE.md) and the included license files. The root toolkit is public-domain/Unlicense code; Samsung SDK/reference assemblies and stock apps must be obtained from your own permitted installation.

## Local build and checks

```powershell
py -3.12 quiet-tizen/tools/build.py payloads
# Obtain matching private TV references as explained in the manual, then:
py -3.12 quiet-tizen/tools/build.py launcher
$env:DOTNET_ROLL_FORWARD = 'Major'
dotnet run --project quiet-tizen/tests/CatalogTests.csproj -- quiet-tizen/launcher/assets/apps.json
node quiet-tizen/tests/moonfin-guide.test.cjs
node quiet-tizen/tests/moonfin-channels.test.cjs
py -3.12 -m unittest discover -s quiet-tizen/tests -p "test_*.py"
```

## Privacy and publication

Never commit `.local/`, private OAuth configuration, tokens, viewing/account data, certificates, signing keys, stock Samsung/Moonfin bundles, extracted SDK assemblies, screenshots containing accounts or raw registry snapshots. Ignoring a file does not remove it from an existing Git history. Inspect the staged changes before publishing.

Known ACR and voice components are disabled, with persistent opt-outs and automatic boot masks. A short startup interval remains before root masking. Closed firmware and streaming apps can have other data flows; no decrypted traffic audit established zero telemetry or the exact capture/upload frequency on this TV.
