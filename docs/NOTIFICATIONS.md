# Home Assistant TV notifications

Quiet Home displays a silent top-right overlay above the current app. HA supplies the title, message, optional image or live camera, duration and up to three named action buttons. This is part of the owner's custom Q60C development setup, requiring the modified launcher and QuietThings integration.

## Remote behavior

A notice captures navigation immediately. Arrows choose a button, OK activates it and Back dismisses. The first selection is always the harmless OK/dismiss button. Any selected button closes the notice; timeout, replacement and HA dismissal also release its temporary controls. The underlying app keeps playing and regains the controls when the notice closes. Normal volume and power remain TV functions.

The implementation keeps the media window's compositor focus while exclusively routing notification navigation. This avoids pausing or foregrounding Home merely to show a doorbell. Existing launcher app/channel shortcuts are consumed while the notice is visible and resume afterward. Tests cover automatic capture, an explicit action, timeout and subsequent remote pause/play in YouTube. Owner visual testing on other apps, protected-video modes, HDMI and Samsung system screens remains useful.

## Settings and connection

Settings, More settings, Home Assistant contains persistent switches for HA access, notifications and action buttons. Disabling them closes the notice and discards queued actions as appropriate. Local Community Apps result notices use the same window independently of the HA notification switch.

HA connects to the TV using its existing private token and optional playback companion port, normally 18766. There is no HA server destination or HA access token stored on the TV. Edit TV connection details in the QuietThings integration in HA. The authenticated API is local HTTP for a trusted network, not an internet-facing encrypted service.

## Calling the service

In HA Developer Tools, Actions, select `quietthings.notify` and choose the TV integration entry. Text fields, image sources and button lists can be templates.

```yaml
action: quietthings.notify
data:
  entry_id: YOUR_QUIETTHINGS_ENTRY_ID
  title: Front door
  message: Someone rang the doorbell
  camera_entity: camera.front_door
  duration_seconds: 30
  actions:
    - action: UNLOCK_FRONT_DOOR
      title: Unlock Door
```

The TV adds its own OK/dismiss button. Once a new notice is prepared and accepted by the TV, it replaces the visible one immediately, with its own timeout and buttons. There is no display queue and the old notice does not return afterward. Its old camera preview stops, and delayed image updates cannot replace the new notice. A sleeping TV is not automatically woken and there is no queue that displays an old doorbell hours later. HA reports request errors to the calling action. An optional service response includes the generated `id` and `displayed: true` after TV acceptance; this does not prove a person read it.

Duration is 5 to 120 seconds. Titles are at most 100 characters and body messages 500. Each actionable notification needs a unique ID; omit `id` to generate one, or supply a new bounded letters/digits/underscore/colon/hyphen ID each time.

## Live cameras and snapshots

`camera_entity` now means a live-updating preview by default. HA obtains fresh camera frames, resizes them and updates the existing TV image without resetting the notice timeout, selected button or action ID. Frames are fetched only while this notice remains visible. Dismissal, replacement, expiry or integration unload ends the preview. Temporary failures show Camera delayed or Camera unavailable rather than labeling an old image as live indefinitely.

This is a low-frame-rate live camera preview, not a full-motion video player. The default interval is one second between frame requests, plus camera/network processing; `camera_refresh_seconds` accepts 0.5 to 10. It deliberately avoids starting another hardware video decoder over the current TV player. It is silent and has no camera audio. Actual testing delivered six fresh frames in eight seconds while YouTube kept playing.

For a single image from the camera, explicitly choose snapshot mode:

```yaml
action: quietthings.notify
data:
  entry_id: YOUR_QUIETTHINGS_ENTRY_ID
  title: Camera snapshot
  message: One still image
  camera_entity: camera.front_door
  camera_mode: snapshot
```

HA uses the existing camera integration. The TV receives bounded image bytes, not a camera URL, password or HA credential. A camera that cannot return images through HA cannot provide this preview. The installed doorbell script falls back to a text notice if its initial camera request fails.

## Image URLs, HA www and animated GIFs

Use `image_url` instead of `camera_entity` for an image attachment. HA downloads and prepares it, then sends the result to the TV. HTTP and HTTPS URLs can point to HA, another LAN server or a public website. `/local/...` is also accepted directly and maps safely to HA's www folder. A file in `/config/www/snapshots/example.jpg` can therefore be used as:

```yaml
action: quietthings.notify
data:
  entry_id: YOUR_QUIETTHINGS_ENTRY_ID
  title: Saved snapshot
  message: This could be any image
  image_url: /local/snapshots/example.jpg
  duration_seconds: 20
```

Or use an ordinary link:

```yaml
action: quietthings.notify
data:
  entry_id: YOUR_QUIETTHINGS_ENTRY_ID
  title: Animation
  message: An animated image from a URL
  image_url: https://example.com/notification.gif
  duration_seconds: 20
```

