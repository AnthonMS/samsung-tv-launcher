# Engineering journal and actual command history

This is an outcome-oriented record of the October 2026 work. The complete retained personalized scripts/results, original session narrative, binaries and backups are preserved privately in `quiet-tizen/.local/`. The [command index](COMMAND-INDEX.md) records every retained top-level operator script by filename/hash and whether a result was saved. The manual spells out the reviewed final command sequence with placeholders. Retired experiments are not a batch installer.

## Starting point: web launcher and trampoline

The previous HTML/CSS/JavaScript launcher had working phone/code YouTube enrollment and Jellyfin Quick Connect. Home did not reliably foreground a heavy already-running web app, so a tiny trampoline launched it normally and exited immediately. A delayed exit failed because the trampoline was paused as soon as the real launcher covered it. That implementation and its original Git history are preserved under `legacy/web-launcher`.

## Native Quiet Home trial

We started from quiet-tizen commit `cf518d7a9278dca7990e27756218258e44044a29`, originally documented for a different U8000F model. Q60C firmware was independently checked in `/etc/prd_info.ini`. Matching private .NET references were obtained from the TV. The Bixby Promotion container was backed up and its native IL replaced with the compiled launcher, preserving its package identity, owner and SMACK label. The owner accepted its responsiveness before Home/startup were assigned.

Early filesystem steps stopped when `cmp` was missing or command-channel escaping changed `find -exec`. SHA256 comparisons and reviewed loop/transport forms replaced those assumptions. Earlier completed operations were inspected; the original backup was not overwritten to retry.

## Small visible changes, verified incrementally

Custom app order/visibility first used a separate screen. Owner feedback changed it to direct dock editing on Home: jiggle, pickup with OK, Left/Right movement, Up visibility toggle and long-OK entry. App icons became full-tile with focused-only inset captions. Counter/arrows went through font/layout fixes after the TV rendered missing glyphs/ellipsis; vector arrows and explicit layout resolved it. The counter moves down when arrows hide.

Large page titles/navigation bars were removed in favor of corner circles. Focus became clearer. Internal Back gained real navigation history instead of exiting to the last input. Home consistently selects the first dock app. The large HDMI source row was replaced by editable numbered gradient dock tiles, without hardcoding a device to a port.

Clock/date formats and light/dark themes became settings. A pixelated background was replaced with a smooth gradient. YouTube recommendations were implemented first, then Jellyfin Continue/Next up and live channels. Thumbnails/duration badges and media channel labels followed the existing visual style. Rows changed from paging/hidden panels to continuous clipped content with minimal focus scrolling. The owner repeatedly reported the launcher remained snappy.

## Playback findings

YouTube/TizenTube native video payloads avoided the slow generic legacy-wrapper path. Testing two videos with a Home return exposed a warm-launch failure that a single cold launch missed. The corrected route was accepted as immediate. The UI alone did not distinguish the two visually similar playback apps.

Jellyfin Quick Connect and Moonfin playback for resume and live items were physically accepted. Display suffix removal did not bypass geo-blocking. DR1 failed manually in Moonfin while Cartoon Network worked; later all streams failed after a server restart. The owner took over that separate backend issue. Channel selection was accepted as attempting the right streams, with final successful delivery left qualified.

## Global buttons and the permission incident

A SQL key-filter pilot was reverted to its exact original values. Native named Wayland exclusive grabs became the final approach. The launcher originally had no effective keygrab permission; an exact client/user/privilege rule was backed up and added, allowing four branded keys and later four rocker/guide aliases.

During investigation, `security-manager-policy-reload --help` was mistakenly invoked as though it were a help query. It is a script that ignores arguments and reapplied shipped base/user-type Cynara policies and privilege-group mappings. The actual security/cynara processes remained running; the migration branch was skipped with SMACK enabled. No pre-call Cynara snapshot existed, so exact pre/post policy equivalence could not be established. It did not directly write the ad/consent settings. The later audit checked the actual consent rows and found optional viewing/ads/remote-management choices off. This incident is documented rather than hidden or described as harmless. Never use that command for querying permissions.

## Warm EPG and channel changes

