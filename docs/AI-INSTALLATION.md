# AI-assisted installation of Quiet Home

Give your AI assistant the prompt below and this repository. It provides a route from an inspected Samsung TV to Quiet Home, optional local Home Assistant control, Community Apps and actionable TV notifications. The assistant should operate on your own clone and your own device, keeping device-specific material private.

This is an installation and adaptation guide, not an unattended universal installer. The complete stack was developed on a **Samsung Q60C, Tizen 9, firmware 2220.4, ARMv7, .NET 6.0.9**. Another model or firmware needs its own compatibility investigation before writes. A working root probe alone does not prove the other components are compatible. There is no requirement to reproduce the original owner's privacy choices or dashboard.

## Prompt to give your assistant

```text
Help me install and configure Quiet Home on my own Samsung Tizen TV using
https://github.com/AnthonMS/samsung-tv-launcher

Start by reading AGENTS.md, quiet-tizen/AGENTS.md and
docs/AI-INSTALLATION.md in my clone. Follow the staged installation guide,
not a collection of scripts run indiscriminately. Read the current manual
and recovery guide before operating on the TV.

Identify my actual device, firmware, runtime, installed apps and existing
customizations. Use only the TV I identify. If my TV differs from the
documented Q60C profile, investigate a separate compatible profile before
mutating it. Never fake a firmware declaration or weaken a compatibility guard.

Use my existing instructions and authorization. Ask only for missing facts,
feature choices or physical observations that you cannot determine safely.
Tell me when a step will interrupt playback. Preserve my accounts, app data,
native settings, display, audio, HDMI, networking and accessibility.

Build locally, take fresh private backups and prepare targeted rollback
before each installation stage. Install the launcher as a trial before
assigning Home/startup. Then add the features I choose, one stage at a time.

For full playback and notifications, use the matching AnthonMS/QuietThings
development fork and the additional TV components documented here. Installing
the original HA integration alone does not install the full stack.

Keep secrets and proprietary files under ignored .local storage. Generate my
own credentials and use my own signing profiles. Do not reuse the author's
private configuration, account data, certificates, device IDs or backups.

Validate actual behavior on my TV, including separate restart and standby
tests. Distinguish automated checks from things I have physically observed.
Do not trigger a real lock or other physical action as a development test.

Finish with a private installation record listing exact source revisions,
installed components, recovery steps, passed checks and remaining limits.
Do not publish or open a PR unless I explicitly request it.
```

## 1. Get the matching sources

The complete stack currently lives on development branches in two repositories. Use those branches, not an assumption that either upstream project's default branch includes these features:

```sh
git clone --branch codex/quietthings-playback https://github.com/AnthonMS/samsung-tv-launcher.git
git clone --branch codex/playback-extension https://github.com/AnthonMS/QuietThings.git
```

If clones already exist, inspect their remotes, branch and local changes before updating. Never reset someone else's working tree. Record both full commit IDs with `git rev-parse HEAD` in the private installation record. If a branch moves or a future release changes the process, inspect the source and its documentation together rather than combining old binaries with new instructions.

Read in this order:

1. Repository [instructions](../AGENTS.md), [native toolkit instructions](../quiet-tizen/AGENTS.md), [manual](MANUAL.md) and [recovery](RECOVERY.md).
2. [Architecture](ARCHITECTURE.md), [media](MEDIA.md) and [remote controls](REMOTE.md).
3. The guides for selected features: [privacy and boot](PRIVACY.md), [HA playback](HOME-ASSISTANT.md), [Community Apps](COMMUNITY-APPS.md), [notifications](NOTIFICATIONS.md).
4. In the QuietThings clone: `README.md`, `experimental/playback/README.md`, `experimental/playback/bridge/README.md`, `experimental/playback/web/COBALT.md` and `examples/doorbell-tv.yaml`.

Current feature guides take precedence over historical journal entries and upstream U8000F case-study instructions. The [journal](JOURNAL.md) explains decisions and past failures; it is not a script to replay. This public guide must work without the original owner's ignored `AI/` history or private operator files.

## 2. Inventory and choose a supported scope

Establish the TV's explicit address with its owner. Do not scan an entire network. Record model, displayed firmware and independent on-device version, architecture, installed .NET runtime, package IDs, package manifests, owners, SMACK labels, database schemas, Home/startup selectors and current Developer Mode configuration. Read existing customizations before changing a TV that already runs QuietTizen.

The [manual](MANUAL.md) supplies PC prerequisites, the initial root probe and read-only inventory commands. Prerequisites include Samsung SDB/Tizen Studio, Python, a compatible .NET SDK with .NET 6 targeting support, Node.js, Git and OpenSSL. HA tests need Linux or WSL. Follow the versions in the current build and test files. Obtain Samsung framework assemblies from your own compatible TV or permitted SDK; they are not distributed here.

For a stock compatible TV, Developer Mode initially points to the PC. For an already customized localhost setup, use its existing private bridge. Do not change Developer Mode back and forth without recording the current access and recovery route.

