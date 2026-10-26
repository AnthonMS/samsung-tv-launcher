# Manual setup, from an untouched compatible TV

This guide separates reusable steps from observations of the tested Q60C. Substitute your own TV/PC addresses and inspected values. Commands run on the PC unless a block says **TV shell script**. Root commands are fed to `/bin/sh` on the TV through the helper. Each helper invocation must finish before the next starts.

## 1. Prerequisites

Use a TV you own, the same trusted LAN, Tizen Studio's Samsung SDB, Python 3.12+, .NET SDK 8 with .NET 6 targeting support, Node.js for tests, Git and OpenSSL. On Windows, Git for Windows supplies OpenSSL. The native package-slot approach uses existing device packages, not a newly signed widget. Installing Moonfin/Jellyfin/TizenTube separately may require the Tizen certificate manager and a certificate for that TV.

```powershell
git clone https://github.com/AnthonMS/samsung-tv-launcher.git
cd samsung-tv-launcher
$env:SDB = "$env:USERPROFILE\tizen-studio\tools\sdb.exe"
$env:PATH = "C:\Program Files\Git\usr\bin;$env:PATH"
$env:TV_IP = 'YOUR_TV_IP'
$env:PC_IP = 'YOUR_PC_IP'
function tv { py -3.12 tools/device.py --tv $env:TV_IP --pc $env:PC_IP @args }
```

The checked-in root subset builds from source; it needs no Samsung binaries on the PC for the initial probe.

```powershell
py -3.12 quiet-tizen/tools/build.py payloads
& $env:SDB connect "$($env:TV_IP):26101"
tv probe
```

Enable Developer Mode on the TV first: open Apps, enter `1 2 3 4 5`, enable it and enter the PC's LAN address. Fully restart the TV, not just standby. At this stage use the PC address, not localhost. Localhost is a later, optional boot-root transition.

A successful probe reports UID/EUID 0, the runtime, protected-file checks and staging cleanup. If it fails, investigate the actual firmware. Do not force another model's declarations or disable SMACK/SELP.

## 2. Inventory the actual TV

Record the displayed software version from Settings, Support, About This TV. On this Q60C `/etc/prd_info.ini` provided an independent version check; `/etc/info.ini` provided the build date but not the displayed firmware number.

Save the following as `.local/inventory.sh` in UTF-8 without BOM and with LF line endings.

```sh
set -eu
id
cat /etc/prd_info.ini
cat /etc/info.ini
ls /usr/share/dotnet/shared/Microsoft.NETCore.App
sqlite3 -readonly /opt/dbspace/.pkgmgr_parser.db '.schema package_app_info'
sqlite3 -readonly /opt/dbspace/.pkgmgr_parser.db 'SELECT app_id,app_exec,app_component,app_onboot,app_disable FROM package_app_info ORDER BY app_id;'
vconftool get db/appfw/home_appid
vconftool get db/appfw/startup_appid
vconftool get db/sdk/develop/ip
```

```powershell
tv exec .local/inventory.sh
```

Inspect the Bixby Promotion and Gaming Hub Service manifests, file owners, SMACK labels, component types and runtime paths. The case used UID 5001, file owner 201:0, `svcapp` for the boot helper and package labels ending `::RO`. These are observations, not universal constants. This helper intentionally does not claim to verify firmware automatically.

## 3. Obtain matching private framework references

The public source is not an SDK distribution. Pull the references from your own compatible device into ignored storage:

```powershell
$refs = 'Tizen.dll','Tizen.NUI.dll','Tizen.System.dll','Tizen.Applications.Common.dll','Tizen.Applications.Service.dll'
foreach ($name in $refs) {
  tv pull "/usr/share/dotnet.tizen/framework/$name" "quiet-tizen/.local/tv-framework/$name"
}
```

The pull helper transfers bounded base64 chunks privately and verifies size and SHA256 before saving. Stock `sdb pull` was unavailable on this TV. Do not paste the encoded output into public logs. Additional references/APIs on another firmware need separate inspection.

