# Changelog

## 0.6.7 Beta - 2026-08-21

- Reorganized navigation into nested sections for each ritual and its corresponding map region.
- Expanded Ritual 1 into a Story-ordered route: prerequisite, altar, Drug Facility, Fishing Dock, Jeep, upper Gold Mine elevator, Plane Crash, and Lambda-2.
- Added important destinations for the later Story regions, including Bamboo Bridge, Anaconda Island, Airport, Omega Camp, and Yabahuaca Village.
- Calibrated exact positions and facing directions for the Jeep, upper Gold Mine elevator, Plane Crash, and Lambda-2 from in-game captures.
- Added terrain-height adjustment for destinations that still use GPS-to-world conversion.
- Preserved progression-aware access for Rituals 3 and 4, the pre-teleport snapshot, and safe return controls.
- Fixed GHMod artwork metadata to load the embedded `icon.png` and `banner.png` files directly.
- Verified the embedded icon at 512 x 512 and banner at 660 x 200 pixels.
- Promoted the completed and user-tested navigation route to Beta.

## 0.6.6-test - 2026-08-21

- Added the Plane Crash Cave at 40W 24S after the upper Gold Mine elevator and before Lambda-2.
- Preserved the exact captured Lambda-2 destination after inserting the new route entry.

## 0.6.5-test - 2026-08-21

- Replaced the approximate Jeep destination with the exact captured position and facing direction.
- Replaced the upper Gold Mine elevator destination with the exact captured position and facing direction.
- Replaced Lambda-2 with the final captured position and facing direction.

## 0.6.4-test - 2026-08-21

- Added a main-menu button to capture an exact teleport destination to the game log.
- The capture records precise world position, facing direction, rotation, and converted GPS coordinates.

## 0.6.3-test - 2026-08-21

- Added a dedicated Jeep arrival point a few meters south of the vehicle.
- Made the player face the Jeep immediately after teleporting so it is visible on arrival.

## 0.6.2-test - 2026-08-21

- Added the Fishing Dock/Pier at 51W 19S before the Jeep destination.
- Moved Lambda-2 into the Ritual 1 route and placed it last.
- Kept the Ritual 1 route ordered according to Story progression.

## 0.6.1-test - 2026-08-21

- Reordered Ritual 1 so its prerequisite is first and its altar is second.
- Removed the redundant Abandoned Tribal Village destination.
- Kept the validated Drug Facility arrival unchanged.
- Moved the Jeep arrival toward the dry riverbed beside the damaged bridge.
- Replaced the lower Gold Mine destination with the upper elevator entrance at 40W 18S.

## 0.6.0 - 2026-08-21

- Added nested teleport sections for each ritual and its corresponding map region.
- Added ten important Story-mode destinations distributed across the four ritual sections.
- Added GPS-to-world conversion and terrain-height adjustment for safer arrivals.
- Preserved progression locks, the pre-teleport snapshot, and safe-return controls.
- Fixed GHMod artwork metadata to use the embedded `icon.png` and `banner.png` directly.
- Verified embedded artwork at 512 x 512 and 660 x 200 pixels.

## 0.5.3 - 2026-08-09

- Resized and reframed the banner to the GHMod-recommended 660 x 200 pixels.
- Resized the square launcher icon to 512 x 512 pixels.
- Reduced embedded artwork size while preserving the approved visual identity.

## 0.5.2 - 2026-08-09

- Added the GHMod-supported `icon` and `banner` metadata fields.
- Added the official GHMod version endpoint to prevent `Unknown` version display.
- Renamed the internal mod class and source file to `RitualNavigator`.
- Kept the banner and icon embedded in the `.ghmod` package.

## 0.5.1 - 2026-08-09

- Added the official Ritual Navigator banner and icon.
- Embedded both visual assets in the GHMod package.
- Updated release documentation and package metadata.

## 0.5.0 - 2026-08-09

- Added a progression-aware menu for the four Story rituals.
- Added navigation to the first ritual's burned-backpack prerequisite.
- Added safe altar teleport targets with consistent player orientation.
- Added optional ground delivery of ritual ingredients, firewood, and a lit torch.
- Added a 30-second torch delivery window followed by normal fuel consumption.
- Added safe return and active-dream stop controls.
- Restricted the menu to active Story gameplay.
- Added native Green Hell cursor handling and player-input blocking.
- Added the English, nearly transparent release menu.
- Removed development probes and reduced runtime logging.
