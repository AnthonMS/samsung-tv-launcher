# After the headless root test has succeeded. Arg: backup name.
set -eu
name=${1:?};case "$name" in ''|*[!A-Za-z0-9_-]*) exit 1;;esac
D=/opt/usr/share/quiet-tizen-backups/$name
A=/opt/usr/apps/com.samsung.tv.ghservice
test -s "$D/restore-boot.sh";test -f /tmp/quiet-privacy-applied
test "$(systemctl is-active quiet-privacy-bridge.service)" = active
sqlite3 -bail /opt/dbspace/.pkgmgr_parser.db "BEGIN IMMEDIATE;CREATE TEMP TABLE guard(ok INTEGER CHECK(ok=1));INSERT INTO guard SELECT count(*)=1 FROM package_app_info WHERE app_id='com.samsung.tv.ghservice' AND app_onboot='false' AND app_component='svcapp' AND app_disable='false';UPDATE package_app_info SET app_onboot='true' WHERE app_id='com.samsung.tv.ghservice';COMMIT;"
cp -a "$D/staged/manifest.xml" "$A/tizen-manifest.xml"
chown 201:0 "$A/tizen-manifest.xml"
chsmack -a 'User::Pkg::com.samsung.tv.ghservice::RO' "$A/tizen-manifest.xml"
if [ -f "$A/res/quiet-privacy/test-endpoint" ];then rm -f "$A/res/quiet-privacy/test-endpoint";fi
echo 'Boot trigger enabled. Wait for root cleanup, then set Developer Host IP to 127.0.0.1 in the TV menu and fully restart.'
