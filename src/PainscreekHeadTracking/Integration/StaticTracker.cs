using System;
using System.Collections.Generic;
using System.IO;
using CameraUnlock.Core.Config;
using CameraUnlock.Core.Data;
using CameraUnlock.Core.Input;
using CameraUnlock.Core.Math;
using CameraUnlock.Core.Processing;
using CameraUnlock.Core.Protocol;
using CameraUnlock.Core.Tracking;
using CameraUnlock.Core.Unity.Extensions;
using UnityEngine;

namespace PainscreekHeadTracking
{
    /// <summary>
    /// Completely static head tracking implementation.
    /// No MonoBehaviour, no GameObject - cannot be destroyed by Unity.
    /// Call ApplyTracking() from a patched LateUpdate method.
    /// </summary>
    public static class StaticTracker
    {
        private const float HotkeyCooldownSeconds = 0.3f;
        private const float ErrorLogThrottleSeconds = 5f;

        // Raycast-based reticle distance
        private const float MaxRaycastDistance = 1000f;
        private const float MinRaycastDistance = 0.5f;
        private const float DistanceSmoothingRate = 15f;
        // Seed distance for the reticle until the first raycast lands, so the
        // crosshair projects to a plausible mid-room depth on the very first frame.
        private const float InitialHitDistanceMeters = 2.5f;

        private const int TrackingModeCount = 3;

        private static PainscreekConfig? _config;
        private static ConfigOwner<PainscreekConfig>? _configOwner;

        // Core components from CameraUnlock.Core
        private static OpenTrackReceiver? _receiver;
        private static TrackingProcessor? _processor;
        private static PositionProcessor? _positionProcessor;
        private static PositionInterpolator? _positionInterpolator;
        private static TrackingMode _trackingMode = TrackingMode.RotationAndPosition;
        // Cached connection locality. Selects LocalSmoothing vs RemoteSmoothing and is
        // re-checked every frame so switching trackers takes effect without a restart.
        private static bool _cachedIsRemoteConnection;

        private static KeyBinding[] _toggleKeys = new KeyBinding[0];
        private static KeyBinding[] _cycleModeKeys = new KeyBinding[0];
        private static KeyBinding[] _yawModeKeys = new KeyBinding[0];
        private static float _lastHotkeyTime;

        // Yaw mode: true = horizon-locked (yaw around world up), false = camera-local
        // (yaw around current camera up, leans/rolls at extreme pitches).
        private static bool _worldSpaceYaw = true;

        // Camera reference
        private static Camera? _mainCamera;
        private static Transform? _cameraTransform;

        // Aim decoupling - reticle position
        private static Vector2 _reticleScreenOffset;

        // State
        private static bool _initialized;
        private static bool _enabled;
        private static float _lastLogTime = float.NegativeInfinity;
        private static bool _directPitchUnavailableWarningLogged;

        // Diagnostics
        private static string _lastDiagLine = string.Empty;

        // Screen dimension cache (updated in ApplyToCamera, reused by GetAimScreenPosition)
        private static int _cachedScreenWidth;
        private static int _cachedScreenHeight;

        // Save/restore: game's camera state before mod applies tracking
        private static Vector3 _savedLocalPosition;
        private static Quaternion _savedLocalRotation;
        private static bool _trackingAppliedThisFrame;
        // Static Camera.onPreCull can fire multiple times per frame for the same
        // camera (shadows, reflections). Without this dedup the second call would
        // re-save the already-tracked transform as if it were clean, and OnPostRender
        // would restore to the tracked state.
        private static int _lastAppliedFrame = -1;

        private static float _lastHitDistance = InitialHitDistanceMeters;

        /// <summary>
        /// Returns the screen position where the aim is pointing.
        /// Used by patched game code for raycasts.
        /// When tracking is disabled/disconnected, returns screen center.
        /// </summary>
        public static Vector3 GetAimScreenPosition()
        {
            // Use cached screen dimensions (updated each frame in ApplyToCamera)
            // to avoid repeated native property calls from multiple raycasters
            int sw = _cachedScreenWidth;
            int sh = _cachedScreenHeight;
            if (sw == 0) { sw = Screen.width; sh = Screen.height; }
            float centerX = sw * 0.5f;
            float centerY = sh * 0.5f;

            if (!_enabled || !(_receiver?.IsReceiving ?? false))
            {
                return new Vector3(centerX, centerY, 0f);
            }
            return new Vector3(
                centerX + _reticleScreenOffset.x,
                centerY + _reticleScreenOffset.y,
                0f
            );
        }

