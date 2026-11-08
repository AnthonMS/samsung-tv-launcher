# QuietThings and optional playback development

This branch adds a Home Assistant upgrade to the existing native launcher. The development build is installed on the tested Q60C and suitable for its verified Moonfin playback use. It is not a complete all-app playback solution or a generally tested release.

## Pieces

- LanApi.cs and LanApiCore.cs: original QuietThings v1 server, unchanged, on port 18765.
- Home.HomeAssistant.cs: host integration, foreground query, existing local media-account access and route-address selection.
- QuietThingsPlayback/*.cs: optional companion on port 18766, foreground-gated providers and status/control protocol.
- Home.cs: small API initialization/lifecycle hooks and absolute volume/mute/source-result wrappers. Existing navigation, accounts and remote features are retained.

Both APIs read lan-api.token from the app data directory. A missing/invalid token denies access. The playback collector reads the existing media-account.json only on the TV. That account is not exported to HA. Collection is demand-driven. A pending native foreground query is reused, so repeated timeouts cannot accumulate queries.

Moonfin/Jellyfin observations are selected by actual foreground app, exact TV endpoint and signed-in user, rather than an old Home Assistant session entity. Selection is rechecked after network reads and before commands. Other foreground apps are identified but have unknown playback until a provider is implemented and tested. No screenshot, audio-activity or last-key heuristic is used.

The companion lives in the launcher process. Installed checks found both APIs reachable while Moonfin and YouTube were foreground; longer background and memory-pressure behavior remain unverified. The boot/privacy helper is unchanged; this branch does not turn its one-shot root bootstrap into an always-running media daemon.

## Current validation

- Physical remote and existing HA remote navigation work; all eight custom grabs and account sign-ins were retained.
- Moonfin playing/paused state, title, duration, advancing position and automatic episode transitions reach a stable QuietThings HA entity and the existing universal player.
- Real play/pause commands passed. The HA integration immediately refreshes an observed state after each command, avoiding its default 10-second request cooldown. A measured rapid pause/resume pair confirmed state in about 0.20/0.16 seconds; app/server reporting can still add latency.
- The dashboard tile and popup show playback state. Read-only progress ticks every second using the actual position timestamp, and holds still when paused. The popup uses a combined play/pause control for supported playback and explicit controls when state is unavailable.
- Following a power cut, both APIs, boot helper, privacy masks, remote grabs and sign-ins recovered. Controlled standby/wake is still a separate pending test.
- YouTube foreground identity was detected with playback unknown and metadata absent. There are no YouTube, TizenTube, Twitch or KickTV playback providers yet.

Seek/stop, the separate Jellyfin client, deliberate app-switch clearing, actual reinstall/session replacement and longer lifecycle checks remain pending on hardware. Automated tests cover session replacement, foreground races and expiry. Daily Moonfin use is accepted only within this boundary.

The separate [QuietThings fork](https://github.com/AnthonMS/QuietThings) contains the integration and generic companion. [Draft PR #1](https://github.com/magnetgrouplabs/QuietThings/pull/1) requests architectural review, including the separate listener. This launcher repository supplies the device-specific host callbacks.

## Build and checks

Use matching private references as described in MANUAL.md. Build to a separate output folder while evaluating this branch; do not overwrite the accepted private deployment package accidentally.

```powershell
$env:DOTNET_ROLL_FORWARD = 'Major'
dotnet build quiet-tizen/launcher/src/Home.csproj -c Release -p:TV_FRAMEWORK_DIR=YOUR_PRIVATE_REFERENCES -o .local/quietthings-development/build
# Run the original QuietThings kit predeploy against launcher/src and those references.
py -3.12 -m unittest discover -s quiet-tizen/tests -p "test_*.py"
```

The separate QuietThings development repository includes the companion tests and the HA integration. Its experimental/playback/README.md documents the protocol and two HA test targets. HA 2026.5.4 is tested with samsungtvws 2.7.2, matching its stock Samsung integration. The newer target uses 3.0.7. Do not force the newer library under the older stock integration.

The original kit verifies that both v1 files are byte-identical. Do not alter those files to extend playback; the companion has a separate protocol/version. Keep the upstream MIT notice.

## Device rollout

Follow MANUAL.md and the ignored operator handoff. Freshly inspect firmware, runtime, package metadata, current DLL/hash, selectors, token presence and management route. Use a new backup phase, consistent SQLite snapshots and a verified private copy of the original DLL before any swap. Preserve account/preferences data. Prepare a DLL/token-specific inverse; never restore live registries wholesale.

Use the established Q60C deployment procedure, including refresh of only an exact idle /usr/bin/dotnet-loader 0 process if needed. Restart only the custom launcher. Do not stop Moonfin, streaming apps, display/audio services or the boot helper for this installation. Do not reboot or standby before root cleanup completes.

The public tools/device.py now forwards the bridge parameters to its vendored root probe while retaining the narrow runtime check. Set OpenSSL/Git's tools on PATH as the manual requires. Its pull size check follows symlinks, matching the bytes actually transferred and hashed.

After installation, test the original API while the launcher is foreground and background, then the companion status and explicit supported commands. Add the development HA integration only with the compatible dependency requirements. Set its playback port to 18766; default 0 disables it. Preserve media_player.living_room_tv_universal as the dashboard facade during migration. Do not restart all HA services or replace an existing approved Samsung token as an incidental step.

For future builds, repeat the applicable checks above and the remaining volume/mute/HDMI, guide/channel, seek/stop, app-switch and standby/wake checks. A read-only diagnostic in SDK context does not substitute for installed-package verification. Keep the targeted inverse and private backup until the broader trial is complete.
