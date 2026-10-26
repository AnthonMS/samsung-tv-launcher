# What the privacy cleanup means

We disabled selected advertising, content-recognition, analytics and optional
voice/context components. “Disabled” means recorded registry flags,
service-stop conditions and process checks—not proof that every related
binary was deleted or every possible data flow was blocked.

## Content recognition and screen sampling

Automatic Content Recognition (ACR) can sample displayed content, derive a
fingerprint and compare it with a reference library to identify programs or
ads. It is more accurate to describe this as **screen-content fingerprinting**
than to assert that readable screenshots of everything on this TV were
uploaded. Samsung describes ACR under its optional Viewing Information
Services in [its ACR explanation](https://www.samsung.com/us/support/answer/ANS10010616/).

A [2024 academic measurement study](https://arxiv.org/html/2409.06203v1)
found ACR network activity on the Samsung/LG models it tested even when they
were used as HDMI displays. Opting out stopped observed ACR traffic. In those
experiments, Netflix and YouTube playback did not produce the same ACR traffic.
The paper concerns other devices and firmware, not a direct audit of this
2025 U8000F. It does not establish that arbitrary laptop text was uploaded
as readable screenshots.

Relevant disabled app IDs here included `com.samsung.tv.acr`,
`com.samsung.tv.canalysis-service` and
`com.samsung.tv.contents-recognition-dbmgr`. Additional recognition masking
was runtime-only. These observations support a targeted reduction of known
components, not a quantified reduction in tracking traffic.

## Advertising and usage profiles

The cleanup targeted Samsung analytics, PISA/ad-agent, targeted-ad and
promotion records. Samsung's [US privacy policy](https://www.samsung.com/us/account/privacy-policy/)
describes collection of device identifiers, usage information and, depending
on the feature and choices, voice information and information from other
sources. Such data can support personalized services and advertising.

That policy is broader than one TV's observed behavior. We did not decrypt
this TV's traffic, inspect a server-side profile or establish which exact
records had been transmitted. Some differently packaged ad or smart-service
components remained. Streaming apps retain their own privacy practices.

## Voice and context features

The owner did not want Bixby/Alexa wake-word services, voice UI, utterance
suggestions or context-aware helpers. Their registry records, selected
service conditions and enablement preferences were changed accordingly.

A wake-word detector is not by itself evidence of continuous room audio
being uploaded for advertising. Nothing in this investigation established
that claim. Likewise, package names such as `content-grab-service` suggest
a capture-related role but do not alone prove what leaves the TV.

## Useful boundaries

- Use Samsung's consent/privacy controls too; a root modification is not
  required to exercise documented ACR opt-out choices.
- Keep optional-feature removal separate from malware claims. Health,
  ambience and device-integration features may be useful to another owner.
- A firmware/catalog update can restore settings or introduce new paths.
- HDMI-only use does not automatically mean zero tracking; disabling a
  launcher advertisement also does not establish zero tracking.
- No camera spying, perpetual room recording or arbitrary screenshot
  exfiltration was demonstrated on this device.

If stronger assurances are needed, perform a separate, consented network
measurement with explicit before/after tests and document its blind spots.
Do not publish private viewing history, identifiers or account traffic.


## Q60C firmware 2220.4 verification, October 2026

On the separately inspected TQ65Q60CAUXXC, Viewing Information Services,
Interest-Based Advertising and Online Remote Management were already opted
out in the TV's consent database. Mandatory acknowledgments and account
sessions were retained. No account cookies were cleared. No safe device-wide
optional-cookie switch was found in the inspected configuration; streaming
apps retain their own cookie controls.

The cleanup disabled 43 exact app records for Samsung advertising/analytics,
ACR, voice/context assistants and optional SmartThings/Health/Gaming Hub
components. Verification found zero unrelated app-flag or other app-record
field changes. Nine verified vendor stop markers cover ten service units.
Voice activation/recognition and TV Plus autorun were disabled. Country-based
app preloading was disabled in its preference and typed WAS config field.

Four units are **runtime-masked only**: contents-recognition-service,
media-analytics-service, ipa and org.tizen.tv.sp-kpi. These masks expire on a
full TV reboot. Consent choices, app flags, preferences and stop markers
persist. The read-only system partition was retained. No network measurement
established zero telemetry.

Consistent database backups and targeted inverse scripts are stored privately
on the PC and TV. Recovery restores selected fields and owned markers/masks,
without replacing live registry databases wholesale. Display, network, audio,
launcher identity, account state and remote grabs were checked afterwards.


## Automatic reapplication on the Q60C

The owner subsequently requested TV-only persistence. The optional headless
bootstrap in [Boot privacy](11-boot-privacy.md) was installed and verified on
a fresh kernel boot. It re-acquires root through localhost and reapplies the
four runtime masks automatically. A token bridge retains PC management.
Four additional voice aliases were disabled, bringing the reviewed app set
to 47. This does not establish that closed firmware has no other telemetry
paths. Root acquisition takes time after boot; the four masks do not provide
a pre-kernel or zero-startup-window guarantee.
