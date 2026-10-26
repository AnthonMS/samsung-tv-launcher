# Media setup and everyday use

## Dock and navigation

Home opens the first visible dock tile. Top-right circles navigate Home, Apps and Settings. Back navigates the launcher's history; it no longer exits to a previous HDMI input. Media rows stay in a continuous vertical layout. Focus scrolls only enough to expose the selected row.

Hold OK on an app tile to enter customize mode, or use the square Customize app row settings tile. Tiles jiggle; OK picks up the current tile, Left/Right reorders it and Up toggles its visibility. Hidden tiles remain dimmed while editing. Back finishes. Normal OK launches the highlighted item.

HDMI 1-3 are numbered gradient tiles, with optional detected device names. The port is not tied to a hardcoded Nintendo/PlayStation icon. Names scroll when too long. User tests confirmed layout and editing; device-detection behavior on a connected Switch was not verified in the last test because it was unplugged.

The dock counter is focused index / visible total. Small arrows sit below the numbers and emphasize the side containing off-screen tiles. With no off-screen tiles the arrows hide and the numbers move down. Only the focused tile shows its name, overlaid inside its image.

## Clock and theme

Settings contains Time & date and Theme tiles. The default is a 24-hour clock. Users can choose 12/24-hour time, show/hide the date and select the date pattern. Light and dark palettes are selectable. The smooth gradient replaced the earlier pixelated background. These settings persist in the app's private data.

## YouTube

Install the desired playback app first and verify it works normally. Discover its actual app ID on the TV. Configure `youtube-client.local.json` from the empty example using a compatible OAuth TV/device-code client you may use. Rebuild and install the private asset package.

On TV, open Settings, YouTube & media, YouTube account. Request a code, open the displayed verification address on a phone/computer, enter the code and approve the account. Do not publish the code, refresh token, client secret or account cache. This is a fresh enrollment on a new TV, not a transfer of somebody else's credentials.

Enable recommendations and choose the playback app in settings. The row uses thumbnails, inset duration badges and normal D-pad focus. YouTube and TizenTube use different launch payload handling where required. The native Cobalt-style route passes the video ID directly; a delayed second request is reserved for the legacy web wrapper.

Test two different videos, returning Home between them. A single successful cold start did not catch the earlier warm-launch bug. The owner confirmed immediate playback after the fix. YouTube/TizenTube can look identical, and preference text alone does not independently identify the foreground app; verify its installed ID/process when necessary.

## Jellyfin and Moonfin

Install Moonfin and/or Jellyfin from its legitimate distribution, then sign that playback app into your server and test a movie and live channel there first. The inspected Moonfin app ID was `MoonfinApp.moonfin`; another build may use a different ID and require code/catalog adaptation. Do not assume an app label is an ID.

In the launcher, set the Jellyfin server address, then select Jellyfin account. On your phone/computer open that server's Profile, Quick Connect and approve the displayed code. The launcher and playback app have separate sessions; pairing one does not automatically log the other in. Server access tokens are saved on the TV with restricted file permissions.

Choose Moonfin or Jellyfin as the media playback app. Continue watching combines resume items with Next up while avoiding duplicate items. Playback carries the selected item and resume position. Live TV shows channel names and current programmes without a duration/progress badge. Display-only suffix cleanup removes labels such as `(Geo-blocked)` and `(1080p)`; it does not bypass region restrictions or repair a stream.

Each recommendation/media row can be disabled independently in settings. Network/image work happens after the initial scene; feeds and thumbnails are cached and bounded. A backend that is restarting or a broken IPTV source can still fail. Always compare the same channel in Moonfin's own guide before diagnosing a launcher bug.

## Moonfin guide and rocker bridge

The launcher uses Moonfin's existing playback/navigation callbacks, not synthetic arrow-key macros. [Remote setup](REMOTE.md) explains the small guarded patch needed for EPG and channel actions. Both the server account and Moonfin login must be ready. The bridge intentionally waits at login panels instead of bypassing authentication.

## Account and preference files

Look in the repurposed package's actual app data directory, discovered through `Application.Current.DirectoryInfo.Data`. Relevant files include `youtube-settings.json`, `media-settings.json`, `media-account.json`, `media-feed.json`, `remote-buttons.json` and generated image/cache files. Other UI preference names can be found in the source. Never publish the directory or use another person's tokens. Updating the code should preserve it. Signing out is an explicit action, not an installation step.