Native warm app controls updated Moonfin's requested data without reliably dispatching its JavaScript event. A web self-launch test did dispatch, but that was insufficient evidence for a native sender. The final bridge uses app-control bootstrap for cold starts and a Tizen message port for warm delivery. A shared per-press nonce avoids duplicate delivery.

The first rocker implementation attempted server Sessions/PlayNow control and only opened the guide. It was removed from the public launcher. Direct authenticated lineup lookup, sort-compatible wrap logic and Moonfin's actual guide/player callbacks became the final route. It queues steps, caches a scoped lineup and releases an old player before changing items.

Replacing a DLL did not always replace the executed code: the idle .NET preloader retained an old assembly. The repair inspected the exact executable and command line, terminated only `/usr/bin/dotnet-loader 0`, then restarted the target app. No kill-all dotnet command was used.

```sh
for P in $(pgrep -x dotnet-loader);do
 test "$(readlink /proc/$P/exe)" = /usr/bin/dotnet-loader || continue
 C=$(tr '\000' ' ' < /proc/$P/cmdline)
 case "$C" in '/usr/bin/dotnet-loader 0 '|'/usr/bin/dotnet-loader 0') kill -TERM "$P";;esac
done
```

## Privacy and root persistence

The first cleanup disabled 43 reviewed app records, nine marker files for ten services, five typed preferences and one typed WAS JSON property. Exactly those flags changed; unrelated app fields stayed equal to the snapshot. Live SIGTERM stops exposed cached launchpad auto-restarts. A full restart caused the new disabled flags to be read fresh and the selected native executables stayed absent.

Four read-only-system services could only be masked under `/run`; those masks expired after a full restart. The owner explicitly requested persistence. The root fork's TV-local SDB chain was adapted into a headless service in the unused Gaming Hub Service slot. A temporary PC relay tested the chain before changing the developer address. It produced UID 0 proof and actual masks on this Q60C.

Four more voice aliases were found and disabled: stt-client, Alexa wake-word, wakeupless and VIF. The total became 47. Developer Host IP moved to localhost, with token bridge access for the PC. Fresh boot proof matched a new kernel boot ID; all masks applied autonomously and the one-shot helper exited. The owner tested 30 seconds of standby and remote wake; the bridge, masks, accounts and eight key grabs remained.

The original session automated the final transition with a delayed background command equivalent to:

```sh
# Historical operation, not the preferred manual flow.
# It must not reboot before the root helper finishes cleanup.
nohup /bin/sh -c 'sleep 45;vconftool set -f -t string db/sdk/develop/ip 127.0.0.1;sleep 2;/sbin/reboot' </dev/null > TRANSITION_LOG 2>&1 &
```

The helper's launcher-wait step timed out around the background child. The actions completed and the following bridge session verified the fresh boot and cleanup. An earlier 15-second deferred reboot reached the final hash check too early and lost that check. The public manual instead changes Developer Mode in the UI and restarts only after the command helper returns, avoiding that lifetime race.

The observed root policy finished about fourteen seconds after the boot helper started. This is not a zero-startup-window or zero-telemetry guarantee. No core security framework was disabled, no global passwd/key file replaced, no firmware flashed, no package database restored wholesale and no generic collector-kill watchdog installed.

## Repository consolidation

The working tree, private evidence and external root toolkit were moved to the new Samsung project folder. The pre-existing public web-launcher history was preserved; the separate quiet-tizen Git database and root-toolkit checkout remain private. Reusable transport, package-building tools and manuals replace dependencies on personal absolute paths. Raw captures/credentials/proprietary files are not published. New publication uses the owner's GitHub identity with no co-author trailer.

## Local Home Assistant playback extension, November 2026

The existing digital remote did not provide reliable playback metadata across apps. An optional companion now runs beside the unchanged QuietThings v1 API inside Quiet Home. HA uses a modified integration and the existing living-room universal player/dashboard. Local Jellyfin session selection serves Moonfin/Jellyfin; a protected root inspector bridge serves Twitch/KickTV; an app-local Cobalt proxy/page observer serves ordinary YouTube and TizenTube. App-specific observations, foreground checks and expiring player/document ownership prevent stale metadata and controls from crossing app switches.