        /// <summary>
        /// Called from patched game code every frame.
        /// Initializes if needed and applies tracking.
        /// </summary>
        public static void ApplyTracking()
        {
            try
            {
                if (!_initialized)
                {
                    Initialize();
                }

                float now = Time.realtimeSinceStartup;
                LogDiagnostics(now);
                HandleHotkeys(now);

                // Check game state (rate-limited internally). Reuse the frame's already
                // sampled timestamp instead of letting the detector read the clock again.
                GameStateDetector.Update(now);

                // Skip if disabled, not connected, or not in gameplay
                if (!_enabled || !(_receiver?.IsReceiving ?? false) || !GameStateDetector.IsGameplay)
                {
                    return;
                }

                if (_cameraTransform == null)
                {
                    EnsureCamera();
                    if (_cameraTransform == null) return;
                }

                int frame = Time.frameCount;
                if (frame == _lastAppliedFrame) return;
                _lastAppliedFrame = frame;

                ApplyToCamera();
            }
            catch (Exception ex)
            {
                if (Time.realtimeSinceStartup - _lastLogTime > ErrorLogThrottleSeconds)
                {
                    Log($"ApplyTracking error: {ex}");
                    _lastLogTime = Time.realtimeSinceStartup;
                }
            }
        }

        // Written on change only. HeadTracking.log is the mod's own file and the whole
        // of what a user sends in; a periodic heartbeat buries the startup chain under
        // hours of one unchanging fact.
        private static void LogDiagnostics(float now)
        {
            bool isReceiving = _receiver?.IsReceiving ?? false;
            string line = $"[DIAG] isReceiving={isReceiving}, camera={(_cameraTransform != null)}, isGameplay={GameStateDetector.IsGameplay}";
            if (line == _lastDiagLine) return;

            _lastDiagLine = line;
            Log($"{line}, time={now:F2}s");
        }

        private static void HandleHotkeys(float now)
        {
            if (now - _lastHotkeyTime <= HotkeyCooldownSeconds) return;
            // Short-circuits the key lookups on the frames where no key went down.
            if (!Input.anyKeyDown) return;

            if (KeyBindingInput.IsTriggered(_toggleKeys))
            {
                _lastHotkeyTime = now;
                _enabled = !_enabled;
                Log(_enabled ? "Tracking enabled" : "Tracking disabled");
                if (!_enabled)
                {
                    ResetCamera();
                }
            }
            else if (KeyBindingInput.IsTriggered(_cycleModeKeys))
            {
                _lastHotkeyTime = now;
                CycleTrackingMode();
            }
            else if (KeyBindingInput.IsTriggered(_yawModeKeys))
            {
                _lastHotkeyTime = now;
                ToggleYawMode();
            }
        }

        private static void CycleTrackingMode()
        {
            _trackingMode = (TrackingMode)(((int)_trackingMode + 1) % TrackingModeCount);

            // Reset the side that just turned off so it doesn't carry stale smoothed
            // values into a future re-enable.
            if (_trackingMode == TrackingMode.PositionOnly)
            {
                _processor?.Reset();
            }
            else if (_trackingMode == TrackingMode.RotationOnly)
            {
                _positionProcessor?.ResetSmoothing();
                _positionInterpolator?.Reset();
            }

            Log($"Tracking mode: {_trackingMode.Description()}");

            bool rotation;
            bool position;
            TrackingModeChannels.Encode(_trackingMode, out rotation, out position);
            SaveConfig(c =>
            {
                c.RotationEnabled = rotation;
                c.PositionEnabled = position;
            });
        }

        private static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            Logger.Initialize();
            Log("StaticTracker initializing...");

            // Set up logger for reflection helper to avoid per-frame delegate allocation
            GameReflectionHelper.SetLogger(Log);
            GameCursorManager.SetLogger(Log);

            _config = LoadConfig();

