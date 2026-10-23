# Samsung TV launcher

A custom home screen for a Samsung Tizen TV, plus the pieces that make the remote's **Home** button
open it. Built and used on a Samsung QE65Q60C (2023, Tizen 9.0, 32-bit ARM).

| Folder | What it is |
| --- | --- |
| `launcher/` | The launcher: a plain Tizen web app (`HDvLaunchr.Home`), no frameworks |
| `homekey/` | The Home key app (`HDvHomeKey.Go`): the Home button opens it, it opens the launcher and closes itself |
| `homecontrol/` | HomeControl: a small .NET tool that remaps the Home button on the TV |
| `launcher-deploy.ps1`, `homekey-deploy.ps1` | Build, sign and install each app over `sdb` |
| `tv.mjs` | Control the TV from a PC through its local REST API (status, open, close, YouTube) |
| `logserver.mjs` | Receives the apps' log lines on the PC while debugging |

## The launcher

Rows, top to bottom (order and visibility can be changed in Settings):

- **Continue watching:** Jellyfin resume items and Next up. OK hands playback to
  Moonfin or the Jellyfin app, at the resume position.
- **YouTube:** your Recommended feed, signed in with a code on your phone. OK opens the video in
  YouTube or TizenTube.
- **Apps:** any installed app, added, removed and arranged from Settings.
- **Live TV:** Jellyfin channels, played in Moonfin, the Jellyfin app or the launcher's own player.

Holding **Back** closes the launcher completely. Layout and settings live in the TV's localStorage.