Real hardware checks passed the controls listed in HOME-ASSISTANT.md for all six apps. The custom remote remaps use the same managed launch path as dock tiles. Twitch lacks a stream title, KickTV exposes a channel identifier, and their no-player observations still lose idle status. Those extraction gaps are unfinished, not proven impossible. Live seek/finite duration/stop are not advertised. Confirmed empty Cobalt menus now preserve the correct app name and report idle. Dashboard progress is read-only timestamp interpolation, not a sample every second or a draggable seek control.

Several practical failures shaped the implementation: HA refresh-request cooldown delayed resume feedback; native Cobalt lacked the attempted production inspector path; shared proxy workers needed exact recycling; active Cobalt could retain Samsung's generic loader; cached HTML could replay retired observer capabilities; and app-context SMACK labels mattered when selecting the optional signed startup resources. Narrow fixes retained the original API contract, root recovery pair, account data, remote grabs and security framework. See the current technical guide and QuietThings experimental/playback documentation for the accepted scope and source map.

## Home startup policy repair, 10 November 2026

Owner review found power-on opening the input viewer while Home still worked. Main-operation and home selectors were correct. Changing startup_appid alone failed a controlled reboot. Samsung's separate db/menu/smart_hub/ontvsetting/auto_start Int32 policy was disabled. Enabling it and restoring the native startup selector passed the next full boot and input-to-Home wake readbacks. The repair changed settings, not the launcher binary; the legacy first-screen value was left untouched. Backups and a guarded policy inverse are separate from the playback inverse.

The owner was unavailable for physical confirmation, so final startup acceptance uses actual foreground, stock power and HA state. HA deep-standby wake remains a separate limitation: direct Samsung wake was intermittent and one QuietThings request was skipped while its entity was unavailable. Repeated PC Wake-on-LAN recovered the device for final checks. Do not describe the startup repair as a guaranteed HA wake fix.

Upstream PR1 is closed, and the owner requires days of daily testing before further PR work. The full implementation requires custom TV runtime components plus the modified integration; it is not an HA-only upgrade or a generally released installer. General fixes/optional protocol support may be considered separately later. This documentation update made no device/runtime changes and did not rerun the recorded test suites.

## 2026-11-10 - Twitch and KickTV broadcast titles

Added channel-matched Twitch player headings and bounded Kick channel-metadata lookup for real broadcast titles. Kick title lookup uses its normal HTTPS API without additional credentials; controls stay local. Confirmed menus now report idle and clear old media. Actual two-channel tests per app passed HA title, pause/resume and menu clearing. Initial titles can take a subsequent poll. Existing dedicated QuietThings entity exposes app_name/app_id/media_title and playing/paused/idle; dashboard already reads it. Managed dock/remap launches remain required and no finite live seek is advertised.

131 playback checks, 21 bridge checks, 22 Python HTTP/lifecycle tests, context/probe and Cobalt regressions passed; launcher/bridge builds had zero warnings/errors. No fresh full reboot or HA restart for this title update. Updated technical/writer documentation and retained guarded recovery. No commits, pushes or PR changes; daily-use testing hold remains.

## 2026-11-10 - Native Community Apps development release

Added Community Apps inside Quiet Home Settings and a Samsung Apps dock entry, with on-TV source entry, dynamic installed apps and an in-process signer/localhost installer. No separate store app or broad installation relay is required. Package identity/source checks preserve existing author identity and block ordinary updates to the installed customized media packages. Only the MIT tizen-sdb signature subset was reused, with license and provenance.

110 Community Apps checks, 315 original API checks, existing catalog/media/remote tests and a zero-warning launcher build passed. Actual TV checks covered signed catalog/profile loading, fresh clock installation, same-version reinstall with retained data, GitHub/signed-source lifecycle, dock access and official store launch. The final build passed full reboot and another native installation. Test app and temporary staging were removed; guarded recovery and private profiles were retained. Owner visual/typing acceptance and broader package/different-version upgrade coverage remain pending. See COMMUNITY-APPS.md. The reported HA wake-state failure remains separate and unresolved. No commits, pushes or PR changes.
