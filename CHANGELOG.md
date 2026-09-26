# Changelog

## [Unreleased]

### Changed

- Settings move to `Painscreek_Data\Managed\CameraUnlock.ini`. Earlier versions of the mod kept these settings in `HeadTracking.cfg`, in the same folder. The first time this version starts and finds no `CameraUnlock.ini`, it reads your settings from `HeadTracking.cfg` and writes them into `CameraUnlock.ini`. It never changes `HeadTracking.cfg`, and does not read it again while `CameraUnlock.ini` exists.
- A setting that the defaults the README shows set to `default` is written as `default` when the value imported for it equals its default at that start, which is the value `Defaults.ini` gives it, or the built-in value where `Defaults.ini` gives none. It then follows `Defaults.ini`. Every other setting is written with the value imported for it.
- `RotationEnabled` and `PositionEnabled` are one setting here, the tracking mode, so both are written as `default` or neither is.
- Comments, and keys the mod never read, are not carried over. Nor are these, where your old file had them:
  - A sensitivity, scale, deadzone, response curve or axis inversion you changed from its default. Set these in your tracker instead.
  - Reticle settings, and a key that toggled the reticle.
  - The setting for a feature that earlier versions shipped switched off while it was untested. It now follows the mod's default.
- `EnableOnStartup`, `RecenterKey` and `AimDecoupling` in `HeadTracking.cfg` are not carried over either. Earlier versions read them and did nothing with them: head tracking always started on, there was no recenter key, and aim was always decoupled.
- An older version of the mod reads `HeadTracking.cfg` and never reads `CameraUnlock.ini`, so a setting you change after updating is not in `HeadTracking.cfg`.
- Deleting only `CameraUnlock.ini` makes the next start read `HeadTracking.cfg` again. To go back to the defaults, replace everything in `CameraUnlock.ini` with the defaults the README shows. Every setting they set to `default` then follows `Defaults.ini`.
- Hotkeys are written as key names, and each hotkey lists every key that triggers it, the Ctrl+Shift chord included: `ToggleKey=End, Ctrl+Shift+Y`.
- A hotkey bound to a plain key no longer fires while Ctrl and Shift are both held, so Ctrl+Shift with that key reaches only a binding that names the chord.
- On Linux and macOS without Wine or Proton, this version reads its settings and saves none: it creates no `CameraUnlock.ini`, reads your settings from `HeadTracking.cfg` again at every start while there is no `CameraUnlock.ini`, and a change made in game lasts until the game closes.
- The tracking mode `Page Up` picks and the yaw mode `Page Down` picks are saved to `CameraUnlock.ini`, and the next start begins in them. Earlier versions always started in rotation and position, and in the yaw mode the file named. `End` still changes the session only.
- The settings sit in sections in `CameraUnlock.ini`: `[Network]`, `[General]`, `[Smoothing]`, `[Position]` and `[Hotkeys]`. The import carries each value over.
- Where the key parse earlier versions ran fails on an old file's `ToggleKey` or `YawModeKey` (under .NET a number too large to be a key code does this, for example `ToggleKey = 99999999999`), the file is not imported. The mod runs that session with the Ctrl+Shift chord for that action, saves nothing, says so in `HeadTracking.log`, and tries again at the next start.
- `uninstall.cmd` leaves `CameraUnlock.ini` and `HeadTracking.cfg` in place.
- The mod ships a fourth DLL, `CameraUnlock.Core.Unity.dll`, which reads the hotkey lists. The installer, the uninstaller and Lopari's manifest carry it.

- pin every GitHub Action to a commit SHA with a trailing version comment
  instead of a mutable tag.
- the `[DIAG]` status line in `HeadTracking.log` is written when the state
  changes rather than every 5 seconds. A one-hour session used to add ~68 KB
  restating the same three flags.
- Removed recentring from the mod entirely, along with the `Home` / `Ctrl+Shift+T`
  hotkey and the `RecenterKey` config entry. The tracker app owns centring, so a
  mod-side centre was a second centre in series with the tracker's own and the two
  drifted apart. The tracker pose is now applied as sent. Centre in your tracker app
  instead: opentrack's Center bind, or the CENTER button in Headcam.
