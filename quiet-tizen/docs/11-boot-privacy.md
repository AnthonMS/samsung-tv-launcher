# TV-only boot privacy on the inspected Q60C

This optional setup was requested by the owner and tested on
TQ65Q60CAUXXC, firmware 2220.4 / Tizen 9, build 20260318_182206,
with .NET 6.0.9. It is not a universal Samsung installer.

## What persists

Forty-seven reviewed optional application records are disabled. Nine vendor
stop markers cover ten services; privacy opt-outs and voice preferences
remain off. Four other service units live on read-only system storage and
need runtime masks: `contents-recognition-service`, `media-analytics-service`,
`ipa`, and `org.tizen.tv.sp-kpi`.

`tools/boot-privacy/Program.cs` replaces the unused Gaming Hub Service IL
entry point with a headless `ServiceApplication`. Its original files,
manifest and registry values were backed up first. Both the manifest and
registry on-boot flag enable it. It acquires root once through the existing
archive-root chain and applies the four masks. Startup retries are bounded
to three minutes; there is no screen/microphone capture, input polling or
collector-killing watchdog. Quiet Home's own package is separate.

The bootstrap is adapted from the locally supplied samsung-tv-root
boot-agent, with its Unlicense retained. The signed launch archive and key
are device-private. The root chain uses private mount-namespace views; it
does not replace the TV's global passwd file or Samsung SDB verification key.
The broad telemetry modules in that fork were not installed.

## Management connection

Developer Mode must remain enabled with **Host PC IP = 127.0.0.1**. The
bootstrap connects to the local SDB daemon. A headless systemd service
`quiet-privacy-bridge.service` exposes a token-authenticated relay on port
26103, preserving PC access through the root toolkit's bridge support.
The token is private; do not print it, publish it or put it in command-line
arguments. It is sent over the trusted LAN without encryption.

Set `TVROOT_BRIDGE_TOKEN` privately before using the root toolkit CLI. The
owner's ignored exact-script helper now loads it automatically. Normal SDB
commands need the toolkit proxy or the private SDB bridge wrapper; a direct
PC connection to TV port 26101 is no longer the management route.

## Validation and recovery

The headless chain was first tested through a temporary PC relay while the
original developer address remained in place. It produced UID 0 proof and
four inactive/masked units on the actual TV. Token tests covered rejection
of wrong/short credentials and authenticated bidirectional forwarding.
A subsequent full restart, with the temporary relay removed, produced a new
boot ID and matching on-TV UID 0 proof. All four masks were applied without
a PC triggering root. Quiet Home started automatically, all eight exclusive
remote grabs survived, account connections stayed saved and display, network
and audio services remained active. The owner then tested 30 seconds of standby and remote wake. Quiet Home
returned normally; the same boot ID and bridge process remained, all four
units were still masked/inactive and none of the 47 selected native
executables was running.

Keep the PC and TV copies of the original service files, consistent database
snapshots and field-specific inverses. The owner-private session record
contains their exact paths. The persistence inverse restores the service and
on-boot flag and the original developer address, preserving the live bridge
until the root session can clean up. Reboot afterwards. Separate inverses
restore the four added voice aliases and the earlier privacy cleanup.

If an incompatible firmware update prevents the bootstrap or bridge from
starting, use the TV's Developer Mode menu to set Host PC IP back to the
actual PC address and perform a full restart, then investigate that firmware.
No firmware flashing, read-only partition changes or update disabling was used.

## Assurance limits

The four runtime masks are re-created after startup root acquisition. They
do not prevent their units from running briefly before that point. Persistent
app disables and privacy opt-outs remain in force during this interval.
No decrypted network audit or proof of zero telemetry was performed.

ACR screen-content fingerprinting can operate while a TV displays HDMI
content; [the measurement study](https://arxiv.org/abs/2409.06203) observed
this on its Samsung/LG test devices and found opt-out stopped observed ACR
traffic. This is evidence for controlling ACR, not proof of the sampling rate
or uploads on this exact TV. [Samsung describes ACR as optional](https://www.samsung.com/us/support/answer/ANS10010616/)
and ties it to Viewing Information Services. Closed firmware and third-party
streaming apps retain separate privacy boundaries.

Assistant checks cover software state: Bixby/Alexa wake-word engines,
wakeupless/VIF and speech-client aliases are disabled and their selected
executables were absent after the checks. This does not prove a microphone
is electrically disconnected or audit separate apps' microphone use.
