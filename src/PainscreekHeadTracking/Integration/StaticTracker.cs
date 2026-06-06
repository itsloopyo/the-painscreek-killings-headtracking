using System;
using CameraUnlock.Core.Config;
using CameraUnlock.Core.Data;
using CameraUnlock.Core.Math;
using CameraUnlock.Core.Processing;
using CameraUnlock.Core.Protocol;
using CameraUnlock.Core.Tracking;
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
        private const float DiagIntervalSeconds = 5f;
        private const int InitialDiagBurstCount = 20;

        // Trackers can drift for ~half a second after first connecting (face trackers
        // warm up; phone trackers settle their IMU bias). Re-recenter after this delay
        // so the user's center pose isn't pinned to a transient initial reading.
        private const float StabilizationRecenterDelaySeconds = 0.5f;

        // Raycast-based reticle distance
        private const float MaxRaycastDistance = 1000f;
        private const float MinRaycastDistance = 0.5f;
        private const float DistanceSmoothingRate = 15f;
        // Seed distance for the reticle until the first raycast lands, so the
        // crosshair projects to a plausible mid-room depth on the very first frame.
        private const float InitialHitDistanceMeters = 2.5f;

        private const int TrackingModeCount = 3;

        // Configuration - loaded from CameraUnlock.Core
        private static HeadTrackingConfigData? _config;

        // Core components from CameraUnlock.Core
        private static OpenTrackReceiver? _receiver;
        private static TrackingProcessor? _processor;
        private static PositionProcessor? _positionProcessor;
        private static PositionInterpolator? _positionInterpolator;
        private static TrackingMode _trackingMode = TrackingMode.RotationAndPosition;
        private static bool _hasAutoRecentered;
        private static float _autoRecenterTime;
        private static bool _needsStabilizationRecenter;

        // Hotkeys (manual handling for Unity 5 compatibility)
        private static KeyCode _recenterKey = KeyCode.Home;
        private static KeyCode _toggleKey = KeyCode.End;
        private static KeyCode _cycleModeKey = KeyCode.PageUp;
        private static KeyCode _yawModeKey = KeyCode.PageDown;
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
        private static bool _enabled = true;
        private static float _lastLogTime = float.NegativeInfinity;
        private static bool _directPitchUnavailableWarningLogged;

        // Diagnostics
        private static float _lastDiagTime = float.NegativeInfinity;
        private static int _diagCount;

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
                LogDiagnosticsIfDue(now);
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

                ApplyToCamera(now);
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

        private static void LogDiagnosticsIfDue(float now)
        {
            _diagCount++;
            // Log every call up to InitialDiagBurstCount (helps catch early init issues),
            // then settle to every DiagIntervalSeconds afterwards.
            if (_diagCount > InitialDiagBurstCount && now - _lastDiagTime <= DiagIntervalSeconds) return;

            _lastDiagTime = now;
            bool isReceiving = _receiver?.IsReceiving ?? false;
            Log($"[DIAG #{_diagCount}] isReceiving={isReceiving}, camera={(_cameraTransform != null)}, isGameplay={GameStateDetector.IsGameplay}, time={now:F2}s");
        }

        private static void HandleHotkeys(float now)
        {
            if (now - _lastHotkeyTime <= HotkeyCooldownSeconds) return;

            // Resolve chord modifiers once, but only check chord-letter GetKeyDown if
            // the modifiers are held - keeps the common (no-modifier) frame path at
            // 2 GetKey + 4 GetKeyDown calls instead of 4+4+4.
            bool chord = (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
                      && (Input.GetKey(KeyCode.LeftShift)   || Input.GetKey(KeyCode.RightShift));

            if (Pressed(_recenterKey, chord, KeyCode.T))
            {
                _lastHotkeyTime = now;
                Recenter();
            }
            else if (Pressed(_toggleKey, chord, KeyCode.Y))
            {
                _lastHotkeyTime = now;
                _enabled = !_enabled;
                Log(_enabled ? "Tracking enabled" : "Tracking disabled");
                if (!_enabled)
                {
                    ResetCamera();
                }
            }
            else if (Pressed(_cycleModeKey, chord, KeyCode.G))
            {
                _lastHotkeyTime = now;
                CycleTrackingMode();
            }
            else if (Pressed(_yawModeKey, chord, KeyCode.H))
            {
                _lastHotkeyTime = now;
                ToggleYawMode();
            }
        }

        private static bool Pressed(KeyCode primary, bool chordHeld, KeyCode chordLetter) =>
            Input.GetKeyDown(primary) || (chordHeld && Input.GetKeyDown(chordLetter));

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

            // Load configuration using CameraUnlock.Core
            string configPath = HeadTrackingConfigData.GetDefaultConfigPath(typeof(StaticTracker).Assembly);
            _config = HeadTrackingConfigData.LoadFromFile(configPath, Log);
            Log($"Config loaded: Port={_config.UdpPort}, Sensitivity=({_config.Sensitivity.Yaw}, {_config.Sensitivity.Pitch}, {_config.Sensitivity.Roll})");

            // Create tracking processor with config settings
            _processor = new TrackingProcessor
            {
                Sensitivity = _config.Sensitivity,
                SmoothingFactor = _config.Smoothing
            };

            // Create position processor with Painscreek-tuned defaults
            var posSettings = PainscreekPositionDefaults.Build();
            _positionProcessor = new PositionProcessor
            {
                Settings = posSettings
            };
            _positionInterpolator = new PositionInterpolator();
            Log($"Position settings: SensX={posSettings.SensitivityX}, SensY={posSettings.SensitivityY}, SensZ={posSettings.SensitivityZ}");

            // Parse hotkeys from config
            _recenterKey = ParseKeyCode(_config.RecenterKeyName, KeyCode.Home);
            _toggleKey = ParseKeyCode(_config.ToggleKeyName, KeyCode.End);
            _yawModeKey = ParseKeyCode(_config.YawModeKeyName, KeyCode.PageDown);
            _worldSpaceYaw = _config.WorldSpaceYaw;
            Log($"Hotkeys: Toggle={_toggleKey}, Recenter={_recenterKey}, YawMode={_yawModeKey}");
            Log($"Yaw mode: {(_worldSpaceYaw ? "world-space (horizon-locked)" : "camera-local")}");

            // Start core OpenTrack receiver
            _receiver = new OpenTrackReceiver();
            _receiver.Log = Log;
            if (_receiver.Start(_config.UdpPort))
            {
                Log($"Listening on UDP port {_config.UdpPort}");
            }
        }

        private static KeyCode ParseKeyCode(string keyName, KeyCode defaultKey)
        {
            try
            {
                KeyCode parsed = (KeyCode)Enum.Parse(typeof(KeyCode), keyName, true);
                // Enum.Parse silently accepts numeric strings ("999") and returns
                // an undefined enum value, which Input.GetKeyDown then treats as
                // a dead key. Reject anything not a real KeyCode member.
                if (!Enum.IsDefined(typeof(KeyCode), parsed))
                {
                    Log($"WARNING: Key name '{keyName}' is not a defined KeyCode, using default: {defaultKey}");
                    return defaultKey;
                }
                return parsed;
            }
            catch (ArgumentException)
            {
                // Invalid key name in config - use default and warn user
                Log($"WARNING: Invalid key name '{keyName}' in config, using default: {defaultKey}");
                return defaultKey;
            }
        }

        private static void EnsureCamera()
        {
            _mainCamera = CameraResolver.Resolve(Log);
            _cameraTransform = _mainCamera != null ? _mainCamera.transform : null;
        }

        private static void ApplyToCamera(float realtime)
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

            // Auto-recenter on first connection
            if (!_hasAutoRecentered && _receiver.IsReceiving)
            {
                _hasAutoRecentered = true;
                _needsStabilizationRecenter = true;
                _autoRecenterTime = realtime;
                Recenter();
                Log($"Connection detected (remote={_receiver.IsRemoteConnection}) - auto-recentered");
            }

            // Re-recenter after a short delay to compensate for face-tracker warm-up drift
            if (_needsStabilizationRecenter && realtime - _autoRecenterTime >= StabilizationRecenterDelaySeconds)
            {
                _needsStabilizationRecenter = false;
                Recenter();
                Log($"Stabilization re-recenter ({StabilizationRecenterDelaySeconds:0.0}s post-connection)");
            }

            bool rotationActive = _trackingMode != TrackingMode.PositionOnly;

            // Get pose from core receiver (includes recenter offset)
            TrackingPose rawPose = _receiver.GetLatestPose();

            // Process through TrackingProcessor (smoothing, sensitivity, limits)
            // Always apply smoothing baseline - local connections benefit from the same
            // minimum smoothing floor that remote connections get (prevents raw jitter).
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

            Vector3 rotatedOffset = new Vector3(
                posOffset.X * cosYaw + posOffset.Z * sinYaw,
                0f,
                -posOffset.X * sinYaw + posOffset.Z * cosYaw
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

        private static void Recenter()
        {
            _receiver?.Recenter();
            _processor?.Reset();
            if (_receiver != null)
            {
                _positionProcessor?.SetCenter(_receiver.GetLatestPosition());
            }
            _positionInterpolator?.Reset();
            Log("Recentered");
        }

        private static void ToggleYawMode()
        {
            _worldSpaceYaw = !_worldSpaceYaw;
            Log(_worldSpaceYaw
                ? "Yaw mode: world-space (horizon-locked)"
                : "Yaw mode: camera-local (rolls/leans at extreme pitches)");
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
