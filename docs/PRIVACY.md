# Privacy cleanup and automatic boot protection

## Scope and limits

The [Q60C profile](../quiet-tizen/profiles/tq65q60cau-2220.4.json) lists 47 reviewed optional app IDs, grouped into advertising/analytics, context helpers, voice assistants and optional SmartThings/Health/Gaming Hub extras. Those extras are optional features, not automatically malware. Preserve `com.samsung.tv.bixby-promotion` and `com.samsung.tv.ghservice`, which host the custom code, and preserve core display, audio, HDMI, Bluetooth, networking, accessibility, app management and account services.

ACR can fingerprint HDMI content, as [measurement research](https://arxiv.org/abs/2409.06203) demonstrated on its tested devices. [Samsung describes it as optional](https://www.samsung.com/us/support/answer/ANS10010616/). This project did not establish this exact TV's sampling frequency, uploaded screenshot contents, data sales or zero telemetry across closed firmware. Software state is not proof a microphone is electrically disconnected.

## 1. Use the TV's consent controls

In native Settings, Privacy Choices/Terms & Privacy, opt out of Viewing Information Services, Interest-Based Ads and optional Online Remote Management. Menu wording varies. Do not accept optional voice services if unwanted. Keep mandatory terms needed for chosen services. On the inspected TV, these optional choices were already off; we verified them rather than rewriting a consent database or clearing account cookies. No safe system-wide optional-cookie switch was found in the inspected configuration.

The actual consent database was `/opt/usr/home/owner/apps_rw/com.samsung.tv.termsclient/data/termsclient.db`. Read-only verification:

```sh
sqlite3 -readonly -header /opt/usr/home/owner/apps_rw/com.samsung.tv.termsclient/data/termsclient.db "SELECT id,isAgreed,isAgreementPut FROM table_home_data_disclaimer_info WHERE id IN ('viewing','syncplus','rm','tnc','pp');"
```

The case had optional IDs 0/1 and mandatory tnc/pp 1/1. The separate `/opt/dbspace/.privacy.db` was a package/privilege mapping database, not these consent choices. Do not edit a similarly named database blindly.

## 2. Generate field-specific cleanup from your fresh snapshot

Create another unique consistent snapshot on the TV and pull its `.pkgmgr_parser.db` into `.local/`. The planner reads your original flags and refuses absent IDs or unknown boolean formats. Generate and apply one chosen group at a time:

```powershell
py -3.12 quiet-tizen/tools/plan_cleanup.py --snapshot .local/privacy/.pkgmgr_parser.db --profile quiet-tizen/profiles/tq65q60cau-2220.4.json --groups ads_analytics --output quiet-tizen/.local/plans/ads
# Review apply.sql, restore.sql and before.json. Copy restore.sh to your TV backup first.
tv exec quiet-tizen/.local/plans/ads/apply.sh
```

Repeat for `context`, `voice_assistant` and `optional_extras`, each in a new output folder, only after reviewing that group's purpose and results. The intended tuple is `app_disable=true`, `app_onboot=false`, `app_autorestart=false`. The inverse restores each observed original tuple with drift guards. This does not delete packages or app data.

## 3. Persistent vendor stop markers

Inspect each exact unit with `systemctl cat UNIT`. Create a marker only when that unit really has the exact negated `ConditionPathExists` shown below. Record its original presence and unit activity first.

| Unit | Marker under /opt/usr/share/systemd/system/ |
| --- | --- |
| adagent-service.service, pisa-service.service | pisa-service (shared) |
| canalysis-service.service | canalysis-daemon |
| com.samsung.tv.context-aware-service.service | com.samsung.tv.context-aware-service |
| com.samsung.tv.context-aware-agent.service | com.samsung.tv.context-aware-agent |
| com.samsung.tv.bixby-prov.service | com.samsung.tv.bixby-prov |
| org.tizen.voice-client.service | org.tizen.voice-client |
| com.samsung.tv.ondevice-voice.service | com.samsung.tv.ondevice-voice |
| com.samsung.tv.ghdaemon.service | com.samsung.tv.ghdaemon |
| org.tizen.voice-app.service | org.tizen.voice-app |

Use `quiet-tizen/recipes/stop-marker.sh apply UNIT MARKER SNAPSHOT_NAME` through the helper. Its corresponding restore operation preserves a marker that predated this work and restarts only a previously active unit. For the shared PISA marker, record both units before creating it; ownership of that single marker belongs to the group, not two independent removers. Do not infer a stop condition from a filename.

## 4. Preferences and cached configuration

Read and save the type/value of every key before changing it. On the tested build the original values were true/1, and the intended settings were:

```sh
vconftool set -f -t bool db/menu/voice/voice_activated 0
vconftool set -f -t int db/menu/voice/wakeupless_status 0
vconftool set -f -t bool db/si_service/voice_recog_enable 0
vconftool set -f -t string db/was/preload_by_country false
vconftool set -f -t bool db/menu/autorun_tv_plus 0
vconftool set -f -t int memory/appfw/app_context_db_change 1
```

The WAS database also held a **text** `true` in `WASConfigTable` row `RESERVED_18`, JSON path `$.rsp.preloadByCountry`. Save a consistent backup first. Use a transaction that guards the observed type/value before changing only this property:

```sql
BEGIN IMMEDIATE;
CREATE TEMP TABLE guard(ok INTEGER CHECK(ok=1));
INSERT INTO guard SELECT count(*)=1 FROM WASConfigTable
 WHERE name='RESERVED_18' AND json_type(value,'$.rsp.preloadByCountry')='text'
 AND json_extract(value,'$.rsp.preloadByCountry')='true';
UPDATE WASConfigTable SET value=json_set(value,'$.rsp.preloadByCountry','false')
 WHERE name='RESERVED_18';
DROP TABLE guard;
COMMIT;
```

Run with `sqlite3 -bail /opt/dbspace/.webappservice.db` in your reviewed script. The inverse expects text false and restores the original text true. Compare `json_remove(value,'$.rsp.preloadByCountry')` before/after to establish that unrelated JSON properties are unchanged. Never replace the whole database.

## 5. Runtime-only units and cached running apps

```powershell
tv exec recipes/mask-runtime.sh
```

The four units are contents-recognition-service, media-analytics-service, ipa and org.tizen.tv.sp-kpi. The system partition containing their units is read-only. `/run/systemd/system` is writable but disappears on a full restart. The existing registry flags and marker files persist.

Existing app-manager/launchpad contexts can restart processes after SIGTERM using cached auto-restart data. This happened in development. Do not repeatedly kill them or kill launchpad/app manager. After root cleanup, restart the TV so it reads the disabled flags fresh. Reapply the runtime masks and verify exact selected executable paths, not a broad process-name wildcard. In the case, none of the selected native executables started on the fresh boot.

## 6. Build the optional TV-only boot helper

This is an additional, explicit installation, not needed just to use Quiet Home. It repurposes the separate unused Gaming Hub Service slot as a headless .NET service. Obtain its original manifest and save it locally:

```powershell
tv pull /opt/usr/apps/com.samsung.tv.ghservice/tizen-manifest.xml .local/boot/original-manifest.xml
py -3.12 tools/prepare_boot.py --manifest .local/boot/original-manifest.xml --test-host $env:PC_IP
# The builder prints the private archive SHA256. Save that value, not the token.
tv push .local/boot/boot-package.tar.gz /home/owner/share/tmp/sdk_tools/boot-package.tar.gz
tv exec recipes/install-boot-test.sh boot-01 EXPECTED_ARCHIVE_SHA256
```

Download the newly created TV backup, original files and `restore-boot.sh` before continuing. The installer does not yet enable on-boot or change the developer address. It preserves the original service type, files and metadata and writes the inverse before replacing the assembly. It refuses an existing privacy installation or unknown firmware build.

For the safe pre-transition test, run the temporary PC relay in a second terminal:

```powershell
py -3.12 tools/test_relay.py --tv $env:TV_IP
```

Then, in the first terminal, save/run a TV script containing only:

```sh
app_launcher -u 5001 -s com.samsung.tv.ghservice
```

The helper waits 45 seconds in test mode so the installation root session can clean up. It then reaches SDB through the temporary PC relay while the PC address remains authorized. Allow the one-client relay to finish. After the asynchronous payload completes, inspect `/tmp/quiet-privacy-applied`, `/opt/usr/share/quiet-tizen-privacy/last-boot.txt`, the four unit states and `quiet-privacy-bridge.service`. UID 0 proof and actual masked/inactive units are required; "submitted" in a client log is not proof of completion.

The root chain uses private mount-namespace passwd/key views from the existing public root toolkit. It does not modify the TV's global passwd file or Samsung verification key. No firmware, bootloader or read-only partition is changed. The broader binary-renaming/collector-watchdog modules in the other fork are not installed.

## 7. Enable autonomous boot, then change the developer address

```powershell
tv exec recipes/enable-boot.sh boot-01
```

After the root helper reports cleanup complete, use the TV's Developer Mode menu to change **Host PC IP to 127.0.0.1**, then fully restart with the remote. The temporary PC relay is no longer used. The on-boot service roots over localhost and applies the masks. A token-authenticated relay on TCP 26103 preserves PC management. Keep the bridge token private; it is sent in cleartext on the trusted LAN.

Redefine the helper for that connection:

```powershell
function tv { py -3.12 tools/device.py --tv $env:TV_IP --pc $env:PC_IP --bridge-token-file .local/boot/bridge.token @args }
tv probe
```

The root toolkit CLI also accepts `TVROOT_BRIDGE_TOKEN` in its environment. Do not put a token in Git, a command-line argument or public output. A normal direct PC-to-TV port 26101 session no longer authenticates in localhost mode; use the proxy. PC and TV LAN addresses themselves have not changed.

## 8. Verify persistence, including separate standby/wake

Compare the current `/proc/sys/kernel/random/boot_id` with the second line of `last-boot.txt`. Check all four services for masked/inactive/PID 0, the bridge for active, the helper for no longer running, the 47 flags for disabled/no-boot/no-auto-restart, the nine marker files and the voice/consent values. Check Quiet Home, all eight grabs and the display/network/audio services.

After a full restart, separately turn the TV off with the remote, wait 30 seconds and wake it. The owner did this; the same boot ID/bridge remained and all checks passed. Do not put the TV in standby while a temporary root session is still cleaning up.

The four masks are recreated after root acquisition, not before the kernel starts. There is a short startup interval, although opt-outs and app disables remain in force. No zero-startup-window or zero-telemetry claim is made. Root proof/bridge access can also break after a firmware update; use the manual developer-address recovery path rather than disabling updates.
