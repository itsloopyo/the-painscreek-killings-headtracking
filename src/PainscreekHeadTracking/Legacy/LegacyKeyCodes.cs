using System;
using UnityEngine;

namespace PainscreekHeadTracking.Legacy
{
    /// <summary>
    /// StaticTracker.ParseKeyCode as the dev pre-release (e4b385b) shipped it, frozen: how that
    /// build turned the ToggleKey and YawModeKey names it read into the key it polled. It polled
    /// the named key with Input.GetKeyDown whatever else was held, and beside it a Ctrl+Shift chord
    /// (Y for the toggle, H for the yaw mode) that no setting changed. The mode cycle was PageUp and
    /// Ctrl+Shift+G, with no setting at all.
    /// </summary>
    internal static class LegacyKeyCodes
    {
        public const KeyCode ToggleDefault = KeyCode.End;
        public const KeyCode YawModeDefault = KeyCode.PageDown;
        public const KeyCode CycleTrackingMode = KeyCode.PageUp;
        public const KeyCode ToggleChordLetter = KeyCode.Y;
        public const KeyCode CycleTrackingModeChordLetter = KeyCode.G;
        public const KeyCode YawModeChordLetter = KeyCode.H;

        public static KeyCode Parse(string keyName, KeyCode defaultKey, Action<string>? log)
        {
            try
            {
                KeyCode parsed = (KeyCode)Enum.Parse(typeof(KeyCode), keyName, true);
                // Enum.Parse silently accepts numeric strings ("999") and returns
                // an undefined enum value, which Input.GetKeyDown then treats as
                // a dead key. Reject anything not a real KeyCode member.
                if (!Enum.IsDefined(typeof(KeyCode), parsed))
                {
                    log?.Invoke($"WARNING: Key name '{keyName}' is not a defined KeyCode, using default: {defaultKey}");
                    return defaultKey;
                }
                return parsed;
            }
            catch (ArgumentException)
            {
                // Invalid key name in config - use default and warn user
                log?.Invoke($"WARNING: Invalid key name '{keyName}' in config, using default: {defaultKey}");
                return defaultKey;
            }
        }
    }
}
