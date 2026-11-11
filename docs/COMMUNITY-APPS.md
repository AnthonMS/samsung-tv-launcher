# Community Apps and Samsung Apps

Quiet Home development version 0.4, November 2026. Managed updates and automatic recovery are installed and tested on the owner's Q60C. Owner daily testing is still required before publication. Community Apps is part of the native launcher. It does not launch or require Apps2Samsung or TV-Store. An in-process installer downloads, checks, signs and installs packages through the TV's localhost developer connection. No new LAN installer service or SDB relay is opened.

## Daily use

Open **Settings > More settings > Community Apps**. Use the remote to choose:

- **Browse**: a four-column grid of app cards, up to 12 per screen. Curated apps use local artwork; other cards use a colored initial. Down past the last row loads the next screen automatically; Up from its first row returns to the previous screen in the same column.
- **Variants**: same-name packages share one card. Select it to compare release filename, source and package identity before opening details. Different variants are not automatically treated as updates.
- **Search**: enter words with the TV keyboard and choose Search. Matches include name, description, source, category and package filename/identity. Clear search restores the full selected view.
- **Installed**: user-installed launchable apps. OK opens the app; Blue adds it to the dock.
- **Updates**: verified newer versions or changed builds from the matching recorded source and variant, plus compatible integration-adapter updates. Different sources, alternative variants, older releases and unknown build provenance receive separate labels. Cards and details show installed and available versions. Refresh checks the current catalog. There is no automatic bulk update.
- **Sources**: add, enable, disable or remove a source. Removing a source keeps its installed apps.
- **Refresh**: reload available packages and installed applications.

After installation, use Open app or Add to dock in its details. Newly installed apps appear in the Apps library without rebuilding the launcher. Discovered apps use a generated icon; curated apps retain their existing artwork. Dock order is preserved and discovered apps are not automatically pinned.

The **Samsung Apps** dock tile opens Samsung's official app store directly. Home returns to Quiet Home. The tile can be moved or hidden using the existing dock editor.

## Sources

The default community catalog is downloaded from Apps2Samsung's public catalog endpoint, with its signature checked against a bundled public key. This is a data source, not an installed app dependency. Other sources can be added from the native screen without a PC:

1. **Signed repository**: enter its HTTPS `repository.json.signed` URL. Quiet Home obtains the publisher key through verified HTTPS, validates the signature, and shows the source identity and key fingerprint before asking to add it. Subsequent refreshes use the pinned key and accepted timestamp.
2. **GitHub releases**: enter `owner/repository` or a GitHub repository/releases URL. The newest non-draft, non-prerelease release is examined for supported package assets. A release must provide a SHA-256 asset digest to install through this source type. Different package variants remain individually selectable.
3. **Direct package**: enter a public HTTPS `.wgt` or `.tpk` URL. This is a manual file source. It does not promise automatic release tracking. The package identity is checked after download; transport uses normal HTTPS certificate validation.

Select Edit address to use the TV keyboard. Back closes the keyboard; then choose Check source and review the result. A trusted signing key identifies a publisher, not a review of the app's quality.

A repository containing only source code is not an installable package. Android APKs, packages for another CPU, unsupported Tizen versions and apps needing an additional root/configuration procedure are not converted automatically. An unavailable source does not prevent other verified sources from loading. Adult-marked catalog entries are omitted in this first version.

## Updates and preserved customizations

Package identity, installed author certificate and signing profile are checked before an update. A different source, different installed app under the same ID, downgrade or certificate mismatch does not trigger automatic uninstall/reinstall. App data is kept. Installation is considered complete only after the TV's reply and installed-app/version readback agree. Interrupted jobs retain a pending record and block blind retries until reconciled.

Compatible app updates use their original package ID and a verified matching signing profile. The first store version temporarily blocked modified apps. Version 0.2 replaces those blanket blocks with launcher-owned compatibility adapters:

| App | Update path | What is preserved |
| --- | --- | --- |
| Moonfin | Official packaged releases through Community Apps | Guide/message-port bridge, channel controls and completion-based seek reporting for the newer player |
| TizenTube | Official packaged releases through Community Apps | A small service hook loads the separately installed Quiet Home playback runtime |
| Samsung YouTube | Samsung Apps and Samsung's normal update mechanism | Quiet Home supplies playback arguments at launch; the app's original configuration is retained |
| Jellyfin / KickTV / Twitch | Compatible packaged releases through Community Apps | Current integration does not require a persistent package patch; app changes can still require provider maintenance |