- replace the single `Smoothing` config key with `LocalSmoothing` (default 0.0) and `RemoteSmoothing` (default 0.15), selected per connection from the packet source address and covering both rotation and position
- remove the hidden 0.15 baseline smoothing floor, so a tracker running on this PC now gets zero-latency tracking by default

### Added

- `EnableOnStartup` in `CameraUnlock.ini` sets whether head tracking is on when the game starts. `End` still turns it on and off for the session only and never changes the file.
- `CycleTrackingModeKey` in `CameraUnlock.ini` sets the keys that cycle the tracking mode. They were fixed at `Page Up` and `Ctrl+Shift+G`, which stay the default.
- A setting set to `default` in `CameraUnlock.ini` takes its value from `Defaults.ini`, which every head tracking mod that keeps its settings in `CameraUnlock.ini` reads. Head tracking mods that keep their settings in another file do not read it, and neither do earlier versions of this mod. Writing a value in place of `default` changes that setting for this game only. When the mod saves a setting that a hotkey changed in game, it writes the new value in place of `default`, so that setting no longer follows `Defaults.ini` in this game until you set it to `default` again.
- `Defaults.ini` is `%AppData%\CameraUnlock\Defaults.ini` on Windows; `$XDG_CONFIG_HOME/CameraUnlock/Defaults.ini` on Linux, or `~/.config/CameraUnlock/Defaults.ini` where `XDG_CONFIG_HOME` is not set, under Wine and Proton too; and `~/Library/Application Support/CameraUnlock/Defaults.ini` on macOS. The mod's log, where it writes one, names the file it read.
- When the mod starts and finds no `Defaults.ini`, it creates one holding the built-in values, unless Windows runs the game as a packaged app, or the game runs on Linux or macOS without Wine or Proton. The mod never changes `Defaults.ini` after that.

### Removed

- The reticle settings, `ShowReticle` and `ReticleColor`. Earlier versions read them and drew no reticle of their own: the game's cursor follows the aim, as it always did.
- The sensitivity and axis inversion settings: `YawSensitivity`, `PitchSensitivity`, `RollSensitivity`, `InvertYaw`, `InvertPitch` and `InvertRoll`. Set these in your tracker app instead.
- With these settings at their shipped defaults the camera moves as it did before.
- stop tracking `tools/Mono.Cecil.dll`. It is extracted from the vendored
  `.nupkg` by `scripts/ensure-cecil.ps1`, so the committed copy was a duplicate
  build artifact; `tools/` is now gitignored.

### Fixed

- ship `LICENSE`, `licenses/cameraunlock-core-LICENSE.txt` and a complete
  `THIRD-PARTY-NOTICES.md` in the installer ZIP. `CameraUnlock.Core.dll` is MIT
  under a different copyright holder from this repo's own LICENSE and was being
  redistributed with its notice nowhere in the package, and our own MIT notice
  was not shipping either. The packager now throws on a missing licence file
  instead of skipping it, and `validate-release` checks for all four inside the
  built ZIP.
- correct `THIRD-PARTY-NOTICES.md`: the Mono.Cecil version rendered as a literal
  `:` placeholder, and cameraunlock-core was described as compiled into
  `PainscreekHeadTracking.dll` when it ships as its own DLL.
- `HeadTracking_BOOT.log` and `%TEMP%\HeadTracking_BOOT_ERROR.log` now start
  fresh on every launch. They were appended to, so the boot log a user sends in
  carried every previous session's lines and the current run had to be picked
  out of the pile.
- an error inside the camera restore hook is logged once per distinct message
  instead of every frame. That path runs in `OnPostRender`, so a persistent
  failure wrote roughly 17 MB an hour into `HeadTracking.log` at 60fps.

## [0.1.0] - 2026-08-20

### Added

- drop mod-side recentring and quieten the logs

### Fixed

- give the forward lean its own travel budget again

## [0.0.2] - 2026-08-18

### Fixed

- migrate to the per-connection smoothing pair in cameraunlock-core
- match stub member kinds to the shipped Unity assemblies

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