Choose the features independently:

| Feature | Additional requirements |
| --- | --- |
| Native launcher and Home/startup | Matching runtime, inspected host package and supported Home APIs |
| YouTube/Jellyfin rows | Your permitted client configuration and fresh account pairing |
| Remote remaps | Inspected key names and narrowly scoped key-grab permission |
| Autonomous boot/root access | Compatible bootstrap slot, local SDB and a private management bridge |
| Base HA control | Launcher LAN API, token, Samsung pairing and modified integration |
| Moonfin/Jellyfin playback | Companion plus matching launcher/player Jellyfin account and local server sessions |
| YouTube/TizenTube playback | Protected root playback bridge, external Cobalt runtime and app-local proxy/trust setup |
| Twitch/KickTV playback | Protected root playback bridge and managed launches with private inspectors |
| Community Apps | Localhost developer access, your own valid signing profiles and compatible packages |
| HA notifications | Modified launcher/companion and modified QuietThings integration |

The original Q60C repurposes Bixby Promotion for Home and Gaming Hub Service for the bootstrap. Preserve those slots if this profile applies. Other TVs may lack them or assign different privileges. Stop that installation stage and report the concrete mismatch rather than substituting arbitrary system packages. Optional privacy cleanup is a separate choice, not a prerequisite for every HA feature.

## 3. Back up, build and trial the launcher

