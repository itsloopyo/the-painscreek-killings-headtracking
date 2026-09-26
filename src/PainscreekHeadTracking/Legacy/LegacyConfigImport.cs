using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using CameraUnlock.Core.Config;
using CameraUnlock.Core.Data;
using CameraUnlock.Core.Input;
using UnityEngine;

namespace PainscreekHeadTracking.Legacy
{
    /// <summary>
    /// The import the config owner runs on HeadTracking.cfg while CameraUnlock.ini is absent:
    /// <see cref="LegacyConfigReader"/>, then the map into <see cref="PainscreekConfig"/>.
    /// </summary>
    internal static class LegacyConfigImport
    {
        /// <summary>The rotation multiplier the dev pre-release shipped on every axis.</summary>
        public const float ShippedSensitivity = 1.0f;

        /// <summary>The rotation axis flips the dev pre-release shipped: none.</summary>
        public const bool ShippedInvert = false;

        public static LegacyImport<PainscreekConfig> Create()
        {
            return new LegacyImport<PainscreekConfig>(Run, LegacyConfigKeys.All());
        }

        /// <summary>
        /// A file the dev build failed to read gave it its defaults for the session. The import
        /// refuses it with that build's reason, so this session runs on those defaults, as that
        /// build did, nothing is written, and the next start tries again.
        /// </summary>
        public static ImportResult Run(LegacyImportInput input, PainscreekConfig config)
        {
            bool found;
            string? loadError;
            LegacyConfig legacy = LegacyConfigReader.Read(input.Path, null, out found, out loadError);
            var dropped = new List<DroppedValue>();
            var poseShaping = new List<PoseShapingValue>();
            Map(legacy, config, dropped, poseShaping);
            if (loadError != null) return ImportResult.Refused("Config load error (using defaults): " + loadError);
            return found ? ImportResult.Imported(dropped, poseShaping) : ImportResult.Absent(dropped, poseShaping);
        }

        /// <summary>
        /// The reader refuses NaN and infinity and keeps the port and the smoothing pair inside the
        /// canonical ranges, so no value reaches here that normalisation N2 would change. The file
        /// has no sections the reader looks at, so a dropped value names none.
        /// </summary>
        public static void Map(LegacyConfig legacy, PainscreekConfig config, List<DroppedValue> dropped,
            List<PoseShapingValue> poseShaping)
        {
            config.UdpPort = legacy.UdpPort;

            // The dev build started with head tracking on and in rotation and position whatever the
            // file said: it parsed EnableOnStartup and never applied it.
            config.EnableOnStartup = true;
            config.RotationEnabled = true;
            config.PositionEnabled = true;

            config.WorldSpaceYaw = legacy.WorldSpaceYaw;

            config.ToggleKeyName = HotkeyList(legacy.ToggleKeyName, LegacyKeyCodes.ToggleDefault, LegacyKeyCodes.ToggleChordLetter);
            config.CycleTrackingModeKeyName = HotkeyList(LegacyKeyCodes.CycleTrackingMode, LegacyKeyCodes.CycleTrackingModeChordLetter);
            config.YawModeKeyName = HotkeyList(legacy.YawModeKeyName, LegacyKeyCodes.YawModeDefault, LegacyKeyCodes.YawModeChordLetter);

            config.LocalSmoothing = legacy.LocalSmoothing;
            config.RemoteSmoothing = legacy.RemoteSmoothing;
            PositionSettings p = config.Position;
            config.Position = new PositionSettings(
                p.SensitivityX, p.SensitivityY, p.SensitivityZ,
                p.LimitX, p.LimitY, p.LimitYDown, p.LimitZ, p.LimitZBack,
                legacy.LocalSmoothing, legacy.RemoteSmoothing,
                p.InvertX, p.InvertY, p.InvertZ);

            LegacyPoseShaping.Record(legacy.YawSensitivity, ShippedSensitivity, "", "YawSensitivity", poseShaping, dropped);
            LegacyPoseShaping.Record(legacy.PitchSensitivity, ShippedSensitivity, "", "PitchSensitivity", poseShaping, dropped);
            LegacyPoseShaping.Record(legacy.RollSensitivity, ShippedSensitivity, "", "RollSensitivity", poseShaping, dropped);
            LegacyPoseShaping.Record(legacy.InvertYaw, ShippedInvert, "", "InvertYaw", poseShaping, dropped);
            LegacyPoseShaping.Record(legacy.InvertPitch, ShippedInvert, "", "InvertPitch", poseShaping, dropped);
            LegacyPoseShaping.Record(legacy.InvertRoll, ShippedInvert, "", "InvertRoll", poseShaping, dropped);

            // The dev build parsed ShowReticle and ReticleColor and drew no reticle of its own; it
            // moved the game's cursor to the aim whatever they said. Only a player who had set them
            // away from the defaults loses anything, and that is a choice that had no effect.
            if (!legacy.ShowDecoupledReticle)
            {
                dropped.Add(new DroppedValue(DropRule.Reticle, "", "ShowReticle", "false"));
            }
            float[] c = legacy.ReticleColorRgba;
            if (c[0] != 1f || c[1] != 1f || c[2] != 1f || c[3] != 1f)
            {
                string text = Encoding.ASCII.GetString(new ColorCodec().Render(new[] { c[0], c[1], c[2], c[3] }));
                dropped.Add(new DroppedValue(DropRule.Reticle, "", "ReticleColor", text));
            }
        }

        /// <summary>
        /// The keys the dev build fired an action on: the key its ParseKeyCode made of the name,
        /// unless that was None, and the Ctrl+Shift chord it checked beside it.
        /// </summary>
        /// <remarks>
        /// A name Enum.Parse reads as a number past the int range made ParseKeyCode throw, and that
        /// build then ran with no receiver at all. No approved rule covers it, so the name goes into
        /// the list as it was, which no hotkey list reads, and the owner defers the import naming
        /// the line.
        /// </remarks>
        public static string HotkeyList(string keyName, KeyCode fallback, KeyCode chordLetter)
        {
            KeyCode primary;
            try
            {
                primary = LegacyKeyCodes.Parse(keyName, fallback, null);
            }
            catch (OverflowException)
            {
                return keyName + ", " + Chord(chordLetter);
            }
            return HotkeyList(primary, chordLetter);
        }

        /// <summary>
        /// A key code the key table names no key for is written as its number, which no hotkey list
        /// reads, so the owner defers that import and says which line.
        /// </summary>
        public static string HotkeyList(KeyCode primary, KeyCode chordLetter)
        {
            if (primary == KeyCode.None) return Chord(chordLetter);
            return KeyText((int)primary) + ", " + Chord(chordLetter);
        }

        private static string Chord(KeyCode chordLetter)
        {
            return KeyBindings.Format(new[] { new KeyBinding(KeyModifiers.Ctrl | KeyModifiers.Shift, (int)chordLetter) });
        }

        private static string KeyText(int unityKeyCode)
        {
            try
            {
                return KeyBindings.Format(new[] { new KeyBinding(KeyModifiers.None, unityKeyCode) });
            }
            catch (ArgumentException)
            {
                return unityKeyCode.ToString(CultureInfo.InvariantCulture);
            }
        }
    }
}
