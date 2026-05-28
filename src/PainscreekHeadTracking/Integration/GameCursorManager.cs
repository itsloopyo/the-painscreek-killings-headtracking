using System;
using System.Reflection;
using UnityEngine;

namespace PainscreekHeadTracking
{
    /// <summary>
    /// Manages the game's cursor and cursor text UI elements.
    /// Moves them to follow the decoupled aim reticle position.
    /// </summary>
    internal static class GameCursorManager
    {
        // Cursor and text repositioning
        private static RectTransform? _cursorRectTransform;
        private static RectTransform? _cursorTextRectTransform;
        private static bool _cursorSearched;
        private static Vector2 _cursorOriginalAnchoredPosition;
        private static Vector2 _cursorTextOriginalAnchoredPosition;
        private static float _canvasScaleFactor = 1f;

        // Cached reflection metadata. PlayerControl.instance is null for the first
        // frames after a scene load, so FindGameCursorElements retries every frame
        // until it appears. The type and its fields never change between retries -
        // resolving them once keeps the retry path to a single cheap GetValue instead
        // of a full AppDomain assembly scan (which also allocates a fresh array) plus
        // three GetField lookups on every pre-spawn frame.
        private static FieldInfo? _instanceField;
        private static FieldInfo? _cursorField;
        private static FieldInfo? _cursorTextField;
        private static bool _metadataResolved;
        private static bool _metadataFailed;

        // Cached logger to avoid delegate allocation on hot path
        private static Action<string>? _cachedLogger;

        /// <summary>
        /// Sets the logger for this manager. Call once during initialization.
        /// </summary>
        public static void SetLogger(Action<string> logger)
        {
            _cachedLogger = logger;
        }

        /// <summary>
        /// Updates cursor and cursor text positions to follow the reticle offset.
        /// </summary>
        public static void UpdatePosition(Vector2 reticleScreenOffset)
        {
            // Try to find cursor elements if we haven't yet
            if (!_cursorSearched)
            {
                _cursorSearched = true;
                FindGameCursorElements();
            }

            // Convert screen pixel offset to canvas units
            float offsetX = reticleScreenOffset.x / _canvasScaleFactor;
            float offsetY = reticleScreenOffset.y / _canvasScaleFactor;

            // Move cursor (the crosshair image)
            if (!IsNull(_cursorRectTransform))
            {
                _cursorRectTransform!.anchoredPosition = new Vector2(
                    _cursorOriginalAnchoredPosition.x + offsetX,
                    _cursorOriginalAnchoredPosition.y + offsetY
                );
            }

            // Move cursorText (the action text like "Examine")
            if (!IsNull(_cursorTextRectTransform))
            {
                _cursorTextRectTransform!.anchoredPosition = new Vector2(
                    _cursorTextOriginalAnchoredPosition.x + offsetX,
                    _cursorTextOriginalAnchoredPosition.y + offsetY
                );
            }
        }

        /// <summary>
        /// Resets cursor elements to their original positions.
        /// </summary>
        public static void ResetToOriginalPositions()
        {
            if (!IsNull(_cursorRectTransform))
            {
                _cursorRectTransform!.anchoredPosition = _cursorOriginalAnchoredPosition;
            }
            if (!IsNull(_cursorTextRectTransform))
            {
                _cursorTextRectTransform!.anchoredPosition = _cursorTextOriginalAnchoredPosition;
            }
        }

        // Plain .NET reference null check, for reflection objects (Type, FieldInfo,
        // PropertyInfo, boxed game instances). Uses ReferenceEquals because System.Type
        // lacks op_Inequality on the old Mono runtime this mod targets.
        private static bool IsNull(object? obj) => ReferenceEquals(obj, null);

        // Unity-object null check. Unity overrides == so a destroyed-but-not-GC'd
        // object reports as null; ReferenceEquals would miss that and we'd write
        // anchoredPosition to a dead transform (MissingReferenceException). Overload
        // resolution binds RectTransform/GameObject/Canvas arguments here.
        private static bool IsNull(UnityEngine.Object? obj) => obj == null;