The adapters inspect the downloaded layout and callback structure before changing it. An unfamiliar or ambiguous structure produces a compatibility error before installation. Sources cannot supply executable patch scripts. Downloads retain their upstream digest; receipts separately record the adapter version. Updating the launcher adapter can offer an integration update even if the upstream app version has not changed.

For Moonfin, the managed adapter has been exercised locally against official 2.10, 2.11 and 3.0 packages. A real TV upgrade from 2.10 to 3.0 retained the login and opened the patched guide. Newer Moonfin player code needed an additional seek-reporting correction. Actual pause, paused seek and resumed progress now pass through Home Assistant. This does not establish support for every future Moonfin build.

The separately provisioned Cobalt runtime and its private observation key live outside TizenTube's replaceable package. Updates add only the checked bootstrap hook to the upstream service before signing. No observation key is embedded in the update archive. Existing TizenTube/YouTube launch configuration and registry fields can remain stock because Quiet Home already supplies mode-specific launch arguments. Samsung's native Cobalt engine remains firmware-managed; the third-party store does not replace it.

On an uncertain same-version managed installation, matching package/version metadata alone cannot establish that the adapter was installed. The pending record retains the previous version and native completion acknowledgement. Integration checks run before clearing a recoverable managed record. A confirmed installed version and author identity are still required.

Managed Moonfin and TizenTube replacements now use a transaction. Before installation the TV verifies that its retained working package matches the installed files, saves that package and the entire app writable tree, and records a durable journal. If there is insufficient space or a safe snapshot cannot be made, it refuses the update before replacement.

After Samsung's installer finishes, Quiet Home checks installed package bytes and integration readiness. A detected installation or integration failure reinstalls the saved package through the native installer and restores the saved app data and receipt. TizenTube verification stops the exact old proxy worker before checking the new worker, preventing a stale process from falsely certifying a replacement. Interrupted transactions are reconciled when the launcher starts. Unresolved recovery keeps its backup and blocks further updates instead of guessing.

Verified success deletes the temporary snapshot and previous package. The currently installed signed package remains as the baseline needed for the next update. A successful rollback also removes the resolved temporary snapshot. Recovery never restores a whole TV database. This automatic protection applies to these two managed apps, not every arbitrary third-party package or Samsung firmware update.

A silent on-screen notice reports success or a failure/recovery result. The store job also retains its status. Verification covers package integrity and the implemented readiness checks; it cannot automatically detect every visual or behavioral regression in a future upstream app. Unknown layouts are rejected before installation. A later human-discovered app bug after a verified successful update has no retained old-version snapshot for one-click downgrade.

Known-complete uploads are truncated through the existing SDB sync channel, without opening an arbitrary shell.

Other normal compatible apps use the general installer. Root-required packages and packages needing injected configuration remain outside the ordinary install path. A Partner profile is used where the catalog/manifest requires it; importing profiles does not grant unsupported platform privileges. Launcher/bootstrap/Cobalt/system packages and Samsung's signed YouTube app cannot be replaced through the third-party installer.

## Signing setup

Signing profiles are private launcher data, provisioned once during maintenance. The current owner profiles were imported without changing their author identity. Daily operations do not require a secondary device or Developer Mode IP changes. The localhost Developer Mode host is preserved.

The standalone local preparation tool accepts profile folders containing `author.p12`, `distributor.p12` and `password.txt`:

```powershell
py -3.12 tools/community_profiles.py --profile-dir PATH_TO_PROFILE --output .local/community-signing
```

It requires Python cryptography and only prepares a private local bundle. It does not contact a TV. Profiles can come from existing signing tools; no particular external application is required. A protected maintenance transfer places them in the launcher's private data directory. Certificates, passwords and profile bundles must never be committed or included in public releases.

Certificate issuance and renewal are not automated in version 0.1. An expired profile needs maintenance. The installer does not create a different author identity as a workaround.

## Implementation and boundaries

- `Home.Store.cs`: native screens, remote navigation, keyboard, installed-app inventory and read-only operational status.
- `CommunityApps/StoreCatalog.cs`: verified HTTPS, signed repositories, GitHub releases and public catalog parsing.
- `CommunityApps/StorePackage.cs`: ZIP/manifest validation, profile loading and installed certificate metadata.
- `CommunityApps/StoreClient.cs`: bounded installation job, adapter-aware receipts, retained packages and interrupted-job reconciliation.
- `CommunityApps/ManagedUpdates.cs`: owned Moonfin/TizenTube transformations, checked layouts and extension version identity.
- `CommunityApps/LocalInstaller.cs`: short-lived localhost SDB session. No root shell, uninstall workaround, global signing-key replacement or broad network relay.
- `LibraryInventory.cs`: curated/discovered app merge, system-tool exclusion and Samsung Apps entry.
- `CommunityApps/Vendor/`: adapted MIT package-signature implementation, license and provenance. No TV-Store code is included.

