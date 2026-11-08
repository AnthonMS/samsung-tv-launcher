# QuietThings and optional playback development

This branch prepares a Home Assistant upgrade for the existing native launcher. It is source code and a locally verified build. It has not replaced the accepted launcher on the TV.

## Pieces

- LanApi.cs and LanApiCore.cs: original QuietThings v1 server, unchanged, on port 18765.
- Home.HomeAssistant.cs: host integration, foreground query, existing local media-account access and route-address selection.
- QuietThingsPlayback/*.cs: optional companion on port 18766, foreground-gated providers and status/control protocol.
- Home.cs: small API initialization/lifecycle hooks and absolute volume/mute/source-result wrappers. Existing navigation, accounts and remote features are retained.

Both APIs read lan-api.token from the app data directory. A missing/invalid token denies access. The playback collector reads the existing media-account.json only on the TV. That account is not exported to HA. Collection is demand-driven. A pending native foreground query is reused, so repeated timeouts cannot accumulate queries.

Moonfin/Jellyfin observations are selected by actual foreground app, exact TV endpoint and signed-in user, rather than an old Home Assistant session entity. Selection is rechecked after network reads and before commands. Other foreground apps are identified but have unknown playback until a provider is implemented and tested. No screenshot, audio-activity or last-key heuristic is used.

The companion currently lives in the launcher process. Background and eviction behavior still require installed-device tests. The boot/privacy helper is unchanged; this branch does not turn its one-shot root bootstrap into an always-running media daemon.

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

Acceptance still requires correct volume/mute/HDMI, existing remote grabs, guide/channel shortcuts, sign-ins, actual playing/paused/seek behavior, app-switch metadata clearing, standby/wake and full restart. A read-only diagnostic in SDK context does not substitute for these checks in the installed package's sandbox.
