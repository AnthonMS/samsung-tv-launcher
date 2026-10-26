import { Fragment } from "react";
import { Code, H2, Note, type Section } from "../Article";
import type { Locale } from "@/lib/dictionaries";

const parts = [
  {
    "id": "resultat",
    "daTitle": "Hvad jeg har nu",
    "enTitle": "What I have now",
    "da": [
      "Jeg har erstattet Samsungs startskærm med en native launcher bygget videre på quiet-tizen. Den åbner direkte på Home og ved opstart. Den gamle web-launcher og dens trampolin-app er ikke længere nødvendige i den daglige opsætning.",
      "Nederst ligger en dock med apps og HDMI-indgange. Ovenover ligger YouTube-anbefalinger, Fortsæt med at se/Næste afsnit og livekanaler fra min Jellyfin-server. Moonfin står for min medieafspilning. Rækker, apps, rækkefølge, tema og ur kan tilpasses.",
      "Netflix-, TV Plus-, Disney+- og Prime Video-knapperne virker som mine egne genveje, også inde i andre apps. Kanalvippen skifter mellem Moonfin-kanaler med wraparound, og midtertryk åbner programguiden."
    ],
    "en": [
      "I replaced Samsung’s home screen with a native launcher built on quiet-tizen. It opens directly from Home and at startup. The old web launcher and trampoline app are no longer needed for everyday use.",
      "A dock contains apps and HDMI inputs. Above it are YouTube recommendations, Jellyfin Continue watching/Next up and live channels. Moonfin handles my media playback. Rows, apps, order, theme and clock are configurable.",
      "The Netflix, TV Plus, Disney+ and Prime Video buttons are custom shortcuts across apps. The channel rocker steps through Moonfin channels with wraparound, and its middle press opens the guide."
    ],
    "code": "",
    "href": "https://github.com/AnthonMS/samsung-tv-launcher/blob/main/docs/ARCHITECTURE.md"
  },
  {
    "id": "model",
    "daTitle": "Det gælder min model og firmware",
    "enTitle": "Start with the exact TV",
    "da": [
      "Mit TV er en Q60C fra 2023, model TQ65Q60CAUXXC, med firmware T-NKLCDEUC-0090-2220.4, Tizen 9 og .NET 6.0.9 på ARMv7. Det oprindelige quiet-tizen-projekt beskrev en anden model. Derfor kontrollerede jeg mine egne pakker, API’er, rettigheder og databasefelter først.",
      "Det her er en dokumenteret installation på ét konkret TV. En anden Samsung med Tizen 9 er ikke automatisk kompatibel. Læs softwareversionen under Settings, Support, About This TV. På min model kunne jeg også kontrollere versionen i /etc/prd_info.ini. Et root-probe alene siger ikke, at resten passer.",
      "Repoet har både den nye Q60C-guide og tydeligt markeret upstream-materiale. Brug ikke U8000F-profilen på en Q60C. Stop og undersøg forskellene, hvis modellen, firmwaren, pakkerne eller runtime ikke matcher."
    ],
    "en": [
      "My TV is a 2023 Q60C, model TQ65Q60CAUXXC, firmware T-NKLCDEUC-0090-2220.4, Tizen 9 and .NET 6.0.9 on ARMv7. Upstream quiet-tizen described another model, so I checked my own packages, APIs, permissions and schemas first.",
      "This is a recorded installation on one exact TV. Another Samsung running Tizen 9 is not automatically compatible. Read the displayed software version in Settings, Support, About This TV. On mine, /etc/prd_info.ini gave an independent check. A successful root probe alone does not validate everything else.",
      "The repository separates the Q60C guide from retained upstream reference material. Do not run the U8000F profile on a Q60C. Investigate differences in model, firmware, package layout or runtime before continuing."
    ],
    "code": "",
    "href": "https://github.com/AnthonMS/samsung-tv-launcher/blob/main/docs/MANUAL.md"
  },
  {
    "id": "vaerktoej",
    "daTitle": "Hent koden og gør computeren klar",
    "enTitle": "Prepare the PC and source",
    "da": [
      "Jeg bruger Tizen Studios Samsung SDB, Python 3.12, .NET SDK 8, Node.js til test, Git og OpenSSL. Git for Windows leverer OpenSSL i eksemplerne. Alle kommandoer herunder køres fra repoets rod på computeren, medmindre jeg udtrykkeligt skriver TV-script.",
      "Erstat YOUR_TV_IP og YOUR_PC_IP med adresserne på dit eget netværk. Hjælperen vælger kun den adresse, du giver den. Den scanner ikke efter andre TV. Funktionen tv gør blot de lange kommandoer kortere.",
      "Jeg gemmer personlige konfigurationer, legitimationsoplysninger, backupfiler og udtrukne Samsung-filer i .local. De er gitignored. Offentlige eksempler indeholder ikke mine tokens, certifikater eller databaser."
    ],
    "en": [
      "I use Samsung SDB from Tizen Studio, Python 3.12, .NET SDK 8, Node.js for tests, Git and OpenSSL. Git for Windows supplies OpenSSL in these examples. Commands below run from the repository root on the PC unless explicitly labelled as a TV script.",
      "Replace YOUR_TV_IP and YOUR_PC_IP with your own LAN addresses. The helper targets only that TV; it does not scan the network. The tv function simply shortens later commands.",
      "Personal configuration, credentials, backups and extracted Samsung files stay in ignored .local folders. Public examples do not contain my tokens, certificates or databases."
    ],
    "code": "git clone https://github.com/AnthonMS/samsung-tv-launcher.git\ncd samsung-tv-launcher\n$env:SDB = \"$env:USERPROFILE\\tizen-studio\\tools\\sdb.exe\"\n$env:PATH = \"C:\\Program Files\\Git\\usr\\bin;$env:PATH\"\n$env:TV_IP = 'YOUR_TV_IP'\n$env:PC_IP = 'YOUR_PC_IP'\nfunction tv { py -3.12 tools/device.py --tv $env:TV_IP --pc $env:PC_IP @args }\npy -3.12 quiet-tizen/tools/build.py payloads",
    "href": "https://github.com/AnthonMS/samsung-tv-launcher/blob/main/docs/MANUAL.md"
  },
  {
    "id": "udvikler",
    "daTitle": "Developer Mode og første root-kontrol",
    "enTitle": "Developer Mode and the first root check",
    "da": [
      "Åbn Apps på TV’et, tast 1 2 3 4 5, slå Developer Mode til og indtast computerens LAN-adresse som Host PC IP. Lav derefter en rigtig genstart. Et almindeligt sluk/tænd med fjernbetjeningen kan blot være standby.",
      "På dette tidspunkt skal adressen være computerens, ikke 127.0.0.1. Localhost kommer først senere, hvis du vælger automatisk root ved opstart.",
      "Probe bruger den eksisterende archive-root-metode og kontrollerer UID 0, runtime, oprydning og de beskyttede filer. Den ændrer ikke Home alene. Root skal kunne gennemføres, før pakkerne kan ændres. Hvis den fejler, er løsningen ikke at slukke sikkerhedssystemerne eller tvinge en anden models profil."
    ],
    "en": [
      "Open Apps on the TV, enter 1 2 3 4 5, enable Developer Mode and enter the PC’s LAN address as Host PC IP. Then perform a full restart. An ordinary remote off/on can just be standby.",
      "Use the PC address at this stage, not 127.0.0.1. Localhost is a later transition if you choose autonomous boot root.",
      "The probe uses the existing archive-root method and checks UID 0, runtime, cleanup and protected files. It does not assign Home by itself. If it fails, investigate compatibility rather than disabling security services or forcing another model’s profile."
    ],
    "code": "& $env:SDB connect \"$($env:TV_IP):26101\"\ntv probe",
    "href": "https://github.com/AnthonMS/samsung-tv-launcher/blob/main/docs/MANUAL.md"
  },
  {
    "id": "backup",
    "daTitle": "Registrer originalerne før noget ændres",
    "enTitle": "Record the original state before changing it",
    "da": [
      "Jeg gemmer appoversigt, manifester, ejere, filrettigheder, SMACK-labels, kørende services og de oprindelige Home-værdier. Der er tre Home-oplysninger: main-operationens app, db/appfw/home_appid og db/appfw/startup_appid. En tidligere trampolin-opsætning kan allerede have ændret dem.",
      "SQLite-backups laves med .backup, ikke ved at kopiere en aktiv database med cp. Originale assemblies, ressourcer og eventuelle native-image-caches gemmes både på TV’et og computeren. Jeg skriver den konkrete inverse operation, før jeg installerer noget.",
      "Manualen indeholder hele TV-scriptet og kommandoerne til at hente backupen. Root-kommandoer køres sekventielt gennem tv exec. Vent altid på, at oprydningen er færdig, før du genstarter eller sætter TV’et i standby."
    ],
    "en": [
      "I record installed apps, manifests, owners, modes, SMACK labels, services and original Home values. There are three Home selectors: the main-operation app, db/appfw/home_appid and db/appfw/startup_appid. A previous trampoline setup may already have changed them.",
      "SQLite snapshots use .backup, not cp on a live database. Original assemblies, resources and any displaced native-image caches are saved on the TV and PC. The targeted inverse is prepared before installation.",
      "The manual contains the complete TV snapshot script and transfer commands. Root/SDB operations run sequentially through tv exec. Wait for cleanup before restarting or entering standby."
    ],
    "code": "# Save the reviewed TV script from docs/MANUAL.md first.\ntv exec .local/snapshot.sh\ntv pull /home/owner/share/tmp/sdk_tools/native-01-backup.tar.gz .local/backups/native-01.tar.gz",
    "href": "https://github.com/AnthonMS/samsung-tv-launcher/blob/main/docs/MANUAL.md"
  },
  {
    "id": "byg",
    "daTitle": "Byg den native launcher",
    "enTitle": "Build the native launcher",
    "da": [
      "Launcheren er nu .NET/NUI frem for en webside. Jeg bruger Bixby Promotion-pakkens eksisterende plads til mit eget IL-program. Det er en ændring i en systempakke på skrivbar lagring, ikke en ny firmware eller et nyt styresystem.",
      "Der skal bygges mod matchende Tizen-assemblies fra din egen tilladte SDK/TV-installation. Repoet distribuerer ikke Samsungs binære filer. Henteværktøjet bruger små base64-blokke og kontrollerer størrelse og SHA256; sdb pull var ikke tilgængelig på denne model.",
      "Kopiér katalog- og klienteksempler til private lokale filer. Brug app-id’erne fra netop dit TV. YouTube kræver en kompatibel klientkonfiguration, du må bruge. Hemmelighederne skal ikke ligge i Git, selv om den færdige private installationspakke har brug for dem."
    ],
    "en": [
      "The launcher is now .NET/NUI rather than a web page. I use the existing Bixby Promotion package slot for my own IL program. This changes a package on writable storage; it does not install a new operating system or firmware.",
      "Build against matching Tizen assemblies obtained from your own permitted SDK/TV installation. Samsung binaries are not distributed in the repository. The transfer helper uses bounded base64 chunks with size/SHA256 verification because sdb pull was unavailable on this model.",
      "Copy catalog/client examples into private local configuration and use app IDs discovered on your own TV. YouTube needs a compatible client configuration you may use. Credentials stay out of Git even when the private installation package needs them."
    ],
    "code": "$refs = 'Tizen.dll','Tizen.NUI.dll','Tizen.System.dll','Tizen.Applications.Common.dll','Tizen.Applications.Service.dll'\nforeach ($name in $refs) {\n  tv pull \"/usr/share/dotnet.tizen/framework/$name\" \"quiet-tizen/.local/tv-framework/$name\"\n}\npy -3.12 quiet-tizen/tools/build.py launcher",
    "href": "https://github.com/AnthonMS/samsung-tv-launcher/blob/main/docs/MANUAL.md"
  },
  {
    "id": "installer",
    "daTitle": "Prøv den før Home ændres",
    "enTitle": "Trial installation before assigning Home",
    "da": [
      "Jeg installerer først uden at ændre standard-Home. Kun den berørte app stoppes. Den nye assembly kopieres med den observerede ejer, filtilstand og SMACK-label, og de nye ressourcer lægges i quiet-home-mappen. Installationsscriptet og metadata er beskrevet fuldt i manualen.",
      "På TV’et kontrollerer jeg billede, tekst, pile, OK, Back, appstart, lyd og adgang til Samsungs native indstillinger. HDMI skal testes med en faktisk tilsluttet enhed. En statusfil eller en vellykket API-retur er ikke nok til at bevise, at skærmen virker.",
      "Først efter at den synlige prøve er god, peger main-operationen, Home og startup på com.samsung.tv.bixby-promotion. Derefter tester jeg Home inde fra en anden app, fuld genstart og standby/vækning hver for sig."
    ],
    "en": [
      "I install a trial without changing default Home first. Only the affected app is stopped. The assembly keeps the inspected owner, mode and SMACK label, and resources go in quiet-home. The manual supplies the complete script and metadata checks.",
      "On the TV I check rendering, text, arrows, OK, Back, app launches, audio and native settings. Test HDMI with a device actually connected. A status file or successful API return alone is not a visual test.",
      "Only after accepting that trial do I point the main-operation handler, Home and startup at com.samsung.tv.bixby-promotion. Then I test Home from another app, a full restart and standby/wake separately."
    ],
    "code": "# After backup, trial installation and physical verification:\ntv exec .local/assign-home.sh",
    "href": "https://github.com/AnthonMS/samsung-tv-launcher/blob/main/docs/MANUAL.md"
  },
  {
    "id": "dock",
    "daTitle": "Tilpas dock, navigation og udseende",
    "enTitle": "Dock editing, navigation and appearance",
    "da": [
      "Hold OK på en dock-flise for at redigere, eller vælg Customize app row i Settings. Fliserne vipper. OK tager fat i en flise, venstre/højre flytter den, og op skifter synlighed. Skjulte fliser er dæmpede under redigering. Back afslutter.",
      "HDMI-indgangene er fliser med tal og gradienter. De er ikke låst til en Nintendo- eller PlayStation-tegning. Hvis TV’et leverer et enhedsnavn, kan det vises efter HDMI-navnet. Lang tekst ruller. Det er visningslogik, ikke automatisk garanti for, at enhver enhed kan identificeres.",
      "De tre cirkler øverst bruges til Home, Apps og Settings. Back følger intern historik i stedet for at hoppe tilbage til en HDMI-kilde. Tema, 12/24-timers ur, dato og datoformat vælges i Settings. Rækkerne forbliver i en sammenhængende liste; fokus ruller kun nok til at vise den valgte række."
    ],
    "en": [
      "Hold OK on a dock tile, or choose Customize app row in Settings. Tiles jiggle. OK picks one up, Left/Right moves it, Up toggles visibility and Back finishes. Hidden tiles stay dimmed during editing.",
      "HDMI inputs are numbered gradient tiles, not ports permanently tied to Nintendo or PlayStation artwork. A device name can be appended when the TV provides it, and long captions scroll. That is display logic, not a promise every connected device is identifiable.",
      "Top-right circles navigate Home, Apps and Settings. Back follows internal history rather than returning to HDMI. Theme, 12/24-hour time, date and date format live in Settings. Rows form a continuous list; focus scrolls only far enough to reveal the selected row."
    ],
    "code": "",
    "href": "https://github.com/AnthonMS/samsung-tv-launcher/blob/main/docs/MEDIA.md"
  },
  {
    "id": "konti",
    "daTitle": "YouTube-kode og Jellyfin Quick Connect",
    "enTitle": "YouTube codes and Jellyfin Quick Connect",
    "da": [
      "YouTube-rækken parres på TV’et ved at vælge YouTube account under Settings, YouTube & media. Jeg åbner den viste adresse på telefonen, indtaster koden og godkender kontoen. Afspilningsapp vælges særskilt, så jeg kan bruge YouTube eller TizenTube.",
      "For Jellyfin indtaster jeg serveradressen, vælger Jellyfin account og godkender koden under serverens Profile, Quick Connect. Moonfin/Jellyfin-afspilleren skal også være logget ind. At parre launcheren logger ikke automatisk en anden app ind.",
      "Fortsæt/Næste afsnit og livekanaler kan slås til eller fra. Afspilning kan skiftes mellem Moonfin og Jellyfin. Livekanaler viser kanalnavn og aktuelt program uden en unødvendig varighedsbjælke. At skjule (Geo-blocked) eller opløsning i navnet fjerner ikke en regionsbegrænsning.",
      "Jeg tester mindst to forskellige videoer med en tur til Home imellem. Det fangede en varmstart-fejl, som én vellykket første afspilning ikke viste. En kanal, der også fejler i Moonfins egen guide, skal undersøges på serveren eller kilden."
    ],
    "en": [
      "Pair the YouTube row by selecting YouTube account in Settings, YouTube & media. Open the displayed verification address on your phone, enter the code and approve the account. Choose YouTube or TizenTube separately as the playback app.",
      "For Jellyfin, enter the server address, select Jellyfin account and approve the code in the server’s Profile, Quick Connect. The Moonfin/Jellyfin playback app must also be signed in. Pairing the launcher does not log another app in automatically.",
      "Continue/Next up and live rows can be enabled independently. Switch media playback between Moonfin and Jellyfin. Live channels show their name/current programme without a duration bar. Removing a (Geo-blocked) or resolution suffix from a label does not bypass a restriction.",
      "Test at least two videos with a Home return between them. That exposed a warm-launch bug a single successful first playback missed. A channel that fails in Moonfin’s own guide also needs server/source investigation."
    ],
    "code": "",
    "href": "https://github.com/AnthonMS/samsung-tv-launcher/blob/main/docs/MEDIA.md"
  },
  {
    "id": "fjernbetjening",
    "daTitle": "Genvejsknapper fra alle apps",
    "enTitle": "Remote shortcuts across apps",
    "da": [
      "Netflix åbner som standard Moonfin, TV Plus åbner Moonfins EPG, Disney+ åbner YouTube og Prime Video åbner Twitch. De fire knapper kan ændres til installerede apps under Remote & controllers. Kanal op/ned følger Moonfins sorterede kanalliste og fortsætter fra den anden ende, når listen slutter.",
      "Det sker med navngivne, eksklusive Wayland-grabs i launcheren. På min firmware krævede det en præcis Cynara-regel for launcherens label, bruger og keygrab-rettighed. Jeg gemte den oprindelige tilstand og en inverse, før reglen blev tilføjet. Jeg gav ikke alle apps generelle systemrettigheder.",
      "EPG og kanalhop bruger en lille versionslåst Moonfin-patch. Koldstart sender en destination via app-control; varmstart bruger en Tizen message port. Fælles request-id undgår dobbelt afspilning. Ukendte Moonfin-bundles afvises af patcheren. Appopdateringer kræver ny kontrol."
    ],
    "en": [
      "Netflix defaults to Moonfin, TV Plus to Moonfin’s EPG, Disney+ to YouTube and Prime Video to Twitch. The four branded buttons can be assigned to installed apps in Remote & controllers. Channel Up/Down follows Moonfin’s sorted lineup and wraps at either end.",
      "The launcher uses named exclusive Wayland grabs. On this firmware it needed an exact Cynara rule for its own label, user and keygrab privilege. I saved the prior state and inverse before adding it, without giving every app general system permissions.",
      "EPG/channel switching uses a small version-locked Moonfin patch. Cold starts use app-control destinations; warm delivery uses a Tizen message port. Shared request IDs prevent duplicate playback. Unknown Moonfin bundles are rejected, and app updates need fresh inspection."
    ],
    "code": "tv pull /opt/usr/apps/MoonfinApp/res/wgt/main.js .local/moonfin/original-main.js\ntv pull /opt/usr/apps/MoonfinApp/res/wgt/index.html .local/moonfin/original-index.html\npy -3.12 quiet-tizen/tools/moonfin_guide_bridge.py .local/moonfin/original-main.js .local/moonfin/original-index.html .local/moonfin/patched",
    "href": "https://github.com/AnthonMS/samsung-tv-launcher/blob/main/docs/REMOTE.md"
  },
  {
    "id": "privatliv",
    "daTitle": "Hvad der blev slået fra",
    "enTitle": "What the privacy cleanup disables",
    "da": [
      "Jeg slog kendte reklame-, analyse-, ACR-, kontekst- og stemmekomponenter fra samt valgfrie SmartThings-, Health- og Gaming Hub-hjælpere. Det er i alt 47 gennemgåede appregistreringer. Valgfrie funktioner er ikke automatisk malware; jeg bruger dem bare ikke i denne opsætning.",
      "Viewing Information Services, Interest-Based Ads og valgfri Remote Management var allerede fravalgt. Jeg kontrollerede de faktiske consent-rækker og beholdt fravalget. Stemmeaktivering, wakeupless og speech recognition blev sat fra med deres korrekte typer. Kontologin og nødvendige sessionscookies blev bevaret.",
      "Ni verificerede markerfiler dækker ti services. Fire andre services ligger på den skrivebeskyttede systempartition og kræver runtime masks. Jeg ændrede ikke bootloader, firmware, Samsungs globale passwd/key-filer eller generelle sikkerhedssystemer.",
      "ACR over HDMI er dokumenteret i forskning, men det beviser ikke en bestemt billedfrekvens eller konkrete datasalg på mit TV. Jeg kan kontrollere kendte processer, indstillinger og netværksgrænser; jeg kan ikke udstede et nul-telemetri-certifikat for lukket firmware. Softwarekontrol beviser heller ikke, at en mikrofon er elektrisk afbrudt."
    ],
    "en": [
      "I disabled reviewed advertising, analytics, ACR, context and voice components plus optional SmartThings, Health and Gaming Hub helpers. The total is 47 app registrations. Optional features are not automatically malware; I simply do not use them in this setup.",
      "Viewing Information Services, Interest-Based Ads and optional Remote Management were already opted out. I checked the actual consent rows and retained those choices. Voice activation, wakeupless and speech recognition were disabled using their correct types. Account logins and necessary session cookies were preserved.",
      "Nine verified marker files cover ten services. Four other services live on read-only system storage and require runtime masks. I did not change the bootloader, flash firmware, replace global passwd/key files or disable core security.",
      "ACR over HDMI is documented in research, but that does not establish a particular capture rate or actual data sales on my TV. I can check known processes/settings and network boundaries; I cannot certify zero telemetry across closed firmware. Software checks also cannot prove a microphone is electrically disconnected."
    ],
    "code": "# Review one group from YOUR fresh snapshot before applying it.\npy -3.12 quiet-tizen/tools/plan_cleanup.py --snapshot .local/privacy/.pkgmgr_parser.db --profile quiet-tizen/profiles/tq65q60cau-2220.4.json --groups ads_analytics --output quiet-tizen/.local/plans/ads\ntv exec quiet-tizen/.local/plans/ads/apply.sh\ntv exec recipes/mask-runtime.sh",
    "href": "https://github.com/AnthonMS/samsung-tv-launcher/blob/main/docs/PRIVACY.md"
  },
  {
    "id": "opstart",
    "daTitle": "Så blokeringerne overlever genstart",
    "enTitle": "Make the blocks return after every boot",
    "da": [
      "Runtime masks i /run forsvinder ved en fuld genstart. Derfor installerede jeg en separat, headless opstartshjælper i den ubrugte Gaming Hub Service-plads. Den får root via TV’ets egen localhost-SDB, genanvender de fire masks og afslutter. Launcheren er en anden app og skal ikke vente på et root-vindue.",
      "Jeg testede først kæden med en midlertidig PC-relay, mens computerens normale developer-adresse stadig virkede. Hjælperen viste UID 0-bevis og de faktiske maskerede, inaktive services. Først derefter aktiverede jeg on-boot i manifest og registrering og fjernede test-endpointet.",
      "Pakken bygges privat. En lokal signeringsnøgle og en tilfældig bridge-token skal blive i .local. Builderen installerer intet på TV’et alene. Arkivets SHA256 bruges ved installationen, og originalerne samt restore-script skal hentes, før du fortsætter."
    ],
    "en": [
      "Runtime masks under /run disappear on a full restart. I installed a separate headless bootstrap in the unused Gaming Hub Service slot. It roots through the TV’s localhost SDB, recreates the four masks and exits. Quiet Home is a different app and does not wait for a root window.",
      "I first tested the chain through a temporary PC relay while normal PC developer access still worked. It produced UID 0 proof and the actual masked/inactive services. Only then did I enable both manifest/registry boot flags and remove the test endpoint.",
      "The package is built privately. Its local signing key and random bridge token stay under .local. The builder does not install anything by itself. Use its archive SHA256 for installation and download originals/the restore script before continuing."
    ],
    "code": "tv pull /opt/usr/apps/com.samsung.tv.ghservice/tizen-manifest.xml .local/boot/original-manifest.xml\npy -3.12 tools/prepare_boot.py --manifest .local/boot/original-manifest.xml --test-host $env:PC_IP\ntv push .local/boot/boot-package.tar.gz /home/owner/share/tmp/sdk_tools/boot-package.tar.gz\ntv exec recipes/install-boot-test.sh boot-01 EXPECTED_ARCHIVE_SHA256\n# Follow the headless test, backup transfer and enable steps in docs/PRIVACY.md.",
    "href": "https://github.com/AnthonMS/samsung-tv-launcher/blob/main/docs/PRIVACY.md"
  },
  {
    "id": "forbindelse",
    "daTitle": "Den vigtige ændring i Developer Mode",
    "enTitle": "The important Developer Mode transition",
    "da": [
      "Efter en vellykket prøve og afsluttet root-oprydning ændres Host PC IP i Developer Mode til 127.0.0.1, og TV’et genstartes helt. Hjælperen kan nu roote lokalt ved hver opstart uden en tændt computer. PC-adgangen går gennem en tokenbeskyttet bridge på port 26103.",
      "PC’ens og TV’ets netværksadresser er ikke blevet ændret. Det er Developer Modes tilladte vært, der er ændret. En normal direkte SDB-forbindelse fra PC’en virker derfor ikke som før. Brug proxyen og hold tokenen privat. Bridge-trafikken er ikke krypteret og hører til på det betroede lokale netværk.",
      "Jeg kontrollerede et nyt kernel boot-id, matchende root-bevis, alle fire masks, de 47 flags, otte knap-grabs og kontoerne. Derefter slukkede jeg med fjernbetjeningen, ventede 30 sekunder og vækkede igen. Quiet Home og blokeringerne var der stadig. Der er stadig en kort opstartsperiode før de fire masks anvendes; fravalg og appblokeringer gælder også i den periode."
    ],
    "en": [
      "After a successful trial and completed root cleanup, change Developer Mode Host PC IP to 127.0.0.1 and fully restart. The helper can then root locally at every boot without a running PC. PC management uses a token-protected bridge on port 26103.",
      "The PC and TV LAN addresses have not changed. Developer Mode’s authorized host has changed, so ordinary direct PC SDB access no longer works as before. Use the proxy and keep its token private. Bridge traffic is unencrypted and belongs on the trusted LAN.",
      "I checked a new kernel boot ID, matching root proof, all four masks, 47 flags, eight key grabs and account connections. Then I turned the TV off with the remote, waited 30 seconds and woke it. Quiet Home and protection remained. A short startup interval still precedes the four masks; persistent opt-outs/app disables remain in force throughout."
    ],
    "code": "# Only AFTER the successful trial, enablement, UI address change and restart:\nfunction tv { py -3.12 tools/device.py --tv $env:TV_IP --pc $env:PC_IP --bridge-token-file .local/boot/bridge.token @args }\ntv probe",
    "href": "https://github.com/AnthonMS/samsung-tv-launcher/blob/main/docs/PRIVACY.md"
  },
  {
    "id": "fejl",
    "daTitle": "Fejlene er også dokumenteret",
    "enTitle": "The failures are documented too",
    "da": [
      "Jeg gemmer ikke kun den pæne slutversion. Repoets journal forklarer, hvorfor trampolinen blev droppet, hvorfor varm app-control ikke var nok, hvorfor kanalhop gik fra serverkommandoer til Moonfins egne callbacks, og hvorfor en gammel .NET-preloader kunne køre den tidligere DLL efter en filudskiftning.",
      "En særlig vigtig fejl var security-manager-policy-reload --help. Det er et script, der ikke behandler argumentet som almindelig hjælp, og det genindlæste base policies. Der fandtes ikke en snapshot fra lige før, så jeg kan ikke påstå præcis før/efter-lighed. De faktiske privacy-fravalg blev bagefter kontrolleret. Den kommando skal ikke bruges til at spørge om tilladelser.",
      "Der ligger et indeks over alle bevarede operator-scripts og deres registrerede resultater i repoet. De rå, personlige scripts, fejloutput, databaser og signeringsfiler er bevaret lokalt, men offentliggøres ikke. Den offentlige manual viser den gennemgåede slutprocedure med pladsholdere."
    ],
    "en": [
      "I kept more than the polished final version. The journal explains why the trampoline was retired, why warm app-control was insufficient, why channel switching moved from server commands to Moonfin callbacks, and why a stale .NET preloader could execute an earlier DLL after replacement.",
      "One important mistake was security-manager-policy-reload --help. It is a script that does not interpret that argument as ordinary help and reloaded base policies. There was no immediate pre-call snapshot, so exact before/after equivalence cannot be claimed. Actual privacy opt-outs were checked afterwards. Do not use that command to query permissions.",
      "The repository indexes every retained operator script and its saved outcome. Raw personalized scripts, error output, databases and signing material are kept locally, not published. The public manual contains the reviewed final procedure with placeholders."
    ],
    "code": "",
    "href": "https://github.com/AnthonMS/samsung-tv-launcher/blob/main/docs/JOURNAL.md"
  },
  {
    "id": "gendan",
    "daTitle": "Sådan kommer jeg tilbage igen",
    "enTitle": "Recovery and future maintenance",
    "da": [
      "Hvis bridge eller boot-root ikke virker efter en inkompatibel firmwareopdatering, åbner jeg Developer Mode på TV’et, sætter Host PC IP tilbage til PC’ens rigtige adresse og genstarter. Så kan jeg igen undersøge TV’et via den almindelige forbindelse.",
      "Boot-persistens, privacy-flags, remote-regel, Moonfin-patch og Home-routing har hver sin inverse. Jeg gendanner kun de felter og filer, den konkrete fase ændrede. Jeg erstatter aldrig en aktiv pakkedatabase med en gammel fuld kopi og rydder ikke kontodata som standardreparation.",
      "Fremover tilføjer jeg én funktion ad gangen, bygger lokalt og tester på TV’et, før den næste kommer til. Det var sådan, jeg kunne opdage, hvis en ændring gjorde launcheren langsommere. Hele kildekoden, manualen, licenserne, testene og journalen ligger i GitHub-repoet. Den gamle web-launcher er bevaret som historik."
    ],
    "en": [
      "If an incompatible firmware update prevents boot root/the bridge, I open Developer Mode on the TV, set Host PC IP back to the actual PC address and restart. Ordinary management is then available for investigation.",
      "Boot persistence, privacy flags, the remote rule, Moonfin patch and Home routing have separate inverses. Restore only the fields/files changed by that phase. Do not replace a live package database with an old full copy or clear account data as the default repair.",
      "I will keep adding one feature at a time, building locally and checking the TV before the next change. That made performance regressions easier to identify. Source, manuals, licenses, tests and journal are in the GitHub repository. The old web launcher remains as history."
    ],
    "code": "",
    "href": "https://github.com/AnthonMS/samsung-tv-launcher/blob/main/docs/RECOVERY.md"
  }
];