            // Every published build shipped identity rotation sensitivity and no axis flips; the
            // settings are gone.
            _processor = new TrackingProcessor
            {
                Sensitivity = SensitivitySettings.Default,
                LocalSmoothing = _config.LocalSmoothing,
                RemoteSmoothing = _config.RemoteSmoothing
            };

            // Create position processor with Painscreek-tuned defaults. Position uses the
            // same smoothing pair as rotation.
            var posSettings = PainscreekPositionDefaults.Build(_config.LocalSmoothing, _config.RemoteSmoothing);
            _positionProcessor = new PositionProcessor
            {
                Settings = posSettings
            };
            _positionInterpolator = new PositionInterpolator();
            Log($"Position settings: SensX={posSettings.SensitivityX}, SensY={posSettings.SensitivityY}, SensZ={posSettings.SensitivityZ}");

            _toggleKeys = ParseKeys("ToggleKey", _config.ToggleKeyName);
            _cycleModeKeys = ParseKeys("CycleTrackingModeKey", _config.CycleTrackingModeKeyName);
            _yawModeKeys = ParseKeys("YawModeKey", _config.YawModeKeyName);
            _worldSpaceYaw = _config.WorldSpaceYaw;
            _enabled = _config.EnableOnStartup;
            // The pair always names a mode: the table reads a pair that names none as its default.
            _trackingMode = TrackingModeChannels.Decode(_config.RotationEnabled, _config.PositionEnabled)!.Value;
            Log($"Hotkeys: Toggle={_config.ToggleKeyName}; CycleTrackingMode={_config.CycleTrackingModeKeyName}; YawMode={_config.YawModeKeyName}");
            Log($"Tracking {(_enabled ? "on" : "off")} at start, mode: {_trackingMode.Description()}");
            Log($"Yaw mode: {(_worldSpaceYaw ? "world-space (horizon-locked)" : "camera-local")}");

            // Start core OpenTrack receiver
            _receiver = new OpenTrackReceiver();
            _receiver.Log = Log;
            if (_receiver.Start(_config.UdpPort))
            {
                Log($"Listening on UDP port {_config.UdpPort}");
            }
        }

        /// <summary>
        /// The settings live in CameraUnlock.ini beside this DLL, in Painscreek_Data\Managed, read
        /// and written by core's config owner, with rows set to default following the player's
        /// Defaults.ini. While CameraUnlock.ini is absent the owner imports HeadTracking.cfg, the
        /// file every earlier build read, through the frozen reader in Legacy/, and never writes it.
        /// Runs on the main thread, where the hotkeys that save also run.
        /// </summary>
        private static PainscreekConfig LoadConfig()
        {
            string location = typeof(StaticTracker).Assembly.Location;
            string dir = Path.GetDirectoryName(location)
                ?? throw new InvalidOperationException("PainscreekHeadTracking.dll has no folder: " + location);
            // The mod draws no messages of its own, so the player's line goes to the log.
            _configOwner = new ConfigOwner<PainscreekConfig>(
                PainscreekConfig.OwnerOptions(dir, DefaultsFile.PerUser(), message => Log("[Config] " + message)));

            ConfigLoadResult<PainscreekConfig> loaded = _configOwner.Load();
            foreach (string line in loaded.Log) Log("[Config] " + line);
            Log("[Config] " + Path.Combine(dir, PainscreekConfig.FileName) + ": " + loaded.Status);
            return loaded.Config;
        }

        // The table's hotkey codec has read every list the file holds, so a list that does not
        // parse reaches here only from a legacy import the owner deferred: a key the key table
        // names no key for, which the import writes as it was. The dev build still fired the chord
        // beside such a key, so the items that parse are bound and the rest are logged.
        private static KeyBinding[] ParseKeys(string key, string text)
        {
            KeyBinding[] bindings;
            string error;
            if (KeyBindings.TryParse(text, out bindings, out error)) return bindings;

            var kept = new List<KeyBinding>();
            foreach (string item in text.Split(','))
            {
                if (KeyBindings.TryParse(item, out bindings, out error)) kept.AddRange(bindings);
                else Log("[Config] [Hotkeys] " + key + ": " + error + ", so it is not bound this session");
            }
            return kept.ToArray();
        }

