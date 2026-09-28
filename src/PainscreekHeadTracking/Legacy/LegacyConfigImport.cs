using System;
using System.Collections.Generic;
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
            var followsDefaultsIni = new LegacyFollowsDefaultsIni();
            Map(legacy, config, dropped, poseShaping, followsDefaultsIni);
            if (loadError != null) return ImportResult.Refused("Config load error (using defaults): " + loadError);
            return found
                ? ImportResult.Imported(dropped, poseShaping, followsDefaultsIni.Concepts)
                : ImportResult.Absent(dropped, poseShaping, followsDefaultsIni.Concepts);
        }

        /// <summary>
        /// The reader refuses NaN and infinity and keeps the port and the smoothing pair inside the
        /// canonical ranges, so no value reaches here that normalisation N2 would change. The file
        /// has no sections the reader looks at, so a dropped value names none. Every row is compared
        /// with what the dev build ran on from a fresh <see cref="LegacyConfig"/>, so a setting the
        /// player never changed follows Defaults.ini.
        /// </summary>
        public static void Map(LegacyConfig legacy, PainscreekConfig config, List<DroppedValue> dropped,
            List<PoseShapingValue> poseShaping, LegacyFollowsDefaultsIni followsDefaultsIni)
        {
            var shipped = new LegacyConfig();

            config.UdpPort = legacy.UdpPort;
            followsDefaultsIni.Setting(ConfigConcepts.UdpPort, legacy.UdpPort, shipped.UdpPort);

            // The dev build started with head tracking on and in rotation and position whatever the
            // file said: it parsed EnableOnStartup and never applied it, so no player changed either.
            config.EnableOnStartup = true;
            followsDefaultsIni.NotInLegacy(ConfigConcepts.EnableOnStartup);
            config.RotationEnabled = true;
            config.PositionEnabled = true;
            followsDefaultsIni.TrackingMode(true);

            config.WorldSpaceYaw = legacy.WorldSpaceYaw;
            followsDefaultsIni.Setting(ConfigConcepts.WorldSpaceYaw, legacy.WorldSpaceYaw, shipped.WorldSpaceYaw);

            config.ToggleKeyName = HotkeyList(legacy.ToggleKeyName, LegacyKeyCodes.ToggleDefault, LegacyKeyCodes.ToggleChordLetter,
                "ToggleKey", dropped);
            followsDefaultsIni.Setting(ConfigConcepts.ToggleKey, PollsTheDefault(legacy.ToggleKeyName, LegacyKeyCodes.ToggleDefault));
            config.CycleTrackingModeKeyName = HotkeyList(LegacyKeyCodes.CycleTrackingMode, LegacyKeyCodes.CycleTrackingModeChordLetter,
                "CycleTrackingModeKey", dropped);
            followsDefaultsIni.NotInLegacy(ConfigConcepts.CycleTrackingModeKey);
            config.YawModeKeyName = HotkeyList(legacy.YawModeKeyName, LegacyKeyCodes.YawModeDefault, LegacyKeyCodes.YawModeChordLetter,
                "YawModeKey", dropped);
            followsDefaultsIni.Setting(ConfigConcepts.YawModeKey, PollsTheDefault(legacy.YawModeKeyName, LegacyKeyCodes.YawModeDefault));

            config.LocalSmoothing = legacy.LocalSmoothing;
            followsDefaultsIni.Setting(ConfigConcepts.LocalSmoothing, legacy.LocalSmoothing, shipped.LocalSmoothing);
            config.RemoteSmoothing = legacy.RemoteSmoothing;
            followsDefaultsIni.Setting(ConfigConcepts.RemoteSmoothing, legacy.RemoteSmoothing, shipped.RemoteSmoothing);
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

            // The dev build parsed the aim decoupling switch and never read it: aim was decoupled
            // whatever the file said, as it is now. A player who had it off still sees the drop.
            if (!legacy.AimDecouplingEnabled)
            {
                dropped.Add(new DroppedValue(DropRule.CoupledAim, "", "AimDecoupling", "false"));
            }

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
        public static string HotkeyList(string keyName, KeyCode fallback, KeyCode chordLetter, string legacyKey,
            ICollection<DroppedValue> dropped)
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
            return HotkeyList(primary, chordLetter, legacyKey, dropped);
        }

        /// <summary>
        /// A Ctrl, Shift or Alt key on its own is left unbound and recorded under
        /// <paramref name="legacyKey"/> (normalisation N3), and the chord stays. So is a key code the
        /// key table names no key for, recorded as KeyCodeOutOfRange (normalisation N1). The dev
        /// build's parse gave its default key for any name Unity's KeyCode does not define, so no
        /// file reaches that case.
        /// </summary>
        public static string HotkeyList(KeyCode primary, KeyCode chordLetter, string legacyKey, ICollection<DroppedValue> dropped)
        {
            string key = LegacyNormalisations.KeyCodeToBindings((int)primary, "", legacyKey, dropped);
            return key.Length == 0 ? Chord(chordLetter) : key + ", " + Chord(chordLetter);
        }

        /// <summary>
        /// Whether the dev build polled its default key for this name: a name it could not parse fell
        /// back to the default, and one past the int range left it polling nothing.
        /// </summary>
        private static bool PollsTheDefault(string keyName, KeyCode fallback)
        {
            try
            {
                return LegacyKeyCodes.Parse(keyName, fallback, null) == fallback;
            }
            catch (OverflowException)
            {
                return false;
            }
        }

        private static string Chord(KeyCode chordLetter)
        {
            return KeyBindings.Format(new[] { new KeyBinding(KeyModifiers.Ctrl | KeyModifiers.Shift, (int)chordLetter) });
        }
    }
}
