# Build and install Quiet Home

Quiet Home is a native NUI/.NET application, with Home/startup and remote-key
routing adjusted to make it act as the launcher. It does not replace the
kernel or the whole Tizen shell. Samsung's settings, app framework and media
stack remain available underneath.

The design follows Free My TV: light background, wide feature card, two
portrait cards, colorful tiles, rounded controls and a translucent dock.
All UI text here is English. See [attribution](../NOTICE.md).

## Obtain matching private framework references

Build against the TV's own compatible `Tizen.NUI.dll`,
`Tizen.Applications.Common.dll`, `Tizen.System.dll` and `Tizen.dll`.
The shipped project files use `Private=false`, so those references are not
copied into the app package.

Find the paths on the target:

```sh
tv exec --command 'find /usr/share/dotnet.tizen -name Tizen.NUI.dll -o -name Tizen.Applications.Common.dll -o -name Tizen.System.dll -o -name Tizen.dll'
```

Inspect the results and pull the four matching runtime assemblies individually
with `tv pull REMOTE_PATH .local/tv-framework/NAME.dll`. Do not use a random
desktop/mobile SDK version just because it has a file with the same name.
Do not commit the resulting assemblies.

```sh
python3 tools/build.py launcher --framework .local/tv-framework
```

This builds `BixbyPromotion.dll`, `QuickLaunch.dll` and `Probe.dll`, then
assembles `.local/package/`. Public source cards are neutral text placeholders;
[optional publisher artwork](06-artwork.md) can be fetched before rebuilding.

## Adapt inputs and apps

The original UI uses HDMI 2 as the wide PlayStation 5 card, HDMI 3 as the
Switch card, and HDMI 1 as the third card. Source IDs 14, 15 and 13 were observed
on the tested TV. They are example wiring, not auto-discovery.

Upload and run Probe to inspect sources and the current main-operation handler:

```sh
tv push .local/build/Probe/Probe.dll Probe.dll
tv exec --command '/usr/bin/dotnet /home/owner/share/tmp/sdk_tools/quiet-tizen/Probe.dll'
```

Edit labels, source actions, `RefreshSource`, `SourceIndex` and the default
`lastSource` in `launcher/src/Home.cs` to match the actual devices. The library
is read from `apps.json`; verify all app IDs against the TV. See
[app configuration](06-artwork.md). Rebuild after source changes.

## Install without changing Home yet

The tested native-app container is **`com.samsung.tv.bixby-promotion`**.
Its `BixbyPromotion.dll` is replaced with our assembly, while the package's
existing identity, metadata and framework integration are retained. This
replaces Bixby Promotion's function. It is not a store-signed standalone TPK.

Inspect the installed package, its owner/labels, original entry assembly and
current registry flags first. This release's recipe requires the observed
owner `201:0`. If the layout differs, stop and adapt the profile.

```sh
tv exec --script recipes/snapshot.sh --arg home-01
tar -czf .local/package.tar.gz -C .local/package .
tv push .local/package.tar.gz package.tar.gz
tv exec --script recipes/install-launcher.sh --arg home-01
```

The recipe backs up the original package before replacing the assembly,
moves the cached native image out of the execution path, installs resources,
applies the package's observed SMACK label and requests a manual launch.
It does not change Home/startup.

The new release recipes are a generalized form of the operations used on the
original TV. Filesystem steps are not a single atomic transaction. If a step
fails, inspect the saved state and use recovery; do not rerun by deleting
the original backup. The snapshot and backup folder is
`/opt/usr/share/quiet-tizen-backups/home-01`.

Verify visibly:

- All pages render; the D-pad, Select and Back behave correctly.
- HDMI cards switch the intended input, with picture and audio.
- Volume/mute and applicable quick settings work.
- Native Advanced and Network settings still open.
- Chosen app shortcuts launch the correct installed apps.
- The Controllers shortcut reaches the Bluetooth device list.

The `build_ms` value in `/tmp/quiet-home-state.txt` measures scene construction,
not input-to-photon latency or complete rendering time.

