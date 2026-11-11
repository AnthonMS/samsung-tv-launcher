# App stores in Quiet Home: research and execution plan

Status: implemented and installed for owner testing on 10 November 2026. The owner subsequently authorized local development, TV installation and automated checks. See [Community Apps](COMMUNITY-APPS.md) for the delivered behavior, acceptance and limitations. The design notes below preserve the original research; proposals are not a statement that every optional feature shipped. No commits, pushes or PR work were authorized for this release.

## Recommendation

The implemented workflow is: open **Community Apps** in Settings, browse a remote-friendly list, install/update compatible Tizen packages and manage sources from the TV. Add a separate **Samsung Apps** tile to the dock for the official store. Preserve the current Developer Mode host at localhost.

Owner clarification: Community Apps is a native feature of Quiet Home, not a shortcut to an installed third-party store. Neither Apps2Samsung nor TV-Store is a runtime or installation prerequisite. Their source is research material for package formats, signing and local installation. Build our own installer component and native interface; selectively reuse code only where rights and compatibility are established. Prove our own minimal package-install path before the full UI. No standalone-store deployment milestone.

An arbitrary GitHub project is not necessarily installable. It needs a compatible packaged release or a supported manifest describing one. Source-only projects, Android APKs, incompatible Tizen versions, wrong CPU builds and unsupported setup requirements remain outside the ordinary installer.

## Established findings

