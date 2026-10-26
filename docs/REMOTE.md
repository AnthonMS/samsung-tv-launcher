# Global remote buttons and Moonfin navigation

The launcher registers exclusive named Ecore Wayland grabs. They deliver the buttons even while another app is focused. A narrowly scoped Cynara permission is required on this firmware. There is no global keyboard logger or input polling loop.

| Physical button | Ecore name | Default |
| --- | --- | --- |
| Netflix | XF86Netflix | Moonfin |
| Samsung TV Plus | XF86KeyTVPlus | Moonfin EPG |
| Disney+ | XF86Disney | YouTube |
| Prime Video | XF86Amazon | Twitch |
| Channel Up | XF86RaiseChannel | Next Moonfin channel, wrapping |
| Channel Down | XF86LowerChannel | Previous Moonfin channel, wrapping |
| Middle channel press | XF86ChannelGuide / XF86ChannelList | Moonfin EPG |

Settings, Remote & controllers configures the four branded buttons to installed apps. The channel rocker is the explicit Moonfin action requested for this setup. Repeated key-down events do not repeatedly launch the app. Native controllers/network/settings remain available.

## Scoped permission, with a saved inverse

First inspect the actual process UID and label and query the current policy. These exact values are the Q60C observations. Save the output privately before any grant. A baseline snapshot of `/opt/var/cynara/db` and the security-manager database was also retained; do not restore either wholesale on a live updated TV.

```sh
C='User::Pkg::com.samsung.tv.bixby-promotion'
P='http://tizen.org/privilege/keygrab'
cyad --check --recursive=yes --client="$C" --user=5001 --privilege="$P" --human-readable
cyad --list-policies=MANIFESTS_GLOBAL --client="$C" --user=5001 --privilege="$P"
```

For the inspected case the exact rule was absent and effective access was denied. Prepare an inverse before this grant. If a rule already exists, preserve its real value and review it instead of erasing it.

```sh
cyad --set-policy --bucket=MANIFESTS_GLOBAL --client="$C" --user=5001 --privilege="$P" --type=ALLOW
cyad --list-policies=MANIFESTS_GLOBAL --client="$C" --user=5001 --privilege="$P"
cyad --check --recursive=yes --client="$C" --user=5001 --privilege="$P" --human-readable
```

The inverse for a newly added, still-matching rule is:

```sh
cyad --erase=MANIFESTS_GLOBAL --recursive=no --client="$C" --user=5001 --privilege="$P"
```

Guard that inverse against the exact saved after-state; do not erase a changed rule. Restart only Quiet Home after granting and inspect `remote_grabs` in `/tmp/quiet-home-state.txt`; all eight entries should be True. Then test physical keys from another streaming app. A successful API return alone does not prove focus or playback.

Never invoke `security-manager-policy-reload --help` to query permissions. It is a script that ignores that argument and changes base policies. The development incident and its limits are recorded in the journal.

## Prepare the Moonfin patch locally

The patcher accepts only the inspected original Moonfin 2.10.0 bundle SHA256:
`554ca7733a19a0ec26ac22ad296c39edefba65d5dc0ae9f1bab92e2e014f3074`.
It also checks minified navigation/player shapes and the original index tag. An app update requires new source inspection, not removing the hash guard. No stock Moonfin bundle is distributed here.

```powershell
tv pull /opt/usr/apps/MoonfinApp/res/wgt/main.js .local/moonfin/original-main.js
tv pull /opt/usr/apps/MoonfinApp/res/wgt/index.html .local/moonfin/original-index.html
py -3.12 quiet-tizen/tools/moonfin_guide_bridge.py .local/moonfin/original-main.js .local/moonfin/original-index.html .local/moonfin/patched
tv push .local/moonfin/patched/main.js /home/owner/share/tmp/sdk_tools/moonfin-main.js
tv push .local/moonfin/patched/index.html /home/owner/share/tmp/sdk_tools/moonfin-index.html
tv push .local/moonfin/patched/quiet-guide.js /home/owner/share/tmp/sdk_tools/quiet-guide.js
```

Before replacing files, create a new root-owned private TV backup folder and copy the original main/index. Preserve their hashes, ownership/mode/SMACK label and the presence/absence of any previous bridge. Prepare a hash-guarded inverse. Stop only Moonfin, copy through temporary files with `cp -a` metadata followed by `cat`/`mv`, and restore observed labels. The case used owner 201:0, mode 644 and `User::Pkg::MoonfinApp::RO`. The original index hash was `e73c36098d4d91c0c6e8003b9211750ae4f1c89a5c4fd349d5459483924bfd60`.

For a fresh matching app, save this as a reviewed TV script after preparing its inverse:

```sh
set -eu
M=/opt/usr/apps/MoonfinApp/res/wgt
D=/opt/usr/share/quiet-tizen-backups/moonfin-01
S=/home/owner/share/tmp/sdk_tools
test ! -e "$D";test ! -e "$M/quiet-guide.js"
test "$(sha256sum "$M/main.js" | cut -d ' ' -f1)" = 554ca7733a19a0ec26ac22ad296c39edefba65d5dc0ae9f1bab92e2e014f3074
mkdir -m 700 "$D"
cp -a "$M/main.js" "$D/main-before.js"
cp -a "$M/index.html" "$D/index-before.html"
app_launcher -u 5001 -k MoonfinApp.moonfin || true
for name in main.js index.html quiet-guide.js;do
 case "$name" in main.js) source="$S/moonfin-main.js";;index.html) source="$S/moonfin-index.html";;*) source="$S/quiet-guide.js";;esac
 if [ -f "$M/$name" ];then cp -a "$M/$name" "$M/$name.next";else cp -a "$M/index.html" "$M/$name.next";fi
 cat "$source" > "$M/$name.next"
 mv "$M/$name.next" "$M/$name"
done
sha256sum "$M/main.js" "$M/index.html" "$M/quiet-guide.js"
```

An inverse must guard those installed hashes, stop Moonfin, restore the two originals with `cp -a` and remove only this phase's newly created receiver. Save it on the TV before replacement.

The final main/index/receiver hashes in the case were respectively:

```text
dd28c4a355e7127b9a72de1c7fcc25f3e68b5a553d5f2d106ec723fcb77abe0e
2be04fc1f3d08dc549bf5dd6612427f00fee58f0851c9ea34cdea60a7ecfb51f
9215c49197da16ba9a2d2a28118d995f6a9f7483e19ad07c8621d6b8d086da11
```

## Why this bridge exists

Cold starts carry `quiet_destination` and a per-press `quiet_request` through `eden_resume`. Warm native launches changed requested-app-control data without reliably dispatching JavaScript's appcontrol event on this TV. A Tizen message port named `quiet_guide` delivers warm actions. Shared request IDs prevent duplicate cold/warm delivery.

The receiver accepts guide/channel-up/channel-down, waits for readiness with a bounded timeout, caches the authenticated lineup for two minutes, follows Moonfin's sort order and queues rapid steps. Up wraps last to first; Down wraps first to last. With no live item selected, Up starts at the first and Down at the last. Playback uses actual channel DTOs and Moonfin's guide/player callbacks, releasing the prior player when required.

Test two distinct videos/channels, return Home between launches, test both wrap boundaries and the centre guide press. If a channel fails manually inside Moonfin too, investigate the server/source separately. The owner confirmed channel selection attempts after the fix; all streams were then failing following a server restart. That is why final stream-delivery success is a documented pending check, not claimed as measured.