## 4. Build and configure the launcher

```powershell
Copy-Item quiet-tizen/launcher/assets/apps.json quiet-tizen/launcher/assets/apps.local.json
Copy-Item quiet-tizen/launcher/assets/youtube-client.example.json quiet-tizen/launcher/assets/youtube-client.local.json
Copy-Item quiet-tizen/launcher/assets/media-server.example.json quiet-tizen/launcher/assets/media-server.local.json
# Edit these private files, then:
py -3.12 quiet-tizen/tools/build.py launcher
```

Discover installed app IDs on your TV; labels alone are not IDs. The public catalog is a starting example. Keep device-specific app catalogs, client credentials, server addresses and publisher artwork local. YouTube configuration requires a compatible TV/device-code client that you are entitled to use. This repo does not publish another application's client secret. Media pairing obtains fresh user tokens on each TV.

The build writes `quiet-tizen/.local/package/`: the native BixbyPromotion assembly, Probe, optional QuickLaunch and `quiet-home` assets. It copies local configuration into the private deploy package. Never upload that package without a separate audit. The launcher itself does not need a web trampoline.

## 5. Back up before replacing anything

Push the diagnostic Probe separately so the original main-operation handler can be recorded before assignment:

```powershell
tv push quiet-tizen/.local/build/Probe/Probe.dll /home/owner/share/tmp/sdk_tools/Probe.dll
```

Save `.local/snapshot.sh` with a unique snapshot folder. This is a **TV shell script**; use your own phase name and refuse an existing folder:

```sh
set -eu
D=/opt/usr/share/quiet-tizen-backups/native-01
test ! -e "$D"
mkdir -m 700 -p "$D"
sqlite3 -readonly /opt/dbspace/.pkgmgr_parser.db ".backup '$D/.pkgmgr_parser.db'"
sqlite3 -readonly /opt/dbspace/.webappservice.db ".backup '$D/.webappservice.db'"
cp -a /opt/usr/apps/com.samsung.tv.bixby-promotion "$D/bixby-before"
vconftool get db/appfw/home_appid > "$D/home-before.txt"
vconftool get db/appfw/startup_appid > "$D/startup-before.txt"
/usr/share/dotnet/shared/Microsoft.NETCore.App/6.0.9/corerun /home/owner/share/tmp/sdk_tools/Probe.dll > "$D/operation-and-sources-before.txt"
sqlite3 -readonly "$D/.pkgmgr_parser.db" 'PRAGMA quick_check;'
tar -czf /home/owner/share/tmp/sdk_tools/native-01-backup.tar.gz -C "$D" .
```

```powershell
tv exec .local/snapshot.sh
tv pull /home/owner/share/tmp/sdk_tools/native-01-backup.tar.gz .local/backups/native-01.tar.gz
```

Save stock assembly hashes, owners, modes, labels and original selectors beside the backup. Prepare the inverse in [Recovery](RECOVERY.md) before installation. Do not copy an active SQLite database with ordinary `cp`; `.backup` gives a consistent snapshot.

## 6. Install without assigning Home yet

Create a tar archive from the private package. Python's tarfile can be used on Windows without relying on shell tar:

```powershell
py -3.12 -c "import tarfile; t=tarfile.open('.local/launcher-package.tar.gz','w:gz'); t.add('quiet-tizen/.local/package',arcname='.'); t.close()"
tv push .local/launcher-package.tar.gz /home/owner/share/tmp/sdk_tools/launcher-package.tar.gz
```

The following **TV shell script** is the Q60C operation shape. Check the backup, input archive and observed metadata first. It stops only the package being replaced. A stock native-image cache, if present, must be preserved outside its execution path.