## Make it open on Home and startup

Save the **three original values** privately first: the main-operation Home
handler printed by Probe, `db/appfw/home_appid`, and
`db/appfw/startup_appid`. They all happened to be `com.samsung.tv.csfs` on the
case-study device; restoration must use the actual saved values.

Only after the visible verification above:

```sh
tv exec --command '/usr/bin/dotnet /opt/usr/share/quiet-tizen-backups/home-01/Probe.dll set-home com.samsung.tv.bixby-promotion'
tv exec --command 'vconftool set -f -t string db/appfw/home_appid com.samsung.tv.bixby-promotion; vconftool set -f -t string db/appfw/startup_appid com.samsung.tv.bixby-promotion'
```

Read all three selectors back. Let temporary root cleanup finish, then
restart with the remote. Check full boot, the Home button, standby/wake
and return from a streaming app separately. Automatic startup was verified
on the original TV; an OS update can change routing.

## Replace the slow settings/sidebar path

The quick settings page handles volume, mute, brightness, Game Mode and
sleep timer with native APIs. Availability is checked for the current input.
Advanced settings, network setup and Bluetooth pairing still use Samsung's
native menu on demand.

The small QuickLaunch bridge exits on vendor `launch_type=preload` requests.
For interactive requests it opens Quiet Home's settings page and exits.
It does not keep a second heavy settings process running.

After quick settings work, take a new snapshot and install the bridge:

```sh
tv exec --script recipes/snapshot.sh --arg shell-01
tv exec --script recipes/install-settings-bridge.sh --arg shell-01
```

This replaces `/opt/usr/apps/com.samsung.tv.quicklaunch/bin/QuickLaunch.dll`
and updates two `key_filter` rules:

| Rule owner / key | New destination |
|---|---|
| `com.samsung.tv.quicklaunch` / `XF86More` | Quiet Home, `quiet_page:settings` |
| `org.tizen.menu` / `XF86SysMenu` | Quiet Home, `quiet_page:settings` |

The recipe selects semantic keys and checks the saved values. It does not
copy another TV's row numbers. It notifies
`memory/appfw/app_context_db_change` after the transaction.

Test both settings buttons, then generate/apply the **`light-shell`** cleanup
group from the `shell-01` registry snapshot. That disables the native sidebar
and browser. Keep the QuickLaunch bridge and `org.tizen.menu` enabled.

Retain rollback files and remove only the known staged files after verification:

```sh
tv exec --command 'rm -f /home/owner/share/tmp/sdk_tools/quiet-tizen/package.tar.gz /home/owner/share/tmp/sdk_tools/quiet-tizen/Probe.dll; rm -rf /home/owner/share/tmp/sdk_tools/quiet-tizen/unpacked'
```

## Performance choices in the launcher

App art is local. Real `ImageView` objects are created ahead of use and moved
between the hidden cache parent and visible cards. `ReleasePolicy=Destroyed`
keeps decoded textures alive while those views live. The cache is bounded
by catalog size and the grid/dock variants, not an ever-growing screenshot
cache. Image loading is asynchronous, so a first launch still needs loading
time; subsequent page visits avoid recreating image views.

The clock updates every 30 seconds and stops while the launcher is paused.
There is no always-running controller-poll loop. The 180 ms repeat filter
applies inside Quiet Home only; changing it cannot fix input lag inside Netflix.
Local diagnostic files retain only current launcher state and are overwritten.
YouTube recommendations use a direct HTTPS client; no telemetry was added.

## Optional YouTube recommendations

Settings → YouTube & media → YouTube controls row visibility, account connection, playback app
and manual refresh. Connecting displays a device code and Google verification
URL on the TV; approve the desired account on your phone. Back cancels an
unfinished connection. Disconnecting removes this launcher's token and feed,
without signing out of either playback app.

