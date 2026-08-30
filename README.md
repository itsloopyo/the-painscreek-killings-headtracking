# The Painscreek Killings Head Tracking

![The Painscreek Killings running with this mod](https://raw.githubusercontent.com/itsloopyo/the-painscreek-killings-headtracking/main/assets/readme-clip.gif)

An unofficial head tracking mod for The Painscreek Killings that moves the view with your head while your mouse keeps control of the cursor, driven by OpenTrack over UDP, with no VR headset required.

## Features

- **Decoupled look and aim** - head tracking moves the camera; aim stays on your mouse
- **6DOF positional tracking** - lean and peek with head position

## Requirements

- [The Painscreek Killings](https://store.steampowered.com/app/252270/The_Painscreek_Killings/) on Steam
- A tracker that sends OpenTrack UDP pose data to port 4242: [OpenTrack](https://github.com/opentrack/opentrack) with a webcam, or a phone app with an OpenTrack UDP output
- Windows 10 or 11 (64-bit)

## Installation

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

1. Download the `-installer.zip` from the [Releases page](https://github.com/itsloopyo/the-painscreek-killings-headtracking/releases) and extract it anywhere. Copy `PainscreekHeadTracking.dll`, `CameraUnlock.Core.dll`, and `Mono.Cecil.dll` from the extracted `mod\` folder into your game's `Painscreek_Data\Managed\`.
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

## Configuration

The mod reads `HeadTracking.cfg` from `Painscreek_Data\Managed\`. Edit it with any text editor; section headers are decorative and can be reorganized freely.

```ini
[Network]
UdpPort = 4242              ; UDP port for OpenTrack data
EnableOnStartup = true      ; Start with head tracking enabled

[Sensitivity]
YawSensitivity = 1.0        ; Horizontal rotation multiplier (0.1 to 3.0)
PitchSensitivity = 1.0      ; Vertical rotation multiplier (0.1 to 3.0)
RollSensitivity = 1.0       ; Head tilt multiplier (0.1 to 3.0)
InvertYaw = false
InvertPitch = false
InvertRoll = false

[Smoothing]
LocalSmoothing = 0.0        ; 0.0 to 1.0; used when the tracker runs on this machine
RemoteSmoothing = 0.15      ; 0.0 to 1.0; used when the tracker is a network device

[AimDecoupling]
AimDecoupling = true        ; Decouple aim from head look direction
ShowReticle = true          ; Draw an aim reticle that tracks the clean aim point
ReticleColor = 1,1,1,1      ; Reticle color as R,G,B,A (default white opaque)

[Keybindings]
ToggleKey = End             ; Unity KeyCode name
YawModeKey = PageDown       ; Unity KeyCode name

[General]
WorldSpaceYaw = true        ; true = horizon-locked yaw; false = camera-local yaw
```

Smoothing covers both rotation and position. Which of the two values applies is
decided per connection from the packet source address: a tracker running on this
PC uses `LocalSmoothing`, a phone or other network device uses `RemoteSmoothing`.
Switching between them takes effect without restarting the game.

## Troubleshooting

**Mod not loading:**
- Check `HeadTracking.log` in the `Painscreek_Data\Managed\` folder for runtime status.
- If the log doesn't exist, the patcher likely never ran. Check `HeadTracking_BOOT.log` in the same folder and `%TEMP%\HeadTracking_BOOT_ERROR.log` for patcher errors.
- Make sure all three DLLs are present in the Managed folder.

**No tracking response:**
- Verify your tracker (OpenTrack or phone app) is running and shows movement in its own preview.
- Confirm UDP output is set to `127.0.0.1:4242` (or to your PC's LAN IP for direct phone-to-PC).
- Press `End` to make sure tracking is enabled.
- If the view is off-centre, centre it in your tracker app rather than in the game.
- Check Windows Firewall is not blocking UDP on port 4242.

**Jittery / unstable tracking:**
- Raise `RemoteSmoothing` (phone/network tracker) or `LocalSmoothing` (tracker on this PC) in `HeadTracking.cfg` (try 0.3 to 0.5 first).
- For phone trackers on Wi-Fi, lower the phone's send rate, or use a wired connection / hotspot.
- Lower the per-axis sensitivities if the source signal is noisy.

**Wrong rotation axis or feels off at extreme angles:**
- Toggle world-space vs camera-local yaw with `Page Down` / `Ctrl+Shift+H`. World-space (default) is horizon-stable; camera-local follows the camera's current up axis.
- Set `InvertPitch`, `InvertYaw`, or `InvertRoll` in the config to flip a reversed axis.

**Game crashes on startup:**
- Restore `Assembly-CSharp.dll` from the `.original` backup, or verify game files through Steam.
- Open an issue with the contents of `HeadTracking.log` and `HeadTracking_BOOT.log`.

## Updating

Download the new release and run `install.cmd` again. Your `HeadTracking.cfg` is preserved.

## Uninstalling

Run `uninstall.cmd` from the release folder. This removes the mod DLLs and restores the original `Assembly-CSharp.dll` from the `.original` backup. The bootstrap patch is reverted automatically. Use `uninstall.cmd /force` to remove everything even if the install state file says we did not install it.

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