JPEG, PNG, WebP, BMP, TIFF and ICO were exercised through the HA decoder. Static images are converted to a bounded TV-compatible JPEG. GIF animation is preserved; animated WebP/APNG readable by Pillow can also be converted to GIF. Animated output is resized to at most 520 by 285 with at most 180 frames and 1 MiB, and loops while the notice is visible. Oversized media reports an error. Static output is at most 960 by 540 and 128 KiB; download input is bounded to 8 MiB with limited redirects and timeouts. Unsupported formats, including SVG/vector documents that Pillow cannot decode, report an error rather than display an unsafe document. Videos and audio are outside this feature.

Camera and image URL are mutually exclusive. URL fetches do not attach an HA bearer token. URLs with embedded account credentials and non-HTTP filesystem schemes are rejected. `/local` reads stay within www, including after decoding and symlink resolution. HA's normal www public-access rules still apply to files placed there.

TV image files use private storage permissions and are deleted when replaced or dismissed, with leftover cleanup on startup. HA camera frames stay in memory; a live camera does not write snapshots into www. Existing www images stay wherever their owner placed them.

## Callback actions

Buttons emit `quietthings_notification_action` in HA with `entry_id`, `notification_id`, `action` and `event_id`. Button names are arbitrary bounded identifiers chosen by the calling HA automation. The TV does not execute HA service names.

The integration only emits an action advertised in a pending notification it sent. It validates age and identity and persists acknowledgement before firing. Events expire after 30 seconds, so restarting HA cannot replay an already acknowledged action. This favors at-most-once handling over repeating a physical operation; delivery may be dropped on a crash or prolonged outage.

An automation matches the action explicitly:

```yaml
triggers:
  - trigger: event
    event_type: quietthings_notification_action
    event_data:
      entry_id: YOUR_QUIETTHINGS_ENTRY_ID
      action: UNLOCK_FRONT_DOOR
conditions:
  - condition: state
    entity_id: lock.front_door
    state: locked
actions:
  - action: lock.unlock
    target:
      entity_id: lock.front_door
```

To dismiss a matching notice from HA:

```yaml
action: quietthings.dismiss_notification
data:
  entry_id: YOUR_QUIETTHINGS_ENTRY_ID
  id: THE_NOTIFICATION_ID
```

An old ID cannot dismiss a newer notice.

## Installed doorbell and person-detection example

The owner's HA now has `/config/packages/quiet_home_tv_notifications.yaml`, containing the shared `script.door_bell_notify_tv` and three enabled automations:

- Quiet Home: doorbell camera notification listens to the existing physical deCONZ doorbell's single, double and hold presses. It calls the script, which checks that the TV is on. Existing phone notifications and the triple-press lock behavior are unchanged.
- Quiet Home: front door person camera notification listens to `binary_sensor.detection_zone_person_occupancy` changing from off to on, the same detection zone used by the front-door spots. It calls the shared script with `notification_kind: person`. The script checks TV power. Unlike the light automation, notifications do not depend on darkness, the light's state or the motion-light enable switch. There is one notification per clear-to-occupied transition, not one per frame or person count change.
- Quiet Home: Unlock Door notification action matches the TV entry, `UNLOCK_FRONT_DOOR` and a `doorbell-tv-` or `person-tv-` notification ID. It checks that the door is still locked and the TV is on, then calls `lock.unlock` on the front door.

The script generates a unique ID and only offers Unlock Door when the lock is locked. It defaults to a live `camera.front_door` preview for 30 seconds and falls back to text on an initial camera failure. Doorbell callers omit `notification_kind`; person callers receive a distinct Person at the front door title/message/ID. Either kind replaces the current notice; a newer person notice can replace a doorbell notice and vice versa. Use this package as the local example for other notifications/actions. The portable template is in the QuietThings repository at `examples/doorbell-tv.yaml`, with an entry-ID placeholder.

The example's trigger, TV-off behavior, conditional buttons, camera fallback and callback matching were tested in isolated HA with a mocked lock service. On the real system the installed notification script was called and safely dismissed; the real lock state and unlock callback's last-trigger time stayed unchanged. Development never sends the live `UNLOCK_FRONT_DOOR` test action or operates the actual lock.

## Implementation

Native UI: `Home.Notifications.cs`. Persistent controls: `HomeAssistantPreferences.cs` and `Home.IntegrationSettings.cs`. Shared validation: `QuietThingsPlayback/TvNotifications.cs`. HA service and camera lifecycle: QuietThings `notifications.py`; image decode/download: `notification_media.py`.

Authenticated companion routes are POST `/v1/notifications`, POST `/v1/notifications/dismiss`, GET `/v1/notifications/status`, POST `/v1/notifications/image` and GET `/v1/notifications/events?after=N`. Image updates match the current ID and increasing frame sequence; they never extend timeout or recreate remote focus. Notification bodies have separate bounds; ordinary transport commands remain limited to 1 KiB. Capabilities advertise `notification_media_version: 2`, live images and GIF support.