        // Resolves the PlayerControl type and its fields once. Returns false if the
        // type or its instance field can't be found (permanent failure, logged once).
        private static bool ResolveReflectionMetadata()
        {
            if (_metadataResolved) return true;
            if (_metadataFailed) return false;

            Type? playerControlType = ReflectionUtil.FindType("PlayerControl");
            if (IsNull(playerControlType))
            {
                Log("Could not find PlayerControl type in any assembly");
                _metadataFailed = true;
                return false;
            }

            var instanceField = playerControlType!.GetField("instance", BindingFlags.Public | BindingFlags.Static);
            if (IsNull(instanceField))
            {
                Log("Could not find PlayerControl.instance field");
                _metadataFailed = true;
                return false;
            }

            _instanceField = instanceField;
            _cursorField = playerControlType.GetField("cursor", BindingFlags.Public | BindingFlags.Instance);
            _cursorTextField = playerControlType.GetField("cursorText", BindingFlags.Public | BindingFlags.Instance);
            _metadataResolved = true;
            return true;
        }

        private static void FindGameCursorElements()
        {
            try
            {
                if (!ResolveReflectionMetadata())
                {
                    return;
                }

                var playerControlInstance = _instanceField!.GetValue(null);
                if (IsNull(playerControlInstance))
                {
                    Log("PlayerControl.instance is null - will retry");
                    _cursorSearched = false; // Try again next frame
                    return;
                }

                // Get the cursor field (the crosshair GameObject)
                if (!IsNull(_cursorField))
                {
                    var cursorObj = _cursorField!.GetValue(playerControlInstance) as GameObject;
                    if (!IsNull(cursorObj))
                    {
                        _cursorRectTransform = cursorObj!.GetComponent<RectTransform>();
                        if (!IsNull(_cursorRectTransform))
                        {
                            _cursorOriginalAnchoredPosition = _cursorRectTransform!.anchoredPosition;
                            Log($"Found cursor, original pos: {_cursorOriginalAnchoredPosition}");

                            // Find canvas scale factor by looking up the hierarchy
                            var canvas = cursorObj.GetComponentInParent<Canvas>();
                            if (!IsNull(canvas))
                            {
                                _canvasScaleFactor = canvas!.scaleFactor;
                                Log($"Canvas scale factor: {_canvasScaleFactor}");
                            }
                        }
                        else
                        {
                            Log("cursor has no RectTransform");
                        }
                    }
                    else
                    {
                        Log("cursor GameObject is null");
                    }
                }

                // Get the cursorText field (the Text component showing action text)
                if (!IsNull(_cursorTextField))
                {
                    var cursorTextComponent = _cursorTextField!.GetValue(playerControlInstance);
                    if (!IsNull(cursorTextComponent))
                    {
                        // cursorText is a Text component, get its RectTransform
                        var componentType = cursorTextComponent!.GetType();
                        var transformProp = componentType.GetProperty("rectTransform", BindingFlags.Public | BindingFlags.Instance);
                        if (!IsNull(transformProp))
                        {
                            _cursorTextRectTransform = transformProp!.GetValue(cursorTextComponent, null) as RectTransform;
                            if (!IsNull(_cursorTextRectTransform))
                            {
                                _cursorTextOriginalAnchoredPosition = _cursorTextRectTransform!.anchoredPosition;
                                Log($"Found cursorText, original pos: {_cursorTextOriginalAnchoredPosition}");
                            }
                        }
                    }
                    else
                    {
                        Log("cursorText is null");
                    }
                }

                if (IsNull(_cursorRectTransform) && IsNull(_cursorTextRectTransform))
                {
                    Log("Failed to find any cursor elements");
                }
            }
            catch (Exception ex)
            {
                Log($"Error finding cursor elements: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private static void Log(string message)
        {
            _cachedLogger?.Invoke(message);
        }
    }
}
