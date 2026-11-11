# Architecture and maintenance map

## Layers

1. Samsung's existing Tizen display, HDMI, audio, network, application and security framework stays installed.
2. `quiet-tizen/launcher/src` is the native .NET/NUI home UI, hosted in the existing Bixby Promotion slot.
3. The existing main-operation handler plus two vconf selectors route Home/startup directly to it.
4. Exclusive Wayland grabs route selected remote keys globally to the still-running launcher.
5. Direct HTTPS clients fetch YouTube/Jellyfin data; foreground playback is handed to installed apps.
6. A small version-guarded Moonfin bridge delivers EPG and channel navigation to Moonfin's own callbacks.
7. Optional boot privacy repurposes the separate Gaming Hub Service slot, roots through localhost SDB and recreates runtime masks. A token relay preserves PC management.
8. The optional QuietThings playback companion in the launcher exposes foreground-gated Jellyfin, web-player and Cobalt observations. Web/Cobalt providers additionally use the protected root playback service and app-local observers. The modified HA integration and personal dashboard are separate layers. See [current playback architecture and limits](HOME-ASSISTANT.md).

Home startup also depends on Samsung's separate Start with Smart Hub Home policy. On the tested Q60C, setting only the two app selectors did not survive restart while that policy was disabled. The current manual covers its typed backup, setting and inverse.

## Source map

| File | Responsibility |
| --- | --- |
| Home.cs | Scene construction, focus, settings, history and current-state diagnostics |
| Home.YouTube.cs / YouTubeClient.cs / YouTubeModels.cs | Recommendations, device-code enrollment and launch payloads |
| Home.Media.cs / MediaClient.cs / MediaModels.cs | Quick Connect, resume/next-up/live data and app playback |
| Home.Remote.cs / RemoteButtonPreferences.cs | Eight global key grabs, app selection and remote persistence |
| AppRowLayout.cs / DockOkHold.cs | Dock order/visibility and long-OK gesture |
| DisplayPreferences.cs / LauncherHistory.cs / HomeFeedLayout.cs | Clock/theme, internal Back and continuous row scrolling |
| launcher/moonfin/quiet-guide.js | Bounded destination receiver, channel queue and duplicate suppression |
| tools/moonfin_guide_bridge.py | Refuse unknown bundles; generate the local Moonfin patch |
| tools/boot-privacy | Headless bootstrap, token relay and four-service policy |
| tools/device.py (repository root) | Explicit-target script transport, private verified pull and SDB bridge |
| tools/prepare_boot.py (repository root) | Compile/sign private boot resources and package, without installing |
| Home.HomeAssistant.cs / QuietThingsPlayback | Optional companion, foreground/account callbacks and managed playback launch |

## Performance rules

Build the visible scene first. Fetch network data and images asynchronously. Bound feed/image caches and retain appropriate image views rather than recreating them on every focus change. Move only enough content to reveal the focused row. Do not add an always-running remote poller or unbounded retry loop. Never equate scene `build_ms` with a measured end-to-end latency improvement.

A feature was added, installed and tested before the next feature. Preserve this approach. The private session record is chronological; the journal groups the final design by cause and outcome.

## Updating dependencies

Do not ship Samsung framework references, stock package code, a Moonfin minified bundle, app icons downloaded for personal use, account databases or signing keys. Re-obtain references from the permitted SDK/TV. Reinspect Moonfin after app updates. Reinspect firmware-dependent labels, UIDs, package slots, private APIs and unit conditions after firmware changes.

The boot helper is bound to the inspected firmware build date and runtime path. Keep a working management connection and manual Developer Mode recovery route before changing it. Never disable updates as a generic shortcut.

## Managed Community Apps updates

`CommunityApps/ManagedUpdates.cs` owns package transformations, applied after upstream digest/manifest checks and before same-author signing. It checks specific callback/service structure, preserves upstream code around the additions and records adapter identity independently of upstream version. Catalogs cannot execute arbitrary scripts. Newer Moonfin AVPlay seeks report their measured position on successful completion; failed seeks do not fabricate state.

TizenTube's injected bootstrap loads an owner-provisioned external runtime whose code and private observation key survive package replacement. Samsung YouTube keeps its stock configuration and normal official update path; managed launch supplies our mode-specific arguments. App-local labels, private key permissions and native service lifecycle still require hardware verification. Version 0.2 final device acceptance is pending; see COMMUNITY-APPS.md.
