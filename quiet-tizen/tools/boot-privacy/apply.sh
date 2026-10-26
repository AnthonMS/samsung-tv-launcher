set -eu
R=/opt/usr/share/quiet-tizen-privacy
test "$(id -u)" = 0
test ! -e "$R/disabled"
# The bridge restores PC management when SDB trusts localhost. No shell RPC is added.
cat > /run/systemd/system/quiet-privacy-bridge.service <<'UNIT'
[Unit]
Description=Quiet TV token-authenticated SDB bridge
[Service]
Type=simple
ExecStart=/usr/share/dotnet/shared/Microsoft.NETCore.App/6.0.9/corerun /opt/usr/share/quiet-tizen-privacy/QuietPrivacyBridge.dll
Restart=on-failure
RestartSec=5
UNIT
systemctl daemon-reload
systemctl start quiet-privacy-bridge.service
# Refuse to apply this device-specific policy after a different firmware build.
grep -Fqx 'Date=20260318_182206;' /etc/info.ini
systemctl mask --runtime --now contents-recognition-service.service media-analytics-service.service ipa.service org.tizen.tv.sp-kpi.service
systemctl reset-failed media-analytics-service.service || true
{
 echo "QUIET-PRIVACY uid=$(id -u)"
 cat /proc/sys/kernel/random/boot_id
 date -u
 systemctl show -p Id -p LoadState -p ActiveState -p MainPID contents-recognition-service.service media-analytics-service.service ipa.service org.tizen.tv.sp-kpi.service
} > "$R/last-boot.txt"
chmod 600 "$R/last-boot.txt"
echo 'uid=0 four-blocks-applied' > /tmp/quiet-privacy-applied
chmod 644 /tmp/quiet-privacy-applied
chsmack -a _ /tmp/quiet-privacy-applied
