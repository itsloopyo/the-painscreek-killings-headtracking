extern alias oracle;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using CameraUnlock.Core.Input;
using PainscreekHeadTracking.Legacy;
using UnityEngine;
using OracleConfig = oracle::CameraUnlock.Core.Config.HeadTrackingConfigData;

namespace PainscreekHeadTracking.Tests.Differential
{
    /// <summary>
    /// What one run of a legacy reader gave: the settings, whether a file was there, what the
    /// startup code set up from them, and every log line of the read and of the startup.
    /// </summary>
    internal sealed class LegacyOutcome
    {
        public LegacyConfig Config = new LegacyConfig();
        public bool Found;
        public SortedDictionary<string, string> Startup = new SortedDictionary<string, string>(StringComparer.Ordinal);
        public List<string> Log = new List<string>();

        public string Describe()
        {
            return "found=" + LegacyStartup.Text(Found) + "\n" + LegacyStartup.Fields(Config)
                   + string.Concat(Startup.Select(kv => "startup " + kv.Key + "=" + kv.Value + "\n"))
                   + string.Concat(Log.Select(l => "log: " + l + "\n"));
        }
    }

    /// <summary>
    /// Both readers warn about the retired Smoothing key once per process, as the published build
    /// did. Each run starts both flags clear, so every input that holds the key logs the warning,
    /// and the runs are serialised so no other run sets a flag in between. The differential
    /// classes share one xunit collection for the same reason.
    /// </summary>
    internal static class ReaderRuns
    {
        private static readonly object Gate = new object();

        public static T Serialised<T>(Func<T> run)
        {
            lock (Gate)
            {
                Clear(typeof(OracleConfig));
                Clear(typeof(LegacyConfigReader));
                return run();
            }
        }

        private static void Clear(Type type)
        {
            FieldInfo? flag = type.GetField("_warnedRetiredSmoothingKey", BindingFlags.NonPublic | BindingFlags.Static);
            if (flag == null) throw new InvalidOperationException(type.FullName + " has no _warnedRetiredSmoothingKey");
            flag.SetValue(null, false);
        }
    }

    /// <summary>
    /// What the dev pre-release ran on for one input: core's HeadTrackingConfigData.LoadFromFile as
    /// 34656598 shipped it (oracle/, byte for byte), then the startup code of its StaticTracker.
    /// </summary>
    internal static class Oracle
    {
        public static LegacyOutcome Run(DifferentialInput input)
        {
            return ReaderRuns.Serialised(() =>
            {
                using (var folder = new LegacyFolder(input))
                {
                    var outcome = new LegacyOutcome { Found = input.Bytes != null };
                    OracleConfig c = OracleConfig.LoadFromFile(folder.LegacyPath, outcome.Log.Add);
                    outcome.Config = new LegacyConfig
                    {
                        UdpPort = c.UdpPort,
                        EnableOnStartup = c.EnableOnStartup,
                        YawSensitivity = c.Sensitivity.Yaw,
                        PitchSensitivity = c.Sensitivity.Pitch,
                        RollSensitivity = c.Sensitivity.Roll,
                        InvertYaw = c.Sensitivity.InvertYaw,
                        InvertPitch = c.Sensitivity.InvertPitch,
                        InvertRoll = c.Sensitivity.InvertRoll,
                        RecenterKeyName = c.RecenterKeyName,
                        ToggleKeyName = c.ToggleKeyName,
                        YawModeKeyName = c.YawModeKeyName,
                        WorldSpaceYaw = c.WorldSpaceYaw,
                        AimDecouplingEnabled = c.AimDecouplingEnabled,
                        ShowDecoupledReticle = c.ShowDecoupledReticle,
                        ReticleColorRgba = c.ReticleColorRgba,
                        LocalSmoothing = c.LocalSmoothing,
                        RemoteSmoothing = c.RemoteSmoothing,
                    };
                    outcome.Startup = LegacyStartup.Of(outcome.Config, (name, fallback) => OracleStartup.ParseKeyCode(name, fallback, outcome.Log));
                    return outcome;
                }
            });
        }
    }

    /// <summary>
    /// The dev pre-release's StaticTracker.ParseKeyCode (e4b385b), its body unchanged but for Log,
    /// which writes to the run's list here.
    /// </summary>
    internal static class OracleStartup
    {
        public static KeyCode ParseKeyCode(string keyName, KeyCode defaultKey, List<string> log)
        {
            Action<string> Log = log.Add;
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
    }

