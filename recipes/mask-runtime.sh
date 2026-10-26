set -eu
systemctl mask --runtime --now contents-recognition-service.service media-analytics-service.service ipa.service org.tizen.tv.sp-kpi.service
systemctl reset-failed media-analytics-service.service || true
systemctl show -p Id -p LoadState -p ActiveState -p MainPID contents-recognition-service.service media-analytics-service.service ipa.service org.tizen.tv.sp-kpi.service
