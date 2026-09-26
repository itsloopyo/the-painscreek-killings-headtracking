# The Painscreek Killings Head Tracking

![The Painscreek Killings running with this mod](https://raw.githubusercontent.com/itsloopyo/the-painscreek-killings-headtracking/main/assets/readme-clip.gif)

An unofficial head tracking mod for The Painscreek Killings that moves the view with your head while your mouse keeps control of the cursor, driven by OpenTrack over UDP, with no VR headset required.

> **Settings have moved.** This version keeps its settings in `Painscreek_Data\Managed\CameraUnlock.ini`.
> The first time it starts it reads your settings from the old
> `Painscreek_Data\Managed\HeadTracking.cfg` into the new file, and leaves the old file as it was.
> Sensitivity, axis inversion and reticle settings are gone: set sensitivity and inversion in
> your tracker. [Configuration](#configuration) has the details.

## Features

- **Decoupled look and aim** - head tracking moves the camera; aim stays on your mouse
- **6DOF positional tracking** - lean and peek with head position
- **Works with any OpenTrack compatible tracker** - free options available for PC, iOS and Android

## Requirements

- [The Painscreek Killings](https://store.steampowered.com/app/252270/The_Painscreek_Killings/) on Steam
- A tracker that sends OpenTrack UDP pose data to port 4242: [OpenTrack](https://github.com/opentrack/opentrack) with a webcam, or a phone app with an OpenTrack UDP output
- Windows 10 or 11 (64-bit)

## Installation

### Lopari

Download [Lopari](https://lopari.app), choose **The Painscreek Killings**, and click
**Play with head tracking**.

### Standalone Installer

1. Download the `-installer.zip` from the [Releases page](https://github.com/itsloopyo/the-painscreek-killings-headtracking/releases).
2. Extract the ZIP anywhere.
3. Double-click `install.cmd`.
4. Configure your tracker to send OpenTrack UDP to `127.0.0.1:4242`.
5. Launch the game.

The installer auto-detects your game install via the Steam registry. If it can't find it:

- Set the `PAINSCREEK_PATH` environment variable to your game folder, or
- Pass the path as a positional argument: `install.cmd "D:\Games\The Painscreek Killings"`

### Manual Installation

For users who prefer to place files by hand. This mod uses a Mono.Cecil bootstrap patcher: the mod DLLs are loaded by a small instruction injected into `Assembly-CSharp.dll`. There is no separate mod loader to install, but `Assembly-CSharp.dll` must be patched once.

1. Download the `-installer.zip` from the [Releases page](https://github.com/itsloopyo/the-painscreek-killings-headtracking/releases) and extract it anywhere. Copy `PainscreekHeadTracking.dll`, `CameraUnlock.Core.dll`, `CameraUnlock.Core.Unity.dll`, and `Mono.Cecil.dll` from the extracted `mod\` folder into your game's `Painscreek_Data\Managed\`.
2. Patch `Assembly-CSharp.dll` once by running `install.cmd` from the same installer ZIP and pointing it at your game directory:
   ```
   install.cmd "C:\Path\To\The Painscreek Killings"
   ```
   The patcher backs up the original as `Assembly-CSharp.dll.original` before modifying it.

## Setting Up OpenTrack

The mod listens for OpenTrack pose data on UDP port `4242`, on every network
interface. One datagram is six little-endian 64-bit floats in the order
`x, y, z, yaw, pitch, roll`: position in centimetres, rotation in degrees, 48
bytes in total. Anything that sends that to that port drives the view.
OpenTrack's **UDP over network** output sends exactly this, and the steps below
set it up.

1. Install [OpenTrack](https://github.com/opentrack/opentrack/releases).
2. Pick a tracker under **Input**, using the notes below.
3. Set **Output** to **UDP over network**, host `127.0.0.1`, port `4242`.
4. Press **Start**. Tracking and the game can start in either order.

### Webcam

OpenTrack ships a `neuralnet tracker` input that reads a plain webcam. Select it
under **Input**, pick your camera in its settings, and use the output settings
above. How well it tracks depends on your camera and your lighting, so try it
before buying anything.

### Phone

A phone app can reach the mod directly, with no OpenTrack on the PC, if it sends
the datagram described above. Point it at this PC's IP address (run `ipconfig`
to find it) on port `4242`. Not every phone tracker speaks this protocol, so
check yours for an OpenTrack or UDP output option first. [Headcam](https://headcam.app)
sends it, and I wrote it so decent tracking is free for anyone who already owns
a phone.

Sending direct works when the app filters its own signal on the device. The
mod's smoothing is sized to take the edge off a clean signal rather than to
rescue a noisy one, so a raw feed sent direct will jitter. If it does, point the
app at OpenTrack's **UDP over network** *input* on some other port, say 5252,
and let OpenTrack's filters and curves clean it up before its output forwards to
`127.0.0.1:4242`.

Anything arriving from outside `127.0.0.0/8` counts as a remote connection and
is smoothed with `RemoteSmoothing` rather than `LocalSmoothing`. That includes a
tracker on this very PC that sends to the machine's own LAN address, because the
mod reads the source address and not the machine.

### Headset or other hardware

If your device has an OpenTrack input driver, select it under **Input** and use
the same output settings. OpenTrack's own **Input** list is the authority on
what it can read; the mod only ever sees what OpenTrack sends.

### Centring

Centring belongs to your tracker. The mod subtracts no centre of its own: it
applies the pose it receives exactly as it arrives, so a stream of zeros holds
the view where the game itself puts it. Press the centre control in your tracker
(OpenTrack's **Center** bind, or the CENTER button in Headcam) and the tracker
zeroes its own output, which leaves the view centred with the mod doing nothing.

That is why there is no centre hotkey here and nothing to re-centre in game. Two
centres in series would drift apart, because each side re-centres at moments the
other cannot see, and you would end up pressing twice to centre once. If the
view sits off to one side, centre it in the tracker.

## Controls

Two equivalent binding sets - use whichever your keyboard has:

| Action              | Nav-cluster | Chord           |
|---------------------|-------------|-----------------|
| Toggle tracking     | `End`       | `Ctrl+Shift+Y`  |
| Cycle tracking mode | `Page Up`   | `Ctrl+Shift+G`  |
| Toggle yaw mode     | `Page Down` | `Ctrl+Shift+H`  |

There is no recenter key. The mod applies the pose your tracker sends as-is, so
centre it in the tracker app: opentrack's Center bind, or the CENTER button in
Headcam.

`Page Up` / `Ctrl+Shift+G` cycles tracking mode:

1. Normal head-tracked gameplay
2. Positional tracking disabled, rotational tracking enabled
3. Rotational tracking disabled, positional tracking enabled
4. Back to normal

`Page Down` / `Ctrl+Shift+H` toggles yaw mode between **world-space** (default, horizon-locked: yaw always rotates around the world up axis, so the horizon stays level when looking up or down) and **camera-local** (yaw rotates around the camera's current up axis, which produces a leaning/rolling effect at extreme pitches).

The tracking mode and the yaw mode you pick are saved to `CameraUnlock.ini`, and the next start
begins in them. `End` turns head tracking on and off for this session only; whether it is on at the
next start is the `EnableOnStartup` setting.

These are the default keys. Each action reads a list of keys from `CameraUnlock.ini`
(`ToggleKey`, `CycleTrackingModeKey`, `YawModeKey`), and any key in the list fires it, so you can
add, rebind or remove any of them, the chords included.

The game's own cursor follows where the mouse aims while head tracking moves the view. It has no
setting or toggle.

## Configuration

<!-- cameraunlock:config -->
The mod reads its settings from `Painscreek_Data\Managed\CameraUnlock.ini` in the game folder, and creates the file when it starts and finds none. Edit it with any text editor.

A setting set to `default` takes its value from `Defaults.ini`, which every head tracking mod that keeps its settings in `CameraUnlock.ini` reads. Head tracking mods that keep their settings in another file do not read it, and neither do earlier versions of this mod. Writing a value in place of `default` changes that setting for this game only. When the mod saves a setting that a hotkey changed in game, it writes the new value in place of `default`, so that setting no longer follows `Defaults.ini` in this game until you set it to `default` again.

`Defaults.ini` is `%AppData%\CameraUnlock\Defaults.ini` on Windows; `$XDG_CONFIG_HOME/CameraUnlock/Defaults.ini` on Linux, or `~/.config/CameraUnlock/Defaults.ini` where `XDG_CONFIG_HOME` is not set, under Wine and Proton too; and `~/Library/Application Support/CameraUnlock/Defaults.ini` on macOS. The mod's log, where it writes one, names the file it read.

When the mod starts and finds no `Defaults.ini`, it creates one holding the built-in values, unless Windows runs the game as a packaged app, or the game runs on Linux or macOS without Wine or Proton. The mod never changes `Defaults.ini` after that. Edit it with any text editor.

Earlier versions of the mod kept these settings in `HeadTracking.cfg`, in the same folder. The first time this version starts and finds no `CameraUnlock.ini`, it reads your settings from `HeadTracking.cfg` and writes them into `CameraUnlock.ini`. It never changes `HeadTracking.cfg`, and does not read it again while `CameraUnlock.ini` exists.

A setting that the defaults below set to `default` is written as `default` when the value imported for it equals its default at that start, which is the value `Defaults.ini` gives it, or the built-in value where `Defaults.ini` gives none. It then follows `Defaults.ini`. Every other setting is written with the value imported for it. `RotationEnabled` and `PositionEnabled` are one setting here, the tracking mode, so both are written as `default` or neither is.

Comments, and keys the mod never read, are not carried over. Nor are these, where your old file had them:

- Reticle settings, and a key that toggled the reticle.
- A sensitivity, scale, deadzone, response curve or axis inversion you changed from its default. Set these in your tracker instead.
- The setting for a feature that earlier versions shipped switched off while it was untested. It now follows the mod's default.

An older version of the mod reads `HeadTracking.cfg` and never reads `CameraUnlock.ini`, so a setting you change after updating is not in `HeadTracking.cfg`.

Deleting only `CameraUnlock.ini` makes the next start read `HeadTracking.cfg` again. To go back to the defaults, replace everything in `CameraUnlock.ini` with the defaults below. Every setting they set to `default` then follows `Defaults.ini`.

On Linux and macOS without Wine or Proton, this version reads its settings and saves none: it creates no `CameraUnlock.ini`, reads your settings from `HeadTracking.cfg` again at every start while there is no `CameraUnlock.ini`, and a change made in game lasts until the game closes.

The built-in value of each setting set to `default` below:

- `UdpPort=4242`
- `EnableOnStartup=true`
- `WorldSpaceYaw=true`
- `RotationEnabled=true`
- `LocalSmoothing=0.0`
- `RemoteSmoothing=0.15`
- `PositionEnabled=true`
- `ToggleKey=End, Ctrl+Shift+Y`
- `CycleTrackingModeKey=PageUp, Ctrl+Shift+G`
- `YawModeKey=PageDown, Ctrl+Shift+H`

With every setting at its default, the file reads:

```ini
; The Painscreek Killings head tracking settings.
; Comments start with ; and go on their own line. Text after a value is part of the value.
; Hotkeys are key names such as End, PageUp or Ctrl+Shift+Y. Separate several with commas; leave empty for none.
; A setting set to default takes its value from Defaults.ini, which every head tracking mod
; that keeps its settings in CameraUnlock.ini reads: %AppData%\CameraUnlock\Defaults.ini on
; Windows, $XDG_CONFIG_HOME/CameraUnlock/Defaults.ini (normally ~/.config/CameraUnlock) on
; Linux, under Wine and Proton too, and ~/Library/Application Support/CameraUnlock/Defaults.ini
; on macOS. The log names the file it read. Write a value instead of default to change that
; setting for this game only.

[CameraUnlock]
; Written by the mod. Leave this section in place.
ConfigFormat=1

[Network]
; UDP port the mod receives tracker data on (OpenTrack protocol).
UdpPort=default

[General]
; true: head tracking is on when the game starts. ToggleKey turns it on and off.
EnableOnStartup=default
; true: yaw turns around the world's up axis. false: around the camera's own up axis.
WorldSpaceYaw=default
; true: turning your head turns the view.
; Tracking mode at startup, with PositionEnabled. The mode hotkey changes both.
RotationEnabled=default

[Smoothing]
; Smoothing when the tracker runs on this PC. 0 is the least, 1 the most.
LocalSmoothing=default
; Smoothing when the tracker is another device on the network, such as a phone.
; 0 is the least, 1 the most.
RemoteSmoothing=default

[Position]
; true: moving your head moves the view.
; Tracking mode at startup, with RotationEnabled. The mode hotkey changes both.
PositionEnabled=default

[Hotkeys]
; Turns head tracking on and off.
ToggleKey=default
; Changes the tracking mode: rotation and position, rotation only, position only.
CycleTrackingModeKey=default
; Switches yaw between the world's up axis and the camera's own (WorldSpaceYaw).
YawModeKey=default
```
<!-- /cameraunlock:config -->

Smoothing covers both rotation and position. Which of the two values applies is
decided per connection from the packet source address: a tracker running on this
PC uses `LocalSmoothing`, a phone or other network device uses `RemoteSmoothing`.
Switching between them takes effect without restarting the game.

## Troubleshooting

**Mod not loading:**
- Check `HeadTracking.log` in the `Painscreek_Data\Managed\` folder for runtime status.
- If the log doesn't exist, the patcher likely never ran. Check `HeadTracking_BOOT.log` in the same folder and `%TEMP%\HeadTracking_BOOT_ERROR.log` for patcher errors.
- Make sure all four DLLs are present in the Managed folder: `PainscreekHeadTracking.dll`, `CameraUnlock.Core.dll`, `CameraUnlock.Core.Unity.dll` and `Mono.Cecil.dll`.

**No tracking response:**
- Verify your tracker (OpenTrack or phone app) is running and shows movement in its own preview.
- Confirm UDP output is set to `127.0.0.1:4242` (or to your PC's LAN IP for direct phone-to-PC).
- Press `End` to make sure tracking is enabled.
- If the view is off-centre, centre it in your tracker app rather than in the game.
- Check Windows Firewall is not blocking UDP on port 4242.

**Jittery / unstable tracking:**
- Raise `RemoteSmoothing` (phone/network tracker) or `LocalSmoothing` (tracker on this PC) in `CameraUnlock.ini` (try 0.3 to 0.5 first).
- For phone trackers on Wi-Fi, lower the phone's send rate, or use a wired connection / hotspot.
- The mod has no sensitivity settings. Use your tracker's own filters and curves if the source signal is noisy.

**Wrong rotation axis or feels off at extreme angles:**
- Toggle world-space vs camera-local yaw with `Page Down` / `Ctrl+Shift+H`. World-space (default) is horizon-stable; camera-local follows the camera's current up axis.
- The mod has no inversion settings. Use OpenTrack's per-axis "Invert" switches in the Output mapping, or your tracker app's own.

**A config edit had no effect:**
- Make sure nothing follows the value on the line. Text after a value is part of the value, so a note on the same line makes the value unreadable and the setting keeps its default. Put a comment on a line of its own, starting with `;`. `HeadTracking.log` names each line the mod could not read.

**Game crashes on startup:**
- Restore `Assembly-CSharp.dll` from the `.original` backup, or verify game files through Steam.
- Open an issue with the contents of `HeadTracking.log` and `HeadTracking_BOOT.log`.

## Updating

Download the new release and run `install.cmd` again. Your settings in `CameraUnlock.ini` are kept.

## Uninstalling

Run `uninstall.cmd` from the release folder. This removes the mod DLLs and restores the original `Assembly-CSharp.dll` from the `.original` backup. The bootstrap patch is reverted automatically. It leaves your settings, `Painscreek_Data\Managed\CameraUnlock.ini` and the old `Painscreek_Data\Managed\HeadTracking.cfg`, in place. Use `uninstall.cmd /force` to remove everything even if the install state file says we did not install it.

## Building from Source

### Prerequisites

- [Pixi](https://pixi.sh) package manager
- .NET SDK 8.0+

### Build

```bash
git clone --recurse-submodules https://github.com/itsloopyo/the-painscreek-killings-headtracking.git
cd the-painscreek-killings-headtracking
pixi run build
pixi run package
```

`pixi run install` builds and deploys directly to the game install. `pixi run release` runs the full version-bump / changelog / tag / push workflow.

## Community & Support

- Discord: [Loop's Head Tracking Hangout](https://discord.com/invite/dxyZdyFNT9) - setup help, bug reports, and new-release announcements
- [Lopari](https://lopari.app) - free Windows launcher with one-click install and launch for the released head-tracking mods
- [Headcam](https://headcam.app) - free app that turns your iPhone or Android phone into the head tracker

## License

MIT. See [LICENSE](LICENSE). Third-party components are listed in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

## Credits

- [EQ Studios](https://store.steampowered.com/app/252270/The_Painscreek_Killings/) - The Painscreek Killings
- [OpenTrack](https://github.com/opentrack/opentrack) - head tracking software and UDP wire protocol
- [Mono.Cecil](https://github.com/jbevain/cecil) - .NET assembly manipulation library used by the bootstrap patcher

## Disclaimer

This mod is unofficial and is not affiliated with, endorsed by, or supported by EQ Studios. Use at your own risk.