```sh
set -eu
A=/opt/usr/apps/com.samsung.tv.bixby-promotion
D=/opt/usr/share/quiet-tizen-backups/native-01
S=/home/owner/share/tmp/sdk_tools/native-package
test -d "$D/bixby-before"
test ! -e "$S"
mkdir "$S"
tar -xzf /home/owner/share/tmp/sdk_tools/launcher-package.tar.gz -C "$S"
app_launcher -u 5001 -k com.samsung.tv.bixby-promotion || true
if [ -d "$A/bin/.native_image" ];then mv "$A/bin/.native_image" "$D/native-image-before";fi
cp -a "$A/bin/BixbyPromotion.dll" "$A/bin/BixbyPromotion.dll.new"
cat "$S/BixbyPromotion.dll" > "$A/bin/BixbyPromotion.dll.new"
mv "$A/bin/BixbyPromotion.dll.new" "$A/bin/BixbyPromotion.dll"
cp -R "$S/quiet-home" "$A/res/"
chown 201:0 "$A/bin/BixbyPromotion.dll"
chmod 644 "$A/bin/BixbyPromotion.dll"
chsmack -a 'User::Pkg::com.samsung.tv.bixby-promotion::RO' "$A/bin/BixbyPromotion.dll"
find "$A/res/quiet-home" -type d -exec chmod 755 {} \;
find "$A/res/quiet-home" -type f -exec chmod 644 {} \;
chown -R 201:0 "$A/res/quiet-home"
chsmack -r -a 'User::Pkg::com.samsung.tv.bixby-promotion::RO' "$A/res/quiet-home"
app_launcher -u 5001 -s com.samsung.tv.bixby-promotion
```

Run the saved script with `tv exec .local/install-native.sh`. If installation fails partway, inspect actual state and the backup. Do not delete the backup to make a retry succeed. If a second resource directory already exists, stop and prepare an update plan instead of nesting copies inside it.

Test the visible interface, arrows/OK/Back, apps, volume/mute, native settings and HDMI on the actual device. Read `/tmp/quiet-home-state.txt`; its `build_ms` measures scene construction, not input-to-photon latency.

## 7. Assign Home and startup after accepting the trial

Save `.local/assign-home.sh`:

```sh
set -eu
/usr/share/dotnet/shared/Microsoft.NETCore.App/6.0.9/corerun /home/owner/share/tmp/sdk_tools/Probe.dll set-home com.samsung.tv.bixby-promotion
vconftool set -f -t string db/appfw/home_appid com.samsung.tv.bixby-promotion
vconftool set -f -t string db/appfw/startup_appid com.samsung.tv.bixby-promotion
vconftool get db/appfw/home_appid
vconftool get db/appfw/startup_appid
```

```powershell
tv exec .local/assign-home.sh
```

Wait for the helper to finish cleanup, then perform a full restart with the remote. Test Home from YouTube/Moonfin, a fresh boot and standby/wake separately. Preserve all three original selectors for undo. The optional upstream QuickLaunch/sidebar replacement is not part of this Q60C setup; native `org.tizen.menu` must remain available.

## 8. Add the remaining features one stage at a time

Configure [media](MEDIA.md), then [remote shortcuts](REMOTE.md), then [privacy](PRIVACY.md). Back up before each stage and verify responsiveness after it. Do not rerun an old cleanup profile or overwrite another TV's registry with this TV's backup. The older UN75U8000F documentation is reference material for a different device.

## Updating code later

Build locally, save the current TV assembly/assets and hashes in a new phase, prepare an inverse, stage the new package, stop only Quiet Home, copy with preserved metadata, refresh only a confirmed idle preloader if needed, and start Quiet Home. Keep accounts and preference files in the package's data directory. Home should return to the first dock tile. Re-test one feature at a time.

The idle .NET preloader cached an old DLL during development. Refresh only an executable whose `/proc/PID/exe` and NUL-decoded command line match `/usr/bin/dotnet-loader 0`; never kill every dotnet process. Exact operation and failed attempts are recorded in [the journal](JOURNAL.md).