        /// <summary>
        /// Called after the new value is already applied. A save that fails is logged, the owner's
        /// reason reaches the log through the status sink, and the session keeps the new value.
        /// </summary>
        private static void SaveConfig(Action<PainscreekConfig> change)
        {
            ConfigSaveResult saved = _configOwner!.Save(change);
            foreach (string line in saved.Log) Log("[Config] " + line);
            if (saved.Status != ConfigSaveStatus.Saved)
            {
                Log("[Config] " + saved.Status + ": the change applies to this session only.");
            }
        }

        private static void EnsureCamera()
        {
            _mainCamera = CameraResolver.Resolve(Log);
            _cameraTransform = _mainCamera != null ? _mainCamera.transform : null;
        }

        private static void ApplyToCamera()
        {
            // Camera not found yet is expected during loading - skip this frame
            if (_cameraTransform == null) return;

            // Receiver/processor being null indicates initialization failed - log and skip
            if (_receiver == null || _processor == null)
            {
                Log("ERROR: ApplyToCamera called but receiver or processor is null - initialization failed");
                return;
            }

            // Cache deltaTime once per frame to avoid 2x native interop in this method.
            float deltaTime = Time.deltaTime;

            // Idempotent; runs every frame but no-ops once Scion's DoF temporal SS
            // has been disabled. Re-arms on toggle-on after ResetCamera restored it.
            PostProcessSuppressor.Apply(_mainCamera!, Log);

            // Save the game's camera state before we modify anything.
            // RestoreCamera() will put these back after rendering so the game
            // never sees our modifications (prevents crouch/animation feedback loops).
            _savedLocalPosition = _cameraTransform.localPosition;
            _savedLocalRotation = _cameraTransform.localRotation;
            _trackingAppliedThisFrame = true;

            UpdateConnectionLocality();

            bool rotationActive = _trackingMode != TrackingMode.PositionOnly;

            // The tracker owns the centre; the pose is applied as sent.
            TrackingPose rawPose = _receiver.GetLatestPose();

            // Process through TrackingProcessor (smoothing, sensitivity, limits).
            // The processor picks LocalSmoothing or RemoteSmoothing from the
            // connection flag set above.
            TrackingPose processed = _processor.Process(rawPose, deltaTime);

            // Get smoothed values (note: pitch needs to be inverted for natural head movement)
            // In PositionOnly mode, zero rotation so positional offset isn't twisted by head yaw.
            float yaw = rotationActive ? processed.Yaw : 0f;
            float pitch = rotationActive ? -processed.Pitch : 0f;
            float roll = rotationActive ? processed.Roll : 0f;

            float gamePitch = ResolveGamePitch(ref roll);
            float yawRad = yaw * Mathf.Deg2Rad;

            if (rotationActive)
            {
                ApplyRotation(yaw, pitch, roll, gamePitch);
            }

            // Capture the game's intended camera world position BEFORE position offset.
            // localRotation doesn't affect world position for a child transform, so this
            // is safe after rotation but must be before localPosition modification.
            Vector3 gameCamWorldPos = _cameraTransform.position;

            // Native Transform.parent call - cache once and share with reticle/position helpers
            // to avoid a second interop hop per frame.
            Transform parent = _cameraTransform.parent;

            bool positionActive = _trackingMode != TrackingMode.RotationOnly;
            if (positionActive)
            {
                ApplyPositionOffset(gameCamWorldPos, parent, yaw, pitch, roll, yawRad, deltaTime);
            }

            UpdateReticleOffset(gameCamWorldPos, parent, gamePitch, deltaTime);

            GameCursorManager.UpdatePosition(_reticleScreenOffset);
        }

        // Pushes the receiver's connection locality into both processors when it changes.
        // The processors select LocalSmoothing or RemoteSmoothing from this flag, so a
        // user swapping a local OpenTrack instance for a phone on WiFi gets the other
        // parameter mid-session.
        private static void UpdateConnectionLocality()
        {
            bool isRemoteConnection = _receiver!.IsRemoteConnection;
            if (isRemoteConnection == _cachedIsRemoteConnection) return;

            _cachedIsRemoteConnection = isRemoteConnection;
            _processor!.IsRemoteConnection = isRemoteConnection;
            if (_positionProcessor != null)
            {
                _positionProcessor.IsRemoteConnection = isRemoteConnection;
            }
            Log($"Connection locality changed: remote={isRemoteConnection}");
        }