export const sections: Section[] = parts.map((p) => ({ id: p.id, da: p.daTitle, en: p.enTitle }));

export function Body({ lang }: { lang: Locale }) {
  const da = lang === "da";
  return (
    <>
      <p>{da
        ? "Jeg ville have en hurtig startskærm med de apps og medier, jeg faktisk bruger. Denne guide erstatter min tidligere beskrivelse af en web-launcher med trampolin. Her er den native løsning, hvad der blev ændret på TV’et, hvordan forbindelsen virker, og hvordan jeg kan gendanne det hele."
        : "I wanted a fast home screen with the apps and media I actually use. This guide replaces my earlier web-launcher/trampoline write-up. It covers the native solution, the TV changes, the management connection and how to undo each layer."}</p>
      <Note><p>{da ? "Kildekode og den fulde manuelle procedure: " : "Source and the complete manual procedure: "}<a href="https://github.com/AnthonMS/samsung-tv-launcher" rel="noopener">AnthonMS/samsung-tv-launcher</a>. {da ? "Brug manualen som kommandoreference og læs kompatibilitetsafsnittet først." : "Use the manual as the command reference and read compatibility first."}</p></Note>
      {parts.map((p, i) => (
        <Fragment key={p.id}>
          <H2 section={sections[i]} lang={lang} />
          {(da ? p.da : p.en).map((paragraph, n) => <p key={n}>{paragraph}</p>)}
          {p.code ? <Code>{p.code}</Code> : null}
          <p><a href={p.href} rel="noopener">{da ? "Detaljer og kommandoer i repoet" : "Detailed instructions and commands in the repository"}</a></p>
        </Fragment>
      ))}
      <p>{da ? "Læs også den uafhængige undersøgelse af ACR over HDMI: " : "For independent measurements of ACR over HDMI, see "}<a href="https://arxiv.org/abs/2409.06203" rel="noopener">Watching TV with the Second-Party</a>.</p>
    </>
  );
}
