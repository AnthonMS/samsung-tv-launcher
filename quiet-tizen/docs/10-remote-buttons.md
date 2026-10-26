# Global remote buttons

Quiet Home uses named Ecore Wayland exclusive key grabs for branded buttons and the channel rocker on
the inspected Q60C firmware. The launcher receives these keys while another
app has focus. There is no extra root daemon or input polling loop.

| Button | Key name | Default shortcut |
| --- | --- | --- |
| Netflix | XF86Netflix | Moonfin |
| Samsung TV Plus | XF86KeyTVPlus | Moonfin TV guide |
| Disney+ | XF86Disney | YouTube |
| Prime Video | XF86Amazon | Twitch |

Settings â†’ Remote & controllers opens four configurable tiles, Bluetooth
controller pairing and a restore-defaults tile. The app picker lists the
launcher catalog first, followed by other installed, displayable UI apps.
The guide is a separate Moonfin action. Selections save atomically to
`remote-buttons.json` in the launcher's private data directory. Holding a
button does not repeatedly relaunch its app.

## Device permission

The repurposed vendor package does not declare the platform keygrab
privilege. On this inspected image, a Cynara rule scoped to its exact
process label, UID and `http://tizen.org/privilege/keygrab` enables the
grabs. It is a persistent, app-specific permission grant. It does not
require replacing the package registry or disabling a security service.

Inspect the actual process label, UID, effective permission and exact
existing policy before making changes. Save the prior rule (including
absence), an inverse and local/TV backups first. The inverse removes only
the newly added exact rule and refuses unexpected changes. Do not copy a
UID or application identity from another model without checking it.

`security-manager-policy-reload` is a maintenance script that changes base
policies even when passed `--help`. It is not a permission-query command.
Use explicit, scoped `cyad` queries and operations for this feature.

## Moonfin guide

The inspected Moonfin 2.10.0 bundle exposes its existing remote-interaction
cleanup and guide callbacks through a small guarded patch. Those callbacks
close its settings/keyboard/player and retain its normal panel restrictions.
The launcher uses Moonfin's registered `eden_resume` app control with
`quiet_destination=guide` for cold starts. It also sends the guide action to
the named Tizen message port `quiet_guide` for an already running app:
native-origin warm resumes update request data without reliably dispatching
the JavaScript `appcontrol` event on this image. The bridge accepts only `guide`, `channel-up` and `channel-down` actions. A compatible Smart Hub `PAYLOAD` envelope has empty `values`,
leaving Moonfin's existing item deep-link handler idle.

`launcher/moonfin/quiet-guide.js` handles cold and warm requests, waits only
while a guide request is pending, and stops on completion, cancellation or
25-second timeout. It waits for authentication readiness and confirms the
guide's window bar after invoking Moonfin's callback. It does not use a
sequence of navigation keys or access account tokens.

`tools/moonfin_guide_bridge.py` prepares files locally and refuses a different
bundle hash or unexpected index shape. It does not install anything. Keep
original main/index files, ownership, SMACK labels and matching rollback
hashes before deploying. Moonfin's upstream Mozilla Public License 2.0 and
existing notices remain applicable; do not publish extracted app bundles.
An app update needs a fresh source inspection and compatible bridge.

## Channel rocker

Channel Up/Down use `XF86RaiseChannel` and `XF86LowerChannel`; the middle
press accepts `XF86ChannelGuide` and `XF86ChannelList` and opens Moonfin's EPG.
These four grabs use the same scoped permission as the branded shortcuts.

The bridge gets the authenticated channel lineup through Moonfin, follows
its channel sort preference, and calls its existing guide/player callbacks.
Up selects the next channel and Down the previous one, wrapping at either
end. With no live channel selected, Up starts at the first and Down at the
last channel. Requests queue in order; a shared request ID prevents the cold
app-control and warm message-port paths from playing the same request twice.
The lineup cache expires after two minutes and follows the active API scope.

Channel selection and wrap behavior have automated tests and physical key
checks. Final stream playback must be checked against a working media server;
a failed individual stream can also fail when selected manually in the guide.

## Channel rocker

Channel Up/Down use `XF86RaiseChannel` and `XF86LowerChannel`. Middle press
accepts `XF86ChannelGuide` and `XF86ChannelList` and opens Moonfin's EPG.
The four additional grabs use the same scoped permission.

The bridge retrieves the authenticated channel lineup through Moonfin, follows
its sort preference, and calls its existing guide/player callbacks. Up selects
the next channel and Down the previous one, wrapping at either end. With no
live channel selected, Up starts at the first and Down at the last. Requests
queue in order; shared request IDs deduplicate cold/warm delivery. The lineup
cache expires after two minutes and follows the active API scope.

Ordering, both wrap boundaries, rapid presses, deduplication and cached lookup
have automated tests. Physical keys select channels and attempt playback.
Successful stream playback awaits a working media server; an individual
channel can also fail when opened manually from Moonfin's guide.

## Verification and recovery

Build the native launcher and run the catalog tests plus
`node tests/moonfin-guide.test.cjs` and `node tests/moonfin-channels.test.cjs`. Test the actual TV from another app:
check the received key, selected app ID and foreground window after enough
time to catch an original branded action stealing focus. Verify the guide
DOM or visible EPG, not just a successful app-launch return value. Check
physical buttons and standby/wake separately.

The owner's ignored `.local/SESSION.md` records exact backup paths, hashes,
installation evidence and the guarded inverse for this device. Recovery
restores the previous launcher and Moonfin code, removes the added bridge
file and exact permission rule, and preserves accounts and app data.
