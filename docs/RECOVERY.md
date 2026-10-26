# Recovery and undo

Keep consistent snapshots and original files both on the PC and TV. A snapshot is evidence and a source for original field values, not permission to overwrite a live registry wholesale. Use the inverse prepared for the exact phase. Fail on drift instead of undoing an app update by accident.

## Lost management connection

In boot-root mode, Developer Host IP must be localhost. Use the private token bridge, not a direct PC SDB session. If an incompatible firmware/helper prevents the bridge from starting, open the TV's Developer Mode dialog, set Host PC IP back to the actual PC address and fully restart. The headless helper then fails its localhost check quietly; direct PC management is restored. No factory reset is required for that recovery route.

## Remove boot persistence only

Using the still-working bridge, pipe the saved TV inverse into sh with its absolute backup path:

```sh
set -- /opt/usr/share/quiet-tizen-backups/boot-01
cat "$1/restore-boot.sh" | /bin/sh -s -- "$1"
```

The inverse checks the installed helper hash, stops only that helper, restores its original assembly/runtime/manifest and original on-boot flag, and writes the original developer address from the saved record. Keep the running bridge alive until the root session can clean up. Restart afterwards. The runtime bridge disappears on restart, with direct PC SDB access restored. Inert backup/resources can be cleaned up later after verification.

This does not automatically re-enable the privacy components. That separation lets you keep the consent choices and app disables while removing autonomous root.

## Undo privacy

Restore selected preference values with their original types. Restore only the guarded WAS JSON field. Run the generated per-group `restore.sh` plans against the still-matching disabled flags. For each vendor marker, remove it only if this phase created it and it still matches the recorded file; preserve pre-existing markers. Restart only services recorded active before the change.

Unmask the four runtime units only when undoing that policy:

```sh
systemctl unmask --runtime contents-recognition-service.service media-analytics-service.service ipa.service org.tizen.tv.sp-kpi.service
```

After a reboot those mask symlinks may already be absent. A correct inverse accepts absence, refuses an unexpected replacement and avoids deleting somebody else's unit. If boot privacy remains enabled, it will reapply masks next boot, so undo that layer first when removing the entire policy. Consent controls should be changed in the TV UI deliberately, not restored from another account's database.

## Restore Moonfin

Stop Moonfin, guard its current main/index/receiver hashes against the installed set, restore your matching original main/index with their saved metadata, and remove only the bridge file that this phase created. Keep the app's account data. On the development TV, the latest guide/player correction had to be rolled back to its immediate predecessor before older hash-guarded inverses would accept it. Chain inverses in reverse installation order.

Do not publish or install an extracted Moonfin bundle from somebody else's TV. An app update changes the baseline; take a new backup and inspect the new source instead of forcing an old inverse.

## Undo remote permission

Stop Quiet Home to release exclusive grabs. If the exact keygrab rule was absent before this work and still equals the saved new rule, erase only that client/user/privilege entry as described in REMOTE.md. If the original rule existed, restore its actual original value. Do not reload all base policies or disable security services.

## Restore Home/startup and stock package

Use the three original values saved before assignment: main-operation application, `db/appfw/home_appid` and `db/appfw/startup_appid`. Do not assume another TV's stock app ID. On a TV that previously used the web trampoline, these originals may be custom values.

```sh
# Substitute the ACTUAL saved values, not these placeholders.
/usr/share/dotnet/shared/Microsoft.NETCore.App/6.0.9/corerun /home/owner/share/tmp/sdk_tools/Probe.dll set-home ORIGINAL_MAIN_APP
vconftool set -f -t string db/appfw/home_appid ORIGINAL_HOME_APP
vconftool set -f -t string db/appfw/startup_appid ORIGINAL_STARTUP_APP
app_launcher -u 5001 -k com.samsung.tv.bixby-promotion || true
```

Then restore the saved original Bixby Promotion assembly and any displaced native-image directory, with original owner/mode/SMACK label. Guard the current installed hash before copying. Restore only resources introduced by that phase; never clear the account data directory as a repair shortcut. Let root cleanup finish before restarting.

## A deployment failed halfway

Read actual files, hashes and flags first. A shell can finish earlier operations before returning a failure. Keep the first backup. Do not delete the backup/stage guard just to rerun. Review the inverse and restore only the completed changes. If startup still opens a working Quiet Home, avoid unrelated resets. Factory reset is a last resort that loses configuration/accounts, not the normal recovery procedure.

## Private records from this installation

The owner's migrated `quiet-tizen/.local/SESSION.md` gives the phase names, observed hashes and exact rollback order, including privacy-20, boot-21 and the channel bridge revisions. Raw command outputs, scripts, local packages, stock binaries and signing material remain under ignored `.local/` paths. The public command index lists retained work items without publishing their contents.