For a local build, copy `launcher/assets/youtube-client.example.json` to
`youtube-client.local.json` and supply a compatible YouTube TV device-flow
client configuration. The local file is ignored by Git and included in the
local deployment package. User account tokens are created only after approval
and stored separately in writable app data with owner-only permissions.
Do not publish tokens, personalized caches or device backups.

The Home row shows up to twenty recommendations, five thumbnails at a time,
with titles, channel names and inset duration badges. Up from the dock enters
recommendations; Up again reaches the toolbar. Playback can use the installed
YouTube or TizenTube app. On this TV both installed manifests map to Samsung's
native Cobalt player. Their local catalog entries set `NativeVideoPayload=true`,
so each selection sends one plain `v=<id>` PAYLOAD to the chosen app ID. No
delayed resend is used for that native path. TizenTube's JSON `args` envelope
is retained only for legacy web-wrapper installations without that catalog
flag, with the guarded cold-launch fallback. Check the installed manifest's
`nativeID` mapping before enabling the flag on another TV/build.

Cached results appear immediately. Refresh runs asynchronously on entry when
older than thirty minutes; failures have a one-minute retry cooldown.
Thumbnails download two at a time, with at most twelve decoded image views
and forty-eight disk files retained. Disabling the row cancels its fetches.
There is no background service or recommendation polling timer. The feed uses
the YouTube TV browse response, so changes to that response may require a
parser update.

The shell background now uses a native gradient visual instead of enlarging
an SVG texture. Existing publisher artwork and HDMI tile assets are unchanged.

## Jellyfin / Moonfin rows

Settings → YouTube & media also contains the Jellyfin server address, Quick
Connect account pairing, playback-app choice and independent Continue / Next
up and Live channels toggles. Moonfin is the default player; Jellyfin can be
selected instead. Both clients must already be connected to the same server
and user on the TV. Quick Connect approval happens in an authenticated
Jellyfin client under Profile → Quick Connect; no password is entered in
Quiet Home. Back cancels a pending code. The editable server address supports
HTTP/HTTPS and an optional base path. A private local build can seed it using
the ignored `media-server.local.json`, based on `media-server.example.json`.

Continue watching combines resumable videos with Next up, keeping resume
items first and excluding duplicate items or Next up episodes from a series
already being resumed. Episode titles include series and episode information.
Resumable tiles show progress; Next up starts at the beginning. Live cards
show the channel logo, cleaned name and current programme, without duration
or progress. Display cleanup removes parenthesized/bracketed geo-block and
resolution/quality labels; it never changes server metadata or channel IDs.

The content rows share a clipped area above the persistent dock. Compact
spacing leaves neighboring rows partially visible. Up/Down moves focus and
scrolls only enough to reveal a row's heading, artwork and captions; a row
already fully visible stays still. Rows are not centered or snapped to a
fixed page. Continue/YouTube show five thumbnails across and Live shows seven
logos. Up from the dock enters the last enabled content row, further Up moves
toward the toolbar; Down reverses this. Each row browses horizontally with
Left/Right. Home resets the content to the top and focuses the first dock app.

Feeds load asynchronously and are cached in private app data. They refresh
on return from another app or after two minutes when entering Home, with a
20-second failure cooldown and a manual refresh tile. Images use authenticated
requests, two concurrent downloads, a bounded 24-view decoded cache and an
80-file disk cache. No account token is added to artwork URLs or diagnostic
state. There is no media-service daemon or periodic network timer.

Playback opens the selected installed client and waits up to 25 seconds for
its controllable server session. The session must match the selected client,
paired user and this TV's exact source IP. A server PlayNow command carries
the item ID and resume position; live items carry no position. This avoids
controlling another Moonfin device on the account. Returning Home cancels a
pending handoff. Session acknowledgment and actual client playback still
need to be checked on each installed client/version.

Global Netflix, Samsung TV Plus, Disney+ and Prime Video shortcuts are
configured under Settings → Remote & controllers. See
[remote button setup and recovery](10-remote-buttons.md) for the scoped
permission and Moonfin guide bridge used on the inspected TV.