Follow [manual sections 3 through 6](MANUAL.md#3-obtain-matching-private-framework-references) for reference acquisition, configuration, build, backups and trial installation. Use ignored `.local/` storage for artifacts. Do not distribute the build output: it can contain private configuration.

Before mutation, save consistent SQLite `.backup` snapshots, affected original packages/files, metadata, selectors and configuration. Prepare file-hash and field-value guarded inverses. Keep a recovery copy on both TV and PC and verify that the PC copy can be read. Never overwrite the first backup to make a retry succeed or restore a stale registry wholesale.

Run SDB/root operations sequentially, wait for cleanup, and inspect actual state after a timeout or partial failure before retrying. Never reboot from inside a temporary root session. Keep Samsung's global security configuration intact.

Trial Quiet Home without assigning it as default. Verify visible navigation, launch/return, settings, HDMI, volume, audio and existing sign-ins. Only after accepting the trial, assign Home/startup using [manual section 7](MANUAL.md#7-assign-home-and-startup-after-accepting-the-trial). Inspect Samsung's separate Smart Hub auto-start policy as well as the selectors. A selector reading back correctly immediately after a write does not prove persistence across boot.

## 4. Add media, remaps and autonomous startup

Follow the media and remote guides with your discovered app IDs. Pair accounts afresh; keep credentials off the console and out of source control. Do not copy the owner's Jellyfin URL, OAuth configuration or preference files. Preserve native settings access.

If selected features require boot/root services, follow the staged test and transition in [PRIVACY.md](PRIVACY.md#6-build-the-optional-tv-only-boot-helper). Test access while the PC remains the authorized Developer Mode host. Prepare recovery before transitioning the host to `127.0.0.1`. Verify the token bridge before relying on it for subsequent maintenance. This is required infrastructure for this stack's local installer and protected playback runtime, even when optional privacy groups are not wanted; adapt the chosen boot payload rather than applying unwanted cleanup.

The existing `tools/prepare_boot.py` builds the base boot helper. It does **not** automatically provision the complete playback addon, Cobalt runtime, store signing profiles or managed-update recovery baseline. Do not report those features installed merely because the base helper built successfully.

## 5. Provision playback and Home Assistant

Use [HOME-ASSISTANT.md](HOME-ASSISTANT.md) and the QuietThings companion, bridge and Cobalt installation contracts. The launcher already includes the host callbacks/providers; the additional root bridge and app runtime still need explicit provisioning.

Build the root bridge from `experimental/playback/bridge/Bridge.csproj`. Build the Cobalt bundle with `npm ci` and `npm run bundle` in `experimental/playback/web`, preserving its generated license notice. Inspect the actual app-local trust assets, native Cobalt identity, Node runtime and package labels before staging anything. Generate a fresh observer credential distinct from the LAN credential. Keep keys in private app/root storage, never in web pages or HA.

The runtime uses a root-owned `/opt/usr/share/quietthings-web-playback` component and a separately maintained `/opt/usr/share/quietthings-cobalt` runtime on the tested profile. Read the bridge source, `start-service.sh`, `manage-twitch.sh`, `ManagedUpdateRecovery.cs` and the launcher boot-resource selection before preparing the device-specific deployment. Stage code, helper scripts, inspected token/runtime paths, permissions, optional markers and signed startup resources as one verified installation phase with an inverse. Do not invent a missing universal deployment command or assume the original owner's private signing key is available.

Check loopback binding, authentication, credential separation and inspector firewall protection before enabling managed app launches. An inspector must not be exposed to the LAN. Preserve original recovery resources when staging optional signed boot resources. Missing/invalid addons must not destroy the base recovery route.

Install `custom_components/quietthings` from **the matching fork** into HA, then restart HA and add/configure the integration. Preserve an existing installation's config entries and entity IDs. Allow Samsung pairing on the TV when requested. Configure the TV address/token and enable the optional playback port, normally `18766`; base control uses `18765`. New HA configurations disable the companion with port `0` until explicitly enabled.

HA initiates connections. There is no HA server destination or HA access token to hardcode on the TV. The authenticated LAN APIs use plain HTTP on a trusted network and should not be exposed to the internet.

Use the dedicated QuietThings media player's state and `app_name`, `app_id` and `media_title` attributes. Recreate any dashboard using your own entity IDs; the original owner's universal media player and custom cards are not automatically installed. Keep hardware power state separate from media state. Confirmed idle can display the app plus Nothing Playing; unknown must not pretend to be idle. Interpolated dashboard progress is not a new player sample every second.

## 6. Provision Community Apps and recovery baselines

Follow [COMMUNITY-APPS.md](COMMUNITY-APPS.md), including private signing-profile preparation with `tools/community_profiles.py`. Use your own certificates valid for your TV. Existing app updates need a matching author identity. Never uninstall an app to bypass a certificate mismatch without an explicit data-migration decision.

The launcher browses catalogs and compatible packaged releases, not arbitrary source repositories or APKs. Samsung Apps handles Samsung's YouTube update path. Moonfin and TizenTube use checked launcher-owned adapters; do not disable structure/hash guards to accept an unknown release.

Before offering managed updates for an existing customized app, establish a retained working signed WGT whose bytes match the installed app and the expected adapter/runtime. A stock package is not a backup of a patched installation. The recovery helper requires a verified baseline and sufficient space for complete writable app data. If these prerequisites cannot be established, leave that update unavailable and explain what is missing.

Detected managed-update failures restore the old signed package and writable data. Interrupted transactions reconcile; unresolved recovery keeps its backup and blocks another update. Verified success removes the temporary old backup while retaining the current WGT as next-update baseline. This cannot catch every bug discovered later by a person and is not permanent version history. Prove recovery using a disposable app before risking an owner's production app data.

## 7. Enable notifications and adapt the example

Use [NOTIFICATIONS.md](NOTIFICATIONS.md) and the fork's `services.yaml` for the actual service fields. Enable access, notifications and buttons in Quiet Home settings. Start with a harmless text notice, then an image/GIF, camera preview and a harmless named callback.

`quietthings.notify` accepts title, message, duration, optional `image_url` or `camera_entity` and named action buttons. HA prepares the media. URL images can use HTTP(S) or `/local/`; camera entities default to a low-frame-rate live preview, with explicit snapshot mode available. This is not full-motion camera video/audio. Remote navigation is captured immediately and returned on button, Back, dismissal or timeout while the underlying app continues playback.

Adapt `examples/doorbell-tv.yaml` with **your own** config entry, doorbell trigger, camera, TV and lock entities. Check HA configuration and reload/restart as required. Preserve unrelated existing automations. Offer physical actions only when appropriate and validate callback identity, notification ID and current state again in HA. Use a harmless callback or mocked lock service for development; actual door unlocking requires deliberate owner testing. Do not copy the original owner's private HA package.

## 8. Acceptance and handover

Run the relevant local suites from the repository READMEs, feature guides and CI configuration. Build against matching private references. Local tests do not certify another TV.

Record separate results for:

- Home/Back/navigation, stock settings, HDMI, audio, accounts and chosen remaps.
- Full restart with automatic Home and startup services, then a separate standby/wake cycle after root cleanup.
- Actual title/state/progress and supported controls in each installed media app, including pause, menu, exit, app switching and stale-metadata clearing.
- YouTube versus TizenTube mode switching and Twitch/KickTV launches from both dock and remapped buttons.
- Installer signing/identity checks, data preservation, failure recovery and success/failure notices.
- Notification images, animated GIFs, camera refresh, harmless action callback, expiry and restored input without interrupting media.
- Private temporary-file cleanup, closed root sessions and retained recovery material.

Do not equate an open app with playback, an accepted HTTP request with a visible result, or a correct DLL on disk with the running assembly. A confirmed idle preloader can cache an older DLL; follow the targeted maintenance procedure rather than killing every dotnet process.

Live streams do not have fabricated finite seek ranges. Twitch depends on app markup; Kick titles use a bounded internet metadata lookup. App/firmware changes may need adapter maintenance. The original installation still has an intermittent HA wake/on-state caveat; a successful boot is not proof every wake route works.

Leave a private handover with source revisions, device inventory, settings, per-component installed hashes, backup locations, specific rollback commands and evidence/limits. Keep token/key values out of that prose. Do not claim completion for an untested or blocked stage. Future maintenance should begin from that device's record and fresh observations, not someone else's old installer.