TV-Store documents TV-side catalog browsing, signing and installation with localhost Developer Mode authorization. Independent signed repositories are supported, but enrolling a new arbitrary source starts on a phone/PC. Updates depend on retaining the original signing identity; mismatch can require reinstall and data loss. Some catalog entries remain PC/phone-only. These are gaps to address explicitly. [TV-Store documentation](https://github.com/Apps2Samsung/TV-Store)

Reviewed snapshot: commit `55881a56a00e5defacf37d2eaa9ba049fbb0181c`, manifest version 0.3.26. Its Node service implements download, catalog/source validation, signing and SDB installation. Service port is 8077; its optional broad LAN SDB relay defaults to 26201. Our authenticated management relay uses 26103. Different listen ports do not establish compatibility because both installers and maintenance ultimately share SDB. [Service source](https://github.com/Apps2Samsung/TV-Store/blob/55881a56a00e5defacf37d2eaa9ba049fbb0181c/tizen-app/service/index.js), [Connection targets](https://github.com/Apps2Samsung/TV-Store/blob/55881a56a00e5defacf37d2eaa9ba049fbb0181c/tizen-app/service/sdbhosts.js)

The downloader disables TLS certificate verification and instead relies on signed catalog content plus package digests. Do not extend that transport to arbitrary unsigned GitHub/package sources and treat it as authenticated. Preserve signed-repository verification. New source enrollment needs verified HTTPS or a trusted fingerprint supplied independently. A key fetched through an unverified connection cannot establish its own authenticity. [Downloader implementation](https://github.com/Apps2Samsung/TV-Store/blob/55881a56a00e5defacf37d2eaa9ba049fbb0181c/tizen-app/service/index.js), [Repository specification](https://github.com/Apps2Samsung/tizen-community-packages/blob/main/docs/repositories.md)

The inspected TV-Store tree has no top-level LICENSE file. Do not assume the main Apps2Samsung application's license automatically covers this separate repository. Verify applicable reuse rights before copying or redistributing engine code. No code reuse is assumed or required. Our implementation can use the documented protocols independently. No fork or public publication was created.

## Local code findings

- `Home.cs` has twelve Settings tiles in four columns and three rows. A thirteenth unchanged would be off-screen. Redesign the grid or add pagination/scrolling, including focus and Back behavior.
- `AppLibrary.cs` loads packaged `assets/apps.json` at launcher creation. `AppRowLayout.cs` offers only that catalog. New installations will not automatically appear in our launcher today.
- `Home.Remote.cs` already enumerates installed applications through ApplicationManager. Reuse that approach for dynamic inventory.
- `Home.Media.cs` demonstrates native text fields and the Samsung input method, useful for repository URL entry.
- `Launch` uses native app control. Saved device evidence shows the native store executable under `com.samsung.tv.store`. This supports trying it later, but is not a new launch/return acceptance test. `org.volt.apps` is only a historical alias candidate.
- Localhost belongs to this TV's existing root/bootstrap management design. HA playback does not inherently require that Developer Mode setting on every Samsung TV. Preserve it here.

## User interface

Settings tile: **Community Apps**, subtitle **Install apps and manage sources**.

Sections: Browse, Installed, Updates and Sources. Browse uses a vertical list with categories/search and a detail panel: description, publisher/source, compatible version, installed version and Install/Update/Open. Completion offers Open and Add to dock. Show real operation stages and reconcile package inventory after an unknown install outcome before offering Retry.

Sources supports Add, disable/enable, remove, refresh and verification/error status. Removing a source leaves its installed apps in place. Proposed source types, delivered in order:

1. Signed repository manifest URL, with publisher/key review and pinned identity. Existing unverified transport requires an independently trusted fingerprint. URL-only enrollment needs verified TLS first.
2. GitHub owner/repository or release URL with packaged `.wgt`/`.tpk` assets. Resolve compatible releases, ask about ambiguous assets, and retain repository/asset identity for updates. Verify HTTPS including redirects. Do not compile repositories or execute their scripts on the TV.
3. Optional direct package URL, with manifest identity/version review. Without a release feed this is a manual package source, not an automatic update subscription.

Dock tile: **Samsung Apps**, a normal selectable/reorderable entry launching the installed native store. Keep Quiet Home running and preserve Home/startup selectors. No long-Back workaround.

## Architecture

Native NUI page -> authenticated local installer API -> download/verify/sign/stage -> localhost SDB install -> package-manager readback -> refreshed launcher inventory.

The UI handles navigation, lists, URL entry and owner decisions. A dedicated service owns signing profiles, staging and one persistent job at a time. No signing keys in launcher assets, JavaScript pages, logs, HA attributes or AI memory. Reuse existing profiles for in-place app updates. Initial profile enrollment and later certificate renewal may need a PC; routine installs must not.

Our installer API belongs to Quiet Home and does not connect to an installed TV-Store or use its private page token. If a separate process is required by platform permissions, deploy it as our own companion component. Design a paired launcher-client key provisioned into private data and authenticate every mutation/certificate operation. Prefer loopback binding if the intended clients can reach it on this TV; otherwise narrow access and verify both IP families. Leave its optional broad LAN SDB relay disabled and preserve our token-authenticated relay.

Serialize installation and maintenance so store jobs do not overlap SDB/root maintenance or boot acquisition. Never hold an idle SDB connection. Permit cancellation before installation submission; after submission wait for completion/readback. If the engine fails, diagnose before choosing a narrow root helper. Direct web-service `wascmd` execution is not the initial path: upstream reports service crashes/denials on Tizen 9. This is a documented concern, not a device result from this task. [Installer notes](https://github.com/Apps2Samsung/TV-Store#how-it-works)

Do not silently change an installed app's source, package ID or signing identity. Certificate mismatch and ID collision must be visible; never automatically uninstall as a workaround. Protect launcher/bootstrap/bridge/Cobalt infrastructure from ordinary bulk updates/removal. Initially offer selected manual app updates. Updates to media apps may alter the IDs/markup used by playback adapters and need an HA regression check.

## Execution after owner requests implementation

### 1. Offline preparation

Define our own installer module/service boundary and pin any required libraries. Check reuse rights for any selected reference code. Define native list/details/Sources pages, Settings layout, job API and source rules. Prepare synthetic packages/manifests, protected package IDs and targeted rollback. Identify existing signing profiles locally without printing private material.

### 2. Compatibility checkpoint

Arrange maintenance after owner playback testing. Take fresh inventory/backups. Confirm exact model/firmware, profiles, free storage, native store app ID, localhost SDB, Node runtime and app-to-service access. Drain other sessions. Do not change Developer Mode host to make a prototype pass.

### 3. Isolated installation proof

Deploy our own minimal installer proof through the current secured management channel. Determine secure profile import into its private storage using existing signing identities. No Apps2Samsung installation, TV-Store package or desktop Arm operation is required. Start with localhost SDB and leave direct web-service `wascmd` and broad LAN relay out of the initial design. Install one disposable test app, verify package/version/launch, then update it with the same author and verify retained data. Diagnose any failure before the full UI.

### 4. Launcher navigation and inventory

Add Community Apps and Samsung Apps entries. Refresh inventory after changes and when returning from either store. Merge installed launchable apps with curated art/aliases, deduplicate real app IDs, exclude hidden services and generate fallback icons. Preserve dock order. Offer Add to dock without forcing apps into the chosen row. Reconcile uninstalls without overwriting preferences; ensure remaps can select newly installed apps.

### 5. Native store and on-TV Sources

Implement paired client authorization and job operations. Deliver signed-source enrollment first, then verified-HTTPS GitHub asset discovery and direct-package support. Add native URL entry, review and actionable errors. All browse, install, update and source-management actions stay inside the native launcher interface. A separate background component is an internal implementation detail, not a second store app.

### 6. Acceptance and recovery

Test official store open/browse and Home return; install/update/data retention; Add to dock and uninstall reconciliation; bad hash/key/source, incompatible version/architecture, unavailable network, interrupted download and duplicate jobs. Test certificate/ID collisions without automatic destructive fallback. Test standby/resume and a later controlled full restart separately. Verify localhost, root recovery, Home/startup, sign-ins, grabs/privacy and affected HA playback.

Use fresh guarded inverses for only this feature's launcher/assets/service/config. Do not restore a whole historical device database or remove unrelated app data. Identify disposable test packages before cleanup. Playback PR hold remains.

## Completion and unresolved checks

From the sofa, the owner can open Community Apps, install/update a supported package while retaining data, add a supported trusted source and add an app to the dock. Samsung Apps opens directly and Home returns to Quiet Home. Routine use needs no secondary device and preserves localhost/root/playback. Initial certificate provisioning and renewal may require an external signing tool, but this remains a feasibility question, not a requirement to use Apps2Samsung. Investigate existing profile import and supported certificate issuance before claiming fully autonomous renewal. Every arbitrary repository or root-dependent package is not a compatibility claim.

No engine compatibility, certificate handover, native-client pairing, verified-TLS bootstrap, store navigation, Settings redesign or boot test was performed on this Q60C during research. These are later checkpoints. Nothing needs to change while the owner tests playback.
