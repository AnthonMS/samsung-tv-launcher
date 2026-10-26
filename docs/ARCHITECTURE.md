# Architecture and maintenance map

## Layers

1. Samsung's existing Tizen display, HDMI, audio, network, application and security framework stays installed.
2. `quiet-tizen/launcher/src` is the native .NET/NUI home UI, hosted in the existing Bixby Promotion slot.
3. The existing main-operation handler plus two vconf selectors route Home/startup directly to it.
4. Exclusive Wayland grabs route selected remote keys globally to the still-running launcher.
5. Direct HTTPS clients fetch YouTube/Jellyfin data; foreground playback is handed to installed apps.
6. A small version-guarded Moonfin bridge delivers EPG and channel navigation to Moonfin's own callbacks.
7. Optional boot privacy repurposes the separate Gaming Hub Service slot, roots through localhost SDB and recreates runtime masks. A token relay preserves PC management.

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

## Performance rules

Build the visible scene first. Fetch network data and images asynchronously. Bound feed/image caches and retain appropriate image views rather than recreating them on every focus change. Move only enough content to reveal the focused row. Do not add an always-running remote poller or unbounded retry loop. Never equate scene `build_ms` with a measured end-to-end latency improvement.

A feature was added, installed and tested before the next feature. Preserve this approach. The private session record is chronological; the journal groups the final design by cause and outcome.

## Updating dependencies

Do not ship Samsung framework references, stock package code, a Moonfin minified bundle, app icons downloaded for personal use, account databases or signing keys. Re-obtain references from the permitted SDK/TV. Reinspect Moonfin after app updates. Reinspect firmware-dependent labels, UIDs, package slots, private APIs and unit conditions after firmware changes.

The boot helper is bound to the inspected firmware build date and runtime path. Keep a working management connection and manual Developer Mode recovery route before changing it. Never disable updates as a generic shortcut.
