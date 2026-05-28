using System;
using System.Reflection;

namespace PainscreekHeadTracking
{
    /// <summary>
    /// Shared reflection utilities for accessing game internals.
    /// Caches FieldInfo for repeated access to game fields.
    /// </summary>
    public static class GameReflectionHelper
    {
        // Reflection cache for direct access to the game's pitch variable
        private static FieldInfo? _verticalRotationField;
        private static FieldInfo? _cameraInvertYField;
        private static bool _reflectionInitialized;
        private static bool _readErrorLogged;

        // Cached values to reduce reflection calls per frame
        private static float _cachedPitch;
        private static int _lastCacheFrame = -1;

        // Cached logger to avoid delegate allocation on hot path
        private static Action<string>? _cachedLogger;

        /// <summary>
        /// Sets the logger for this helper. Call once during initialization.
        /// </summary>
        public static void SetLogger(Action<string> logger)
        {
            _cachedLogger = logger;
        }

        /// <summary>
        /// Gets the game's pitch value directly from FirstPersonPlayerController.verticalRotation.
        /// This avoids Euler decomposition issues that occur when roll is applied to the camera.
        /// When roll is non-zero, localEulerAngles.x gives contaminated pitch values.
        /// Caches result per frame to minimize reflection overhead.
        /// </summary>
        /// <returns>The game's pitch value, or null if reflection failed</returns>
        public static float? GetGamePitchDirect()
        {
            try
            {
                if (!_reflectionInitialized)
                {
                    InitializeReflection();
                }

                // Return cached value if already computed this frame
                int currentFrame = UnityEngine.Time.frameCount;
                if (_lastCacheFrame == currentFrame)
                {
                    return _cachedPitch;
                }

                if (ReferenceEquals(_verticalRotationField, null))
                {
                    return null;
                }

                object? value = _verticalRotationField.GetValue(null);
                if (ReferenceEquals(value, null))
                {
                    return null;
                }

                float pitch = (float)value;

                // The game negates pitch when CameraInvertY is true:
                //   Euler(CameraInvertY ? -verticalRotation : verticalRotation, 0, 0)
                if (!ReferenceEquals(_cameraInvertYField, null))
                {
                    object? invertValue = _cameraInvertYField.GetValue(null);
                    if (!ReferenceEquals(invertValue, null) && (bool)invertValue)
                    {
                        pitch = -pitch;
                    }
                }

                _cachedPitch = pitch;
                _lastCacheFrame = currentFrame;
                return pitch;
            }
            catch (Exception ex)
            {
                // A read failure here repeats every frame (e.g. the game's
                // verticalRotation field is a different type in this build), so log
                // it once and fall back to Euler decomposition silently thereafter.
                if (!_readErrorLogged)
                {
                    _readErrorLogged = true;
                    _cachedLogger?.Invoke($"GetGamePitchDirect error (logged once): {ex.Message}");
                }
                return null;
            }
        }

        private static void InitializeReflection()
        {
            _reflectionInitialized = true;

            Type? playerControllerType = ReflectionUtil.FindType("FirstPersonPlayerController");
            if (ReferenceEquals(playerControllerType, null))
            {
                _cachedLogger?.Invoke("WARNING: FirstPersonPlayerController type not found!");
                return;
            }
            _cachedLogger?.Invoke($"Found FirstPersonPlayerController in {playerControllerType.Assembly.GetName().Name}");

            _verticalRotationField = playerControllerType.GetField(
                "verticalRotation",
                BindingFlags.Public | BindingFlags.Static);
            _cameraInvertYField = playerControllerType.GetField(
                "CameraInvertY",
                BindingFlags.Public | BindingFlags.Static);

            _cachedLogger?.Invoke(!ReferenceEquals(_verticalRotationField, null)
                ? "Found verticalRotation field"
                : "WARNING: verticalRotation field not found!");

            if (!ReferenceEquals(_cameraInvertYField, null))
            {
                _cachedLogger?.Invoke("Found CameraInvertY field");
            }
        }
    }
}