        private static void ApplyRotation(float yaw, float pitch, float roll, float gamePitch)
        {
            if (_worldSpaceYaw)
            {
                // Horizon-locked: yaw around world up, pitch+roll camera-local.
                Quaternion yawQ = Quaternion.AngleAxis(yaw, Vector3.up);
                Quaternion pitchQ = Quaternion.AngleAxis(gamePitch + pitch, Vector3.right);
                Quaternion combined = yawQ * pitchQ;

                Vector3 newFwd = combined * Vector3.forward;
                Vector3 newUp = combined * Vector3.up;

                if (Mathf.Abs(roll) > 0.001f)
                {
                    float rollRad = roll * Mathf.Deg2Rad;
                    float cr = Mathf.Cos(rollRad);
                    float sr = Mathf.Sin(rollRad);
                    newUp = newUp * cr + Vector3.Cross(newFwd, newUp) * sr;
                }

                _cameraTransform!.localRotation = Quaternion.LookRotation(newFwd, newUp);
            }
            else
            {
                // Camera-local: head rotation applied in the gamePitch-rotated frame,
                // so yaw rotates around the camera's current up (rolls at extreme pitch).
                Quaternion gameClean = Quaternion.AngleAxis(gamePitch, Vector3.right);
                Quaternion qy = Quaternion.AngleAxis(yaw, Vector3.up);
                Quaternion qx = Quaternion.AngleAxis(pitch, Vector3.right);
                Quaternion qz = Quaternion.AngleAxis(-roll, Vector3.forward);
                Quaternion headLocal = qy * qx * qz;
                _cameraTransform!.localRotation = gameClean * headLocal;
            }
        }

        private static void ApplyPositionOffset(Vector3 gameCamWorldPos, Transform parent, float yaw, float pitch, float roll, float yawRad, float deltaTime)
        {
            if (_receiver == null || _positionProcessor == null || _positionInterpolator == null) return;

            var rawPos = _receiver.GetLatestPosition();
            var interpolatedPos = _positionInterpolator.Update(rawPos, deltaTime);
            var headRotQ = QuaternionUtils.FromYawPitchRoll(yaw, pitch, roll);
            Vec3 posOffset = _positionProcessor.Process(interpolatedPos, headRotQ, deltaTime);

            // Rotate positional offset by yaw only so forward/back lean stays in
            // the horizontal plane. Pitch rotation was mixing Z into Y, which
            // pushed the camera through floors/ceilings when crouched.
            float cosYaw = Mathf.Cos(yawRad);
            float sinYaw = Mathf.Sin(yawRad);

            // Negative z is the forward lean throughout the pipeline and the asymmetric
            // clamp is built on that; Unity's +z is forward, so the flip belongs here rather
            // than in InvertZ, which inverts ahead of the clamp and swaps the two budgets.
            float leanForward = -posOffset.Z;

            Vector3 rotatedOffset = new Vector3(
                posOffset.X * cosYaw + leanForward * sinYaw,
                0f,
                -posOffset.X * sinYaw + leanForward * cosYaw
            );

            // Apply in world space so parent transform scale (which changes
            // when crouched) can't amplify the offset.
            Vector3 worldOffset = parent != null ? parent.rotation * rotatedOffset : rotatedOffset;
            _cameraTransform!.position = gameCamWorldPos + worldOffset;
        }

