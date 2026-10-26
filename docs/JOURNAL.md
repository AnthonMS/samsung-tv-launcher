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