Downloads allow public HTTPS hosts only. Redirects are revalidated and connections use checked public DNS addresses. Catalogs have size/time bounds; package and expanded ZIP sizes are bounded, paths/symlinks/duplicates checked, and signed catalog package hashes verified. The first implementation caps downloads at 96 MB and expanded content at 160 MB.

The existing authenticated QuietThings API adds a read-only `/community/status` route for maintenance observation. There is no remote installation endpoint. Existing HA control/playback routes are retained.

## Acceptance status

Local checks: 124 Community Apps checks, including signed-repository security vectors, ZIP/package checks, cryptographic signatures, imported profile loading, source affinity, disabled/removed/failed/expired-source installation gates, dock behavior and simulated SDB completion/refusal/zero-ID-close cases. The existing 315 API contract checks and catalog/media/remote tests also passed. Another 35 managed-package checks cover actual upstream package transformations and update state, with 18 generated guide/channel callback checks and 10 actual-completion seek checks. Launcher builds have zero warnings/errors.

Actual TV checks completed during development:

- The TV's .NET 6 runtime passed the initial 68 core/signature/catalog/simulated-transport checks.
- The native launcher loaded 239 verified package variants and both imported signing profiles.
- A small clock app installed through native confirmation, then reinstalled in place with the same version and retained test data. This is not a claim of a different-version upgrade test.
- GitHub source add/disable/enable/remove and independent signed-repository review/add/remove passed through the native screens.
- Add to dock, Samsung Apps dock launch, actual Samsung-store foreground and Home return passed.
- The final build passed a full restart with Quiet Home as startup, both account connections, all eight remote grabs, localhost Developer Mode and four privacy masks retained.
- A further native in-place clock installation after that reboot completed with no pending job and preserved its test data.

The disposable clock was removed after acceptance. Temporary installer/test staging was cleaned; recovery backups and private signing profiles were retained. Owner visual acceptance remains pending. Owner testing should cover keyboard entry, layout/readability, navigation and the apps they choose to install. Generic native TPK installation, large packages, prolonged memory pressure and fully autonomous certificate renewal are not hardware-certified by the clock test.

## Recovery

Use the fresh feature-specific guarded inverse prepared for this installation. It restores the pre-store launcher and removes only the known new app-local dependencies and catalog key. Private store profiles/receipts are retained for recovery and future data-safe updates. The complete playback inverse chains the feature rollback with the previous playback recovery layers. Startup-policy recovery remains independent.

Exact device paths, installed hashes, backups, command records and credentials belong in ignored local operator material, not public guides. Never rerun an old installer over a newer launcher without its current hash guards.

## Managed-update acceptance

The actual Moonfin 2.10 to 3.0 upgrade retained login, guide integration and channel inventory. Corrected pause, paused seek and resume/progress passed in both Home Assistant media entities. Production Moonfin 3.0 and TizenTube 2.1.1 same-version managed replacements passed the transactional success path with no pending journals and no temporary recovery snapshots left. TizenTube was already the latest inspected release; its replacement is not described as a different-version upgrade.

A disposable owner-signed app proved native 1.0 to 2.0 to 1.0 installation without uninstall. The recovery test saved its original package and data, performed an upgrade and simulated data migration, then restored the old package and original data while removing the new-schema file. Separate success and repeated-finish checks proved cleanup and idempotence. Fault and interruption tests cover coordinator ordering and recovery boundaries.

The managed build passed full restart with automatic Quiet Home startup, localhost Developer Mode, both sign-ins, signing profiles, remote grabs and privacy settings retained. YouTube and TizenTube transport checks passed after externalizing the runtime. These are development acceptance results on one Q60C, not a guarantee for every app release, TV or firmware. Final private evidence and exact current revision are maintained in the operator handoff.


### November 11 grid and update-policy acceptance

The installed development catalog contained 242 package variants grouped into 150 app cards. Actual remote navigation passed forward/backward automatic paging, Browse/Installed/Updates, variant selection, details, Back and entry to the native search keyboard. Pure search matching and page-boundary checks pass locally. Owner typing with the TV keyboard and visual design acceptance are deferred; no screenshot-based visual certification is claimed.

The corrected Updates view showed two actual groups: KickTV 2.2.2 to 2.3.0 and Moonlight 1.14.0 to 1.20.0. These are an observed catalog snapshot, not a permanent update list. No apps were updated during this UI task. Package receipt provenance and installed package version take precedence over a different catalog hash alone.