        // Projects the game's clean aim direction through the head-tracked view to
        // produce a screen-space reticle offset. Uses WorldToScreenPoint so horizon-locked
        // yaw, roll, and positional parallax are all handled by Unity's projection matrix.
        private static void UpdateReticleOffset(Vector3 gameCamWorldPos, Transform parent, float gamePitch, float deltaTime)
        {
            if (_mainCamera == null || _cameraTransform == null) return;

            // Cache screen dimensions so multiple raycasters reading GetAimScreenPosition
            // don't each pay for a native Screen.width/height call.
            int screenWidth = Screen.width;
            int screenHeight = Screen.height;
            _cachedScreenWidth = screenWidth;
            _cachedScreenHeight = screenHeight;

            // Game's world-space aim: parent's yaw + game pitch, before our modifications.
            // Pitch-only rotation around X of forward(0,0,1) is (0, -sin, cos) - skip the
            // Quaternion.Euler construction + quat*vec multiply on a per-frame hot path.
            float gamePitchRad = gamePitch * Mathf.Deg2Rad;
            Vector3 localFwd = new Vector3(0f, -Mathf.Sin(gamePitchRad), Mathf.Cos(gamePitchRad));
            Vector3 gameWorldFwd = parent != null ? parent.TransformDirection(localFwd) : localFwd;

            RaycastHit hit;
            if (Physics.Raycast(gameCamWorldPos, gameWorldFwd, out hit, MaxRaycastDistance)
                && hit.distance >= MinRaycastDistance)
            {
                float t = 1f - Mathf.Exp(-DistanceSmoothingRate * deltaTime);
                _lastHitDistance = Mathf.Lerp(_lastHitDistance, hit.distance, t);
            }

            Vector3 aimTarget = gameCamWorldPos + _lastHitDistance * gameWorldFwd;
            Vector3 screenPt = _mainCamera.WorldToScreenPoint(aimTarget);

            // screenPt.z > 0 means the aim target is in front of the camera. If behind
            // (extreme head rotation), WorldToScreenPoint returns inverted coordinates -
            // fall back to zero offset rather than showing garbage.
            if (screenPt.z > 0f)
            {
                _reticleScreenOffset = new Vector2(
                    screenPt.x - screenWidth * 0.5f,
                    screenPt.y - screenHeight * 0.5f);
            }
            else
            {
                _reticleScreenOffset = Vector2.zero;
            }
        }

        // Resolves the game's clean (un-rotated) pitch value, preferring direct reflection
        // into FirstPersonPlayerController.verticalRotation. When reflection is unavailable
        // we fall back to decomposing localEulerAngles, but Euler decomposition is
        // contaminated by our own roll modification - so in degraded mode we zero roll
        // to keep the extraction mathematically valid.
        private static float ResolveGamePitch(ref float roll)
        {
            float? directPitch = GameReflectionHelper.GetGamePitchDirect();
            if (directPitch.HasValue)
            {
                return directPitch.Value;
            }

            if (!_directPitchUnavailableWarningLogged)
            {
                Log("WARNING: Direct pitch access unavailable (reflection failed) - roll tracking disabled");
                _directPitchUnavailableWarningLogged = true;
            }

            Vector3 currentEuler = _cameraTransform!.localEulerAngles;
            float gamePitch = currentEuler.x;
            if (gamePitch > 180f) gamePitch -= 360f;
            roll = 0f;
            return gamePitch;
        }

        private static void ToggleYawMode()
        {
            bool worldSpace = !_worldSpaceYaw;
            _worldSpaceYaw = worldSpace;
            Log(worldSpace
                ? "Yaw mode: world-space (horizon-locked)"
                : "Yaw mode: camera-local (rolls/leans at extreme pitches)");
            SaveConfig(c => c.WorldSpaceYaw = worldSpace);
        }

        /// <summary>
        /// Restores the camera's localPosition and localRotation to the values
        /// saved at the start of ApplyToCamera(). Called from OnPostRender so
        /// the game never sees the mod's transform modifications.
        /// </summary>
        public static void RestoreCamera()
        {
            if (!_trackingAppliedThisFrame) return;
            _trackingAppliedThisFrame = false;
            if (_cameraTransform == null) return;
            _cameraTransform.localPosition = _savedLocalPosition;
            _cameraTransform.localRotation = _savedLocalRotation;
        }

        private static void ResetCamera()
        {
            _processor?.Reset();
            _positionProcessor?.Reset();
            _positionInterpolator?.Reset();
            _reticleScreenOffset = Vector2.zero;
            _trackingAppliedThisFrame = false;
            GameCursorManager.ResetToOriginalPositions();
            PostProcessSuppressor.Restore(Log);
        }

        private static void Log(string message)
        {
            Logger.Log(message);
        }
    }
}
