# The Painscreek Killings Head Tracking

Decoupled head tracking for The Painscreek Killings: your head moves the camera while the mouse keeps independent control of aim and interaction text, no VR headset required.

<!-- ![Mod GIF](https://raw.githubusercontent.com/itsloopyo/the-painscreek-killings-headtracking/main/assets/readme-clip.gif) -->

## Features

- **Decoupled look and aim** - head tracking moves the camera; aim stays on your mouse
- **6DOF positional tracking** - lean and peek with head position

## Requirements

- [The Painscreek Killings](https://store.steampowered.com/app/252270/The_Painscreek_Killings/) on Steam
- An OpenTrack-compatible tracker: [OpenTrack](https://github.com/opentrack/opentrack) with a webcam, a phone app (e.g. SmoothTrack), or dedicated hardware
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

1. Install [OpenTrack](https://github.com/opentrack/opentrack).
2. Set the output to **UDP over network**.
3. Set the remote IP to `127.0.0.1` and the port to `4242`.
4. Start tracking before launching the game.

### VR Headset Setup

A VR headset makes an excellent tracker since it reports head pose at high rate and low latency.

1. Connect your headset to the PC over Air Link (Quest) or Virtual Desktop, and start SteamVR.
2. In OpenTrack, pick **Tracker: SteamVR** as the input.
3. Set output to UDP over network as above and start tracking.

### Webcam Setup

OpenTrack ships a `neuralnet tracker` input that runs head-pose estimation against any webcam.

1. Pick **Tracker: neuralnet tracker** as the input.
2. Click the input gear, select your webcam, and confirm the preview window shows your face.
3. Set output to UDP over network as above and start tracking.

### Phone App Setup

If your tracking app already smooths and centers, you can send directly from the phone to the mod on port 4242, no OpenTrack on PC required.

1. Install an OpenTrack-compatible tracking app (e.g. SmoothTrack, FaceTrackNoIR companion, OpenSeeFace).
2. Point it at your PC's IP address (run `ipconfig` to find it) on port `4242`.
3. Set the protocol to OpenTrack/UDP.

**With OpenTrack as a relay (optional):** if you want curve mapping or visual preview, route through OpenTrack. Set OpenTrack's input to `UDP over network` on a different port (e.g. `5252`), set its output to `127.0.0.1:4242`, and point your phone app at port `5252`. Make sure your firewall allows incoming UDP on the input port.

## Controls

Two equivalent binding sets - use whichever your keyboard has:

| Action              | Nav-cluster | Chord           |
|---------------------|-------------|-----------------|
| Recenter            | `Home`      | `Ctrl+Shift+T`  |
| Toggle tracking     | `End`       | `Ctrl+Shift+Y`  |
| Cycle tracking mode | `Page Up`   | `Ctrl+Shift+G`  |
| Toggle yaw mode     | `Page Down` | `Ctrl+Shift+H`  |

`Page Up` / `Ctrl+Shift+G` cycles tracking mode:

1. Normal head-tracked gameplay
2. Positional tracking disabled, rotational tracking enabled
3. Rotational tracking disabled, positional tracking enabled
4. Back to normal

`Page Down` / `Ctrl+Shift+H` toggles yaw mode between **world-space** (default, horizon-locked: yaw always rotates around the world up axis, so the horizon stays level when looking up or down) and **camera-local** (yaw rotates around the camera's current up axis, which produces a leaning/rolling effect at extreme pitches).

## Configuration

The mod creates `HeadTracking.cfg` in `Painscreek_Data\Managed\` on first run. Edit it with any text editor; section headers are decorative and can be reorganized freely.

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
Smoothing = 0.0             ; 0.0 to 1.0; higher = smoother but more latent (a 0.15 floor is always applied internally)

[AimDecoupling]
AimDecoupling = true        ; Decouple aim from head look direction
ShowReticle = true          ; Draw an aim reticle that tracks the clean aim point
ReticleColor = 1,1,1,1      ; Reticle color as R,G,B,A (default white opaque)

[Keybindings]
RecenterKey = Home          ; Unity KeyCode name
ToggleKey = End             ; Unity KeyCode name
YawModeKey = PageDown       ; Unity KeyCode name

[General]
WorldSpaceYaw = true        ; true = horizon-locked yaw; false = camera-local yaw
```

## Troubleshooting

**Mod not loading:**
- Check `HeadTracking.log` in the `Painscreek_Data\Managed\` folder for runtime status.
- If the log doesn't exist, the patcher likely never ran. Check `HeadTracking_BOOT.log` in the same folder and `%TEMP%\HeadTracking_BOOT_ERROR.log` for patcher errors.
- Make sure all three DLLs are present in the Managed folder.

**No tracking response:**
- Verify your tracker (OpenTrack or phone app) is running and shows movement in its own preview.
- Confirm UDP output is set to `127.0.0.1:4242` (or to your PC's LAN IP for direct phone-to-PC).
- Press `End` to make sure tracking is enabled, then `Home` to recenter.
- Check Windows Firewall is not blocking UDP on port 4242.

**Jittery / unstable tracking:**
- Raise `Smoothing` in `HeadTracking.cfg` (try 0.3 to 0.5 first).
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
powershell -ExecutionPolicy Bypass -File scripts/create-unity-stubs.ps1
pixi run build
pixi run package
```

`pixi run install` builds and deploys directly to the game install. `pixi run release` runs the full version-bump / changelog / tag / push workflow.

## License

MIT. See [LICENSE](LICENSE). Third-party components are listed in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

## Credits

- [EQ Studios](https://store.steampowered.com/app/252270/The_Painscreek_Killings/) - The Painscreek Killings
- [OpenTrack](https://github.com/opentrack/opentrack) - head tracking software and UDP wire protocol
- [Mono.Cecil](https://github.com/jbevain/cecil) - .NET assembly manipulation library used by the bootstrap patcher

## Disclaimer

This mod is unofficial and is not affiliated with, endorsed by, or supported by EQ Studios. Use at your own risk.
