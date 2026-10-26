# Fresh inspected Q60C only. Args: unique backup name, staged archive SHA256.
set -eu
name=${1:?};digest=${2:?}
case "$name" in ''|*[!A-Za-z0-9_-]*) exit 1;;esac
D=/opt/usr/share/quiet-tizen-backups/$name
A=/opt/usr/apps/com.samsung.tv.ghservice
R=/opt/usr/share/quiet-tizen-privacy
T=/home/owner/share/tmp/sdk_tools/boot-package.tar.gz
grep -Fqx 'Date=20260318_182206;' /etc/info.ini
test "$(sha256sum "$T" | cut -d ' ' -f1)" = "$digest"
test ! -e "$D";test ! -e "$R";test ! -e "$A/res/quiet-privacy"
test "$(sqlite3 -readonly /opt/dbspace/.pkgmgr_parser.db "SELECT count(*) FROM package_app_info WHERE app_id='com.samsung.tv.ghservice' AND app_component='svcapp' AND app_onboot='false' AND app_disable='false';")" = 1
mkdir -m 700 "$D"
sqlite3 -readonly /opt/dbspace/.pkgmgr_parser.db ".backup '$D/.pkgmgr_parser.db'"
cp -a "$A/bin/com.samsung.tv.ghservice.dll" "$D/original.dll"
cp -a "$A/bin/com.samsung.tv.ghservice.runtimeconfig.json" "$D/original.runtimeconfig.json"
cp -a "$A/tizen-manifest.xml" "$D/original-manifest.xml"
vconftool get db/sdk/develop/ip > "$D/developer-ip-before.txt"
mkdir "$D/staged";tar -xzf "$T" -C "$D/staged"
# Record the expected installed hash before replacement, for field-specific undo.
sha256sum "$D/staged/com.samsung.tv.ghservice.dll" | cut -d ' ' -f1 > "$D/installed-hash.txt"
cat > "$D/restore-boot.sh" <<'RESTORE'
set -eu
D=${1:?Pass the absolute backup folder}
A=/opt/usr/apps/com.samsung.tv.ghservice
test "$(sha256sum "$A/bin/com.samsung.tv.ghservice.dll" | cut -d ' ' -f1)" = "$(cat "$D/installed-hash.txt")"
app_launcher -u 5001 -k com.samsung.tv.ghservice || true
sqlite3 -bail /opt/dbspace/.pkgmgr_parser.db "BEGIN IMMEDIATE;CREATE TEMP TABLE guard(ok INTEGER CHECK(ok=1));INSERT INTO guard SELECT count(*)=1 FROM package_app_info WHERE app_id='com.samsung.tv.ghservice' AND app_component='svcapp' AND app_disable='false';UPDATE package_app_info SET app_onboot='false' WHERE app_id='com.samsung.tv.ghservice';COMMIT;"
cp -a "$D/original.dll" "$A/bin/com.samsung.tv.ghservice.dll"
cp -a "$D/original.runtimeconfig.json" "$A/bin/com.samsung.tv.ghservice.runtimeconfig.json"
cp -a "$D/original-manifest.xml" "$A/tizen-manifest.xml"
PC=$(sed -n 's/^.*value = //p' "$D/developer-ip-before.txt" | sed 's/ (String).*$//')
test -n "$PC"
vconftool set -f -t string db/sdk/develop/ip "$PC"
echo 'Original service/developer address restored. Leave the bridge alive until root cleanup; reboot afterwards.'
RESTORE
mkdir -m 755 "$R"
cp "$D/staged/bridge.token" "$R/bridge.token";chmod 600 "$R/bridge.token"
cp "$D/staged/QuietPrivacyBridge.dll" "$R/QuietPrivacyBridge.dll";chmod 644 "$R/QuietPrivacyBridge.dll"
chsmack -a User "$R" "$R/bridge.token" "$R/QuietPrivacyBridge.dll"
cp -a "$D/staged/resources" "$A/res/quiet-privacy"
chown -R 201:0 "$A/res/quiet-privacy"
find "$A/res/quiet-privacy" -type d -exec chmod 755 {} \;
find "$A/res/quiet-privacy" -type f -exec chmod 644 {} \;
chsmack -r -a 'User::Pkg::com.samsung.tv.ghservice::RO' "$A/res/quiet-privacy"
app_launcher -u 5001 -k com.samsung.tv.ghservice || true
cp -a "$A/bin/com.samsung.tv.ghservice.dll" "$A/bin/com.samsung.tv.ghservice.dll.new"
cat "$D/staged/com.samsung.tv.ghservice.dll" > "$A/bin/com.samsung.tv.ghservice.dll.new"
mv "$A/bin/com.samsung.tv.ghservice.dll.new" "$A/bin/com.samsung.tv.ghservice.dll"
cp -a "$D/staged/com.samsung.tv.ghservice.runtimeconfig.json" "$A/bin/com.samsung.tv.ghservice.runtimeconfig.json"
chown 201:0 "$A/bin/com.samsung.tv.ghservice.runtimeconfig.json"
chsmack -a 'User::Pkg::com.samsung.tv.ghservice::RO' "$A/bin/com.samsung.tv.ghservice.runtimeconfig.json"
echo 'Installed test helper only. Boot flag and developer IP remain unchanged. Download this backup before continuing.'