The launcher behaves like a normal app: it never closes other apps, and its `config.xml` follows
[CleanFrame](https://github.com/nthnclrk/CleanFrame) and [arn0-tv](https://github.com/NelsonEU/arn0-tv)
(`background-support="disable"`, `prelaunch.support`).

## Why the Home button goes through a second app

With Home remapped straight to the launcher, pressing Home often left the TV's loading screen stuck
at the end, with the launcher running behind it. That happened both when the TV had closed the
launcher in the background (it does while YouTube runs) and when the launcher was only paused. Any
ordinary launch, such as opening it from the app row or a second Home press, brought it straight up.

So Home is mapped to the Home key app instead. It launches the launcher the ordinary way and exits
at once, which means every Home press starts it fresh and its one job always runs. It takes about
0.2 s. Power-on still opens the launcher directly.

## Setup

**This changes undocumented TV settings and needs root on the TV. It worked on one TV; try it at
your own risk, and keep the baseline file so you can undo it.**

### What you need

- The TV in Developer Mode (Apps, type `12345`, enter your PC's IP, restart the TV).
- [Tizen Studio](https://developer.tizen.org/development/tizen-studio/download) with its CLI and
  `sdb`, and a Samsung certificate profile. The deploy scripts use a profile named `TVLauncher`.
- A .NET SDK (6 or newer), Python 3.12 and `make`, for HomeControl and the root tool.
- Node.js, for `tv.mjs` and `logserver.mjs`.

### 1. Make it yours

- **IP addresses:** the TV is `192.168.1.46`, the PC `192.168.1.10` and the Jellyfin server
  `192.168.1.26:8096`. Change them in the deploy scripts, `tv.mjs`, `logserver.mjs`,
  `launcher/js/app.js`, `launcher/js/jellyfin.js`, `homekey/js/main.js` and `homekey/config.xml`.
- **YouTube row:** copy `launcher/js/youtube-client.example.js` to `youtube-client.js` and fill in
  the YouTube TV client ID and secret (see the comment in the file). Without it the row shows an
  error tile; everything else works.

### 2. Install both apps

In PowerShell, from this folder:

```powershell
$sdb = "$env:USERPROFILE\tizen-studio\tools\sdb.exe"; & $sdb connect 192.168.1.46:26101
```

```powershell
.\launcher-deploy.ps1
```

```powershell
.\homekey-deploy.ps1
```

Each ends with `Tizen application is successfully installed.` Running the Home key app once
should bring the launcher to the front. You can stop here and open the launcher from the app list.

### 3. Set up the root tool

HomeControl runs as root through
[levkropp/samsung-tv-root-untethered](https://github.com/levkropp/samsung-tv-root-untethered)
(commit `2b0629381b3eaf5a78f84332d630d6d4b10681e7` was used here). In Git Bash:

```bash
git clone https://github.com/levkropp/samsung-tv-root-untethered && cd samsung-tv-root-untethered && git checkout 2b0629381b3eaf5a78f84332d630d6d4b10681e7
```

```bash
py -3.12 -m venv .venv && .venv/Scripts/python -m pip install -e .
```

```bash
make common-payload
```

```bash
.venv/Scripts/samsung-tv-root doctor
```

`doctor` should show your `sdb` path and `Payload common: ready` (the other payloads are for other
models). Then check the TV side:

```bash
.venv/Scripts/samsung-tv-root archive-root probe 192.168.1.46
```

```bash
MSYS_NO_PATHCONV=1 .venv/Scripts/python -m samsung_tv_root archive-root root 192.168.1.46 --command 'id'
```

It works when you see `uid: 0` and `system files unchanged: yes`. Keep `MSYS_NO_PATHCONV=1` in
front of every root command that contains a `/usr/...` path: Git Bash otherwise rewrites it into a
Windows path and the TV answers `/bin/sh: C:/Program: No such file or directory`.

All later root commands run from this folder.

### 4. Make power-on open the launcher

```bash
.venv/Scripts/python -m samsung_tv_root archive-root root 192.168.1.46 --command '
vconftool set db/appfw/home_appid HDvLaunchr.Home;
vconftool get db/appfw/home_appid;
reboot;
'
```

After the restart the TV also names the launcher as its startup app. To see which app IDs the TV
knows, `vconftool get db/appfw/aul/appid` and `ls /opt/usr/apps/` work the same way.

### 5. Build HomeControl and push it

`homecontrol/Program.cs` has the target hardcoded (`Target = "HDvHomeKey.Go"`,
`TargetPkg = "HDvHomeKey"`). Change those if your Home key app has another ID. In PowerShell:

```powershell
cd homecontrol; dotnet build -c Release
```

```powershell
& $sdb -s 192.168.1.46:26101 push bin\Release\net6.0\linux-arm\HomeControl.dll /opt/usr/home/owner/share/tmp/sdk_tools/
```

If `sdb` says `target not found`, connect again and repeat.

### 6. Save a baseline

Read the current values (Git Bash, root tool folder):

```bash
MSYS_NO_PATHCONV=1 .venv/Scripts/python -m samsung_tv_root archive-root root 192.168.1.46 --command '/usr/share/dotnet/shared/Microsoft.NETCore.App/6.0.9/corerun /opt/usr/home/owner/share/tmp/sdk_tools/HomeControl.dll query 2>&1'
```

Copy the JSON after `SNAPSHOT=` into `homecontrol/baseline-homekey.json`, exactly as printed. The
file in this repo is what it looked like here. HomeControl refuses to change anything unless the
baseline matches the TV exactly. Push it:

```powershell
& $sdb -s 192.168.1.46:26101 push baseline-homekey.json /opt/usr/home/owner/share/tmp/sdk_tools/
```

Keep a copy off the TV: it's the way back.

### 7. Remap Home

```bash
MSYS_NO_PATHCONV=1 .venv/Scripts/python -m samsung_tv_root archive-root root 192.168.1.46 --command '/usr/share/dotnet/shared/Microsoft.NETCore.App/6.0.9/corerun /opt/usr/home/owner/share/tmp/sdk_tools/HomeControl.dll apply /opt/usr/home/owner/share/tmp/sdk_tools/baseline-homekey.json 2>&1'
```

It worked when it prints `exit code: 0` and `APPLIED=` with `"Home": "HDvHomeKey.Go"` and
`db/appfw/home_appid` set to `HDvHomeKey.Go`, while the startup values stay on the launcher.
`WARNING: ... not eligible for boot` and the `NI/HomeControl.ni.dll` and `GC_INFO` lines can be
ignored.

Don't run step 4 again after this: it would change `home_appid` back and the baseline would no
longer match.

### 8. Test

Open YouTube from the launcher, watch for a minute, then press Home once: a short loading screen,
then the launcher.

### Undo

```bash
MSYS_NO_PATHCONV=1 .venv/Scripts/python -m samsung_tv_root archive-root root 192.168.1.46 --command '/usr/share/dotnet/shared/Microsoft.NETCore.App/6.0.9/corerun /opt/usr/home/owner/share/tmp/sdk_tools/HomeControl.dll restore /opt/usr/home/owner/share/tmp/sdk_tools/baseline-homekey.json 2>&1'
```

This puts Home back to what the baseline says. Restore **before** uninstalling the Home key app,
or Home points at an app that isn't there. Going all the way back to Samsung Home means putting
`com.samsung.tv.csfs` back in place of the launcher.

## Debugging

Run `node logserver.mjs <folder>` on the PC and turn on "Send log to this PC" in the launcher's
Settings. Both apps then send their log lines to `<folder>/launcher.log`; the Home key app's lines
are tagged `[homekey]`.

## Credits

- [levkropp/samsung-tv-root-untethered](https://github.com/levkropp/samsung-tv-root-untethered) for root on the TV.
- [splitcipher/couchroot](https://github.com/splitcipher/couchroot), [CleanFrame](https://github.com/nthnclrk/CleanFrame)
  and [arn0-tv](https://github.com/NelsonEU/arn0-tv) for documenting the native Home assignment.