    /// <summary>
    /// The frozen reader and key parse on one input. It must leave the file and its folder as they
    /// were.
    /// </summary>
    internal static class FrozenReader
    {
        public static LegacyOutcome Run(DifferentialInput input)
        {
            return ReaderRuns.Serialised(() =>
            {
                using (var folder = new LegacyFolder(input))
                {
                    string[] before = folder.Entries();
                    DateTime written = input.Bytes == null ? DateTime.MinValue : File.GetLastWriteTimeUtc(folder.LegacyPath);

                    var outcome = new LegacyOutcome();
                    string? loadError;
                    outcome.Config = LegacyConfigReader.Read(folder.LegacyPath, outcome.Log.Add, out outcome.Found, out loadError);
                    if (loadError != null) throw new InvalidOperationException(input.Name + ": the reader failed: " + loadError);
                    outcome.Startup = LegacyStartup.Of(outcome.Config, (name, fallback) => LegacyKeyCodes.Parse(name, fallback, outcome.Log.Add));

                    if (!before.SequenceEqual(folder.Entries()))
                        throw new InvalidOperationException(input.Name + ": the frozen reader changed the folder: " + string.Join(", ", folder.Entries()));
                    if (input.Bytes != null)
                    {
                        if (!File.ReadAllBytes(folder.LegacyPath).SequenceEqual(input.Bytes))
                            throw new InvalidOperationException(input.Name + ": the frozen reader rewrote the legacy file");
                        if (File.GetLastWriteTimeUtc(folder.LegacyPath) != written)
                            throw new InvalidOperationException(input.Name + ": the frozen reader touched the legacy file");
                    }
                    return outcome;
                }
            });
        }
    }

    /// <summary>
    /// What the dev pre-release's StaticTracker set up from its settings, one line per item, floats
    /// with their bits. It started with tracking on and in rotation and position whatever the file
    /// said, and read neither RecenterKey, AimDecoupling nor the reticle settings. Its position
    /// settings were constants in PainscreekPositionDefaults, not settings.
    /// </summary>
    internal static class LegacyStartup
    {
        public static SortedDictionary<string, string> Of(LegacyConfig c, Func<string, KeyCode, KeyCode> parseKey)
        {
            var s = new SortedDictionary<string, string>(StringComparer.Ordinal);
            s["TrackingEnabled"] = "true";
            s["RotationEnabled"] = "true";
            s["PositionEnabled"] = "true";
            s["UdpPort"] = c.UdpPort.ToString(CultureInfo.InvariantCulture);
            s["WorldSpaceYaw"] = Text(c.WorldSpaceYaw);
            s["LocalSmoothing"] = Text(c.LocalSmoothing);
            s["RemoteSmoothing"] = Text(c.RemoteSmoothing);
            s["RotationSensitivity"] = Text(c.YawSensitivity) + " " + Text(c.PitchSensitivity) + " " + Text(c.RollSensitivity);
            s["RotationInversion"] = Text(c.InvertYaw) + " " + Text(c.InvertPitch) + " " + Text(c.InvertRoll);
            s["ToggleKey"] = Hotkey(() => parseKey(c.ToggleKeyName, LegacyKeyCodes.ToggleDefault), LegacyKeyCodes.ToggleChordLetter);
            s["CycleTrackingModeKey"] = Hotkey(() => LegacyKeyCodes.CycleTrackingMode, LegacyKeyCodes.CycleTrackingModeChordLetter);
            s["YawModeKey"] = Hotkey(() => parseKey(c.YawModeKeyName, LegacyKeyCodes.YawModeDefault), LegacyKeyCodes.YawModeChordLetter);
            return s;
        }

        /// <summary>
        /// ParseKeyCode catches only ArgumentException, so a key name Enum.Parse reads as a number
        /// too large for an int threw OverflowException out of StaticTracker.Initialize. That left
        /// the build with no receiver, so tracking never started. The exception is recorded as the
        /// key's outcome.
        /// </summary>
        private static string Hotkey(Func<KeyCode> parse, KeyCode chordLetter)
        {
            KeyCode primary;
            try
            {
                primary = parse();
            }
            catch (OverflowException ex)
            {
                return "throws " + ex.GetType().Name + ": " + ex.Message;
            }
            return Hotkey(primary, chordLetter);
        }

        /// <summary>Every field, floats with their bits.</summary>
        public static string Fields(LegacyConfig c)
        {
            var text = new StringBuilder();
            foreach (FieldInfo field in typeof(LegacyConfig).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                object? value = field.GetValue(c);
                string shown = value is float f ? Text(f)
                    : value is float[] a ? string.Join(" ", a.Select(Text))
                    : value is bool b ? Text(b)
                    : Convert.ToString(value, CultureInfo.InvariantCulture) ?? "null";
                text.Append(field.Name).Append('=').Append(shown).Append('\n');
            }
            return text.ToString();
        }

        public static string Text(bool value)
        {
            return value ? "true" : "false";
        }

        public static string Text(float value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture) + "/0x"
                   + BitConverter.ToInt32(BitConverter.GetBytes(value), 0).ToString("X8", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// The bindings StaticTracker.Pressed(primary, chord, letter) fired on: the primary key
        /// whatever else was held, unless it was None, and Ctrl+Shift+letter.
        /// </summary>
        public static string Hotkey(KeyCode primary, KeyCode chordLetter)
        {
            string chord = KeyBindings.Format(new[] { new KeyBinding(KeyModifiers.Ctrl | KeyModifiers.Shift, (int)chordLetter) });
            if (primary == KeyCode.None) return chord;
            return KeyName((int)primary) + ", " + chord;
        }

        /// <summary>A Unity key code's name, or the number for one that names no key.</summary>
        public static string KeyName(int unityKeyCode)
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
