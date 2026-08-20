# Changelog

## [0.1.0] - 2026-08-20

### Added

- drop mod-side recentring and quieten the logs

### Fixed

- give the forward lean its own travel budget again

## [0.0.2] - 2026-08-18

### Fixed

- migrate to the per-connection smoothing pair in cameraunlock-core
- match stub member kinds to the shipped Unity assemblies

## [Unreleased]

### Fixed

- `HeadTracking_BOOT.log` and `%TEMP%\HeadTracking_BOOT_ERROR.log` now start
  fresh on every launch. They were appended to, so the boot log a user sends in
  carried every previous session's lines and the current run had to be picked
  out of the pile.
- an error inside the camera restore hook is logged once per distinct message
  instead of every frame. That path runs in `OnPostRender`, so a persistent
  failure wrote roughly 17 MB an hour into `HeadTracking.log` at 60fps.

### Changed

- the `[DIAG]` status line in `HeadTracking.log` is written when the state
  changes rather than every 5 seconds. A one-hour session used to add ~68 KB
  restating the same three flags.
- Removed recentring from the mod entirely, along with the `Home` / `Ctrl+Shift+T`
  hotkey and the `RecenterKey` config entry. Every tracker app centres itself, so a
  mod-side centre was a second centre in series with the tracker's own and the two
  drifted apart. The tracker pose is now applied as sent. Centre in your tracker app
  instead: opentrack's Center bind, or the CENTER button in Headcam.
- replace the single `Smoothing` config key with `LocalSmoothing` (default 0.0) and `RemoteSmoothing` (default 0.15), selected per connection from the packet source address and covering both rotation and position
- remove the hidden 0.15 baseline smoothing floor, so a tracker running on this PC now gets zero-latency tracking by default

## [0.0.1] - 2026-06-07

### Added

- add HeadTrackingSession and expand C++ core with RE Engine, Unreal, and tracking-session modules
- aim projection, reframework/unreal hooks, input/logging hardening, games
- add Mass Effect Legendary Edition to games catalog
- expand games catalog, fix unicode games.json read, stage launcher manifest
- add Pacific Drive to games catalog
- add Homeworld: Remastered Collection to games catalog
- add manifest-mode installer validator and ASI loader subdir support
- authenticate GitHub API requests via env token when present
- add R.E.P.O. detection data

### Fixed

- fail fast in ASI dev-deploy when the game is running
- restore il2cpp camera position by undoing applied local delta
- set SO_REUSEADDR so the receiver reclaims its port on relaunch

### Other

- reframework: strip VR runtime DLLs on install for flatscreen mode
- reframework: cache GetValue method and avoid per-call heap in ArrayGetValue; data: add BioShock Infinite
- uninstall: remove reframework_revision.txt marker dropped at game root
- install: render MOD_CONTROLS multi-line via percent expansion
- Add YAPYAP to games.json
- powershell: write state file BOM-less so Lopari JSON parser accepts it
- Use shared TrackingMode enum from CameraUnlock.Core
- Add launcher manifest mode and route CI builds through pixi run package
- powershell: stop redirecting git stderr in Invoke-VersionCommit
- Add PATCH_MARKER config var to install/uninstall scripts

## [Unreleased]

Initial development. No releases yet.
