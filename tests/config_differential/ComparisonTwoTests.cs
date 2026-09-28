using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CameraUnlock.Core.Config;
using CameraUnlock.Core.Input;
using PainscreekHeadTracking.Legacy;
using UnityEngine;
using Xunit;

namespace PainscreekHeadTracking.Tests.Differential
{
    /// <summary>
    /// Comparison 2, the import against the migration, and what the import does with each value
    /// the dev build ran on. Every input of comparison 1 is migrated into a new CameraUnlock.ini,
    /// from a writable and from a read-only legacy file, once over the built-in Defaults.ini and
    /// once over a Defaults.ini that differs on every row this game takes from it.
    /// </summary>
    [Collection(DifferentialCollection.Name)]
    public class ComparisonTwoTests
    {
        /// <summary>Every global row the table binds, each away from its built-in value.</summary>
        private const string OtherDefaults =
            "[CameraUnlock]\r\nConfigFormat=1\r\n\r\n" +
            "[Network]\r\nUdpPort=4343\r\n\r\n" +
            "[General]\r\nEnableOnStartup=false\r\nWorldSpaceYaw=false\r\nRotationEnabled=true\r\n\r\n" +
            "[Smoothing]\r\nLocalSmoothing=0.25\r\nRemoteSmoothing=0.35\r\n\r\n" +
            "[Position]\r\nPositionEnabled=false\r\n\r\n" +
            "[Hotkeys]\r\nToggleKey=F8\r\nCycleTrackingModeKey=F7\r\nYawModeKey=F6\r\n";

        private static readonly string[] HotkeyRows = { "ToggleKey", "YawModeKey" };

        private static readonly Lazy<string> MigratedDir = new Lazy<string>(() =>
        {
            string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "migrated");
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            Directory.CreateDirectory(dir);
            return dir;
        });

        /// <summary>
        /// Normalisation N1 on a key name: where Enum.Parse read the dev build's ToggleKey or
        /// YawModeKey as a number past the int range, its ParseKeyCode threw and that build ran with
        /// no receiver. No key has that name, so the import leaves it unbound, logs the drop and
        /// keeps the chord, and the file migrates.
        /// </summary>
        [Fact]
        public void AKeyNamePastTheIntRangeImportsAsUnboundAndKeepsTheChord()
        {
            DifferentialInput[] overflowing = Inputs.All()
                .Where(i => HotkeyRows.Any(row => Oracle.Run(i).Startup[row].StartsWith("throws ", StringComparison.Ordinal)))
                .OrderBy(i => i.Name, StringComparer.Ordinal)
                .ToArray();
            Assert.Equal(new[] { "corpus ToggleKey: value 1100 characters", "corpus YawModeKey: value 1100 characters" },
                overflowing.Select(i => i.Name));
            foreach (DifferentialInput input in overflowing)
            {
                bool toggle = input.Name.StartsWith("corpus ToggleKey:", StringComparison.Ordinal);
                string row = toggle ? "ToggleKey" : "YawModeKey";
                string chord = toggle ? "Ctrl+Shift+Y" : "Ctrl+Shift+H";
                LegacyConfig old = Oracle.Run(input).Config;
                string keyName = toggle ? old.ToggleKeyName : old.YawModeKeyName;
                MigrationOutcome migration = MigrationOutcome.Run(input, null, false);
                Assert.Equal(ConfigLoadStatus.Migrated, migration.Status);
                Assert.Contains("\r\n" + row + "=" + chord + "\r\n", Encoding.ASCII.GetString(migration.Created!));
                Assert.Contains(migration.Log, l => l.Contains(row + "=" + keyName + ", it is not a key code Unity names"));
            }

            var dropped = new List<DroppedValue>();
            string name = new string('9', 1100);
            Assert.Equal("Ctrl+Shift+Y",
                LegacyConfigImport.HotkeyList(name, LegacyKeyCodes.ToggleDefault, LegacyKeyCodes.ToggleChordLetter, "ToggleKey", dropped));
            DroppedValue drop = Assert.Single(dropped);
            Assert.Equal(DropRule.KeyCodeOutOfRange, drop.Rule);
            Assert.Equal("", drop.Section);
            Assert.Equal("ToggleKey", drop.Key);
            Assert.Equal(name, drop.Value);
        }

        [Fact]
        public void TheMigrationHoldsWhatTheImportReadOverTheBuiltInDefaults()
        {
            Compare(null);
        }

        [Fact]
        public void TheMigrationHoldsWhatTheImportReadOverOtherDefaults()
        {
            Compare(OtherDefaults);
        }

        private static void Compare(string? defaultsIni)
        {
            List<DifferentialInput> inputs = Inputs.All().ToList();
            var failures = new ConcurrentBag<string>();
            var created = new ConcurrentDictionary<string, byte[]>(StringComparer.Ordinal);
            byte[] committed = File.ReadAllBytes(ConfigTests.Committed());
            var defaults = new PainscreekConfig();
            PainscreekConfig.Table().Apply(CanonicalIni.Parse(defaultsIni == null ? new byte[0] : Encoding.ASCII.GetBytes(defaultsIni)), defaults);
            Dictionary<string, string> defaultLines = Lines(MigrationOutcome.Describe(defaults));
            Parallel.ForEach(inputs, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, input =>
            {
                ImportOutcome import = ImportOutcome.Run(input);
                foreach (bool readOnly in input.Bytes == null ? new[] { false } : new[] { false, true })
                {
                    string name = input.Name + (readOnly ? " (read-only)" : "");
                    MigrationOutcome migration = MigrationOutcome.Run(input, defaultsIni, readOnly);

                    string imported = FollowingDefaultsIni(MigrationOutcome.Describe(import.Config), import.Result, defaultLines);
                    string migrated = MigrationOutcome.Describe(migration.Config);
                    if (input.Bytes == null)
                    {
                        if (migration.Status != ConfigLoadStatus.Created) failures.Add(name + ": " + migration.Status);
                        if (migration.Created == null || !migration.Created.SequenceEqual(committed)) failures.Add(name + ": the created file is not config/CameraUnlock.ini");
                        // With no file the dev build ran on its defaults, which are the built-in values.
                        if (defaultsIni == null && imported != migrated) failures.Add(name + ":\n" + ComparisonOneTests.Diff(imported, migrated));
                        continue;
                    }

                    if (migration.Status != ConfigLoadStatus.Migrated)
                    {
                        failures.Add(name + ": " + migration.Status + ": " + migration.Reason);
                        continue;
                    }
                    created[ComparisonOneTests.Sha256(migration.Created!)] = migration.Created!;
                    string text = Encoding.ASCII.GetString(migration.Created!);
                    foreach (ConceptDescriptor concept in import.Result.FollowsDefaultsIni)
                    {
                        if (!text.Contains("\r\n" + concept.Key + "=default\r\n"))
                            failures.Add(name + ": " + concept.Key + " follows Defaults.ini and is not written default");
                    }
                    if (imported != migrated) failures.Add(name + ":\n" + ComparisonOneTests.Diff(imported, migrated));
                }
            });
            Assert.True(failures.IsEmpty, string.Join("\n", failures.OrderBy(f => f, StringComparer.Ordinal).Take(20)));
            // Handed to core's canonical config lint by tests/config_differential/lint-migrated.mjs,
            // which pixi run test runs next.
            foreach (KeyValuePair<string, byte[]> file in created)
            {
                File.WriteAllBytes(Path.Combine(MigratedDir.Value, file.Key + ".ini"), file.Value);
            }
        }

        /// <summary>
        /// What the migration gives: the import, with every row it leaves to Defaults.ini at the
        /// value <paramref name="defaultLines"/> holds for it.
        /// </summary>
        private static string FollowingDefaultsIni(string described, ImportResult result, Dictionary<string, string> defaultLines)
        {
            Dictionary<string, string> lines = Lines(described);
            foreach (ConceptDescriptor concept in result.FollowsDefaultsIni)
            {
                foreach (string name in new[] { concept.Key, "Position." + concept.Key })
                {
                    if (lines.ContainsKey(name)) lines[name] = defaultLines[name];
                }
            }
            var s = new StringBuilder();
            foreach (string name in Lines(described).Keys) s.Append(name).Append('=').Append(lines[name]).Append('\n');
            return s.ToString();
        }

        private static Dictionary<string, string> Lines(string described)
        {
            var lines = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string line in described.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int eq = line.IndexOf('=');
                lines.Add(line.Substring(0, eq), line.Substring(eq + 1));
            }
            return lines;
        }

        /// <summary>
        /// The map proof: on every input, what the converted mod runs on from the import is what the
        /// dev build ran on, apart from exactly the values the approved changes drop.
        /// </summary>
        [Fact]
        public void TheImportKeepsEverySettingButTheApprovedDrops()
        {
            var failures = new List<string>();
            foreach (DifferentialInput input in Inputs.All())
            {
                LegacyOutcome oracle = Oracle.Run(input);
                ImportOutcome import = ImportOutcome.Run(input);
                LegacyConfig old = oracle.Config;
                ImportResult result = import.Result;
                ImportStatus status = input.Bytes == null ? ImportStatus.Absent : ImportStatus.Imported;
                if (result.Status != status) failures.Add(input.Name + ": " + result.Status);

                var before = new SortedDictionary<string, string>(oracle.Startup, StringComparer.Ordinal);
                var expectedDrops = new List<string>();
                var keyDrops = new List<string>();
                Action<string, string, KeyCode, KeyCode> hotkey = (key, keyName, fallback, letter) =>
                {
                    KeyCode primary;
                    try
                    {
                        primary = LegacyKeyCodes.Parse(keyName, fallback, null);
                    }
                    catch (OverflowException)
                    {
                        before[key] = LegacyStartup.Hotkey(KeyCode.None, letter);
                        keyDrops.Add("KeyCodeOutOfRange  " + key + " " + keyName);
                        return;
                    }
                    if (!IsModifier(primary)) return;
                    before[key] = LegacyStartup.Hotkey(KeyCode.None, letter);
                    keyDrops.Add("ModifierKey  " + key + " " + primary);
                };
                hotkey("ToggleKey", old.ToggleKeyName, LegacyKeyCodes.ToggleDefault, LegacyKeyCodes.ToggleChordLetter);
                hotkey("YawModeKey", old.YawModeKeyName, LegacyKeyCodes.YawModeDefault, LegacyKeyCodes.YawModeChordLetter);
                SortedDictionary<string, string> after = ConvertedStartup.Of(import.Config);
                Assert.Equal(before.Keys, after.Keys);
                foreach (string key in before.Keys)
                {
                    if (key == "RotationSensitivity" || key == "RotationInversion") continue;
                    if (before[key] != after[key]) failures.Add(input.Name + ": " + key + " " + before[key] + " -> " + after[key]);
                }

                expectedDrops.AddRange(keyDrops);
                var expectedShaping = new List<string>();
                Action<string, string, string, bool> shaping = (key, value, shipped, folded) =>
                {
                    expectedShaping.Add(" " + key + " " + value + " " + shipped + " " + folded);
                    if (!folded) expectedDrops.Add("PoseShaping  " + key + " " + value);
                };
                Action<string, float> sensitivity = (key, value) => shaping(key, Codec(value), "1.0", value == 1.0f);
                Action<string, bool> inversion = (key, value) => shaping(key, Text(value), "false", !value);
                sensitivity("YawSensitivity", old.YawSensitivity);
                sensitivity("PitchSensitivity", old.PitchSensitivity);
                sensitivity("RollSensitivity", old.RollSensitivity);
                inversion("InvertYaw", old.InvertYaw);
                inversion("InvertPitch", old.InvertPitch);
                inversion("InvertRoll", old.InvertRoll);
                if (!old.AimDecouplingEnabled) expectedDrops.Add("CoupledAim  AimDecoupling false");
                if (!old.ShowDecoupledReticle) expectedDrops.Add("Reticle  ShowReticle false");
                float[] c = old.ReticleColorRgba;
                if (c[0] != 1f || c[1] != 1f || c[2] != 1f || c[3] != 1f)
                {
                    expectedDrops.Add("Reticle  ReticleColor " + Codec(c[0]) + ", " + Codec(c[1]) + ", " + Codec(c[2]) + ", " + Codec(c[3]));
                }

                string[] drops = result.Dropped.Select(d => d.Rule + " " + d.Section + " " + d.Key + " " + d.Value).ToArray();
                string[] poses = result.PoseShaping.Select(p => p.Section + " " + p.Key + " " + p.Value + " " + p.Shipped + " " + p.Folded).ToArray();
                if (!drops.SequenceEqual(expectedDrops)) failures.Add(input.Name + ": dropped " + string.Join("; ", drops));
                if (!poses.SequenceEqual(expectedShaping)) failures.Add(input.Name + ": pose shaping " + string.Join("; ", poses));

                // A setting the player never changed from what the dev build ran on follows
                // Defaults.ini. That build ignored EnableOnStartup and the tracking mode and had no
                // mode key setting, so those always follow; a key name follows where the build
                // polled its default key for it.
                var shipped = new LegacyConfig();
                var expectedFollows = new List<string>();
                Action<string, bool> follows = (key, unchanged) => { if (unchanged) expectedFollows.Add(key); };
                follows("UdpPort", old.UdpPort == shipped.UdpPort);
                follows("EnableOnStartup", true);
                follows("RotationEnabled", true);
                follows("PositionEnabled", true);
                follows("WorldSpaceYaw", old.WorldSpaceYaw == shipped.WorldSpaceYaw);
                follows("ToggleKey", oracle.Startup["ToggleKey"] == LegacyStartup.Hotkey(LegacyKeyCodes.ToggleDefault, LegacyKeyCodes.ToggleChordLetter));
                follows("CycleTrackingModeKey", true);
                follows("YawModeKey", oracle.Startup["YawModeKey"] == LegacyStartup.Hotkey(LegacyKeyCodes.YawModeDefault, LegacyKeyCodes.YawModeChordLetter));
                follows("LocalSmoothing", old.LocalSmoothing.Equals(shipped.LocalSmoothing));
                follows("RemoteSmoothing", old.RemoteSmoothing.Equals(shipped.RemoteSmoothing));
                string[] followed = result.FollowsDefaultsIni.Select(f => f.Key).ToArray();
                if (!followed.SequenceEqual(expectedFollows)) failures.Add(input.Name + ": follows Defaults.ini " + string.Join(", ", followed));
            }
            Assert.True(failures.Count == 0, string.Join("\n", failures.Take(20)));
        }

        /// <summary>
        /// Every documented example folds its pose shaping: the sensitivities and flips it holds are
        /// the ones the conversion moved into code, so nothing is dropped for a player who never
        /// changed them.
        /// </summary>
        [Fact]
        public void EveryExampleFoldsItsPoseShaping()
        {
            foreach (DifferentialInput input in Inputs.Examples())
            {
                ImportOutcome import = ImportOutcome.Run(input);
                Assert.Equal(6, import.Result.PoseShaping.Count);
                Assert.True(import.Result.PoseShaping.All(p => p.Folded), input.Name);
                Assert.Empty(import.Result.Dropped);
            }
        }

        /// <summary>
        /// Fresh equals upgrade. No build shipped, seeded or wrote a HeadTracking.cfg, so the file
        /// the dev build documented stands in for one: the README's example migrates, over the
        /// built-in Defaults.ini, into the committed file byte for byte. No default moved.
        /// </summary>
        [Fact]
        public void TheNewestExampleMigratesToTheCommittedFile()
        {
            MigrationOutcome migration = MigrationOutcome.Run(
                new DifferentialInput("README example 795a88c", Inputs.NewestExample()), null, false);

            Assert.Equal(ConfigLoadStatus.Migrated, migration.Status);
            Assert.Equal(Encoding.ASCII.GetString(File.ReadAllBytes(ConfigTests.Committed())),
                Encoding.ASCII.GetString(migration.Created!));
        }

        /// <summary>
        /// The first example still holds the Smoothing key the dev build warned about and ignored,
        /// and the migration log names it as not carried.
        /// </summary>
        [Fact]
        public void AnOlderExampleLogsWhatItDoesNotCarry()
        {
            DifferentialInput first = Inputs.Examples().Single(i => i.Name == "README example 99d6e86");
            MigrationOutcome migration = MigrationOutcome.Run(first, null, false);

            Assert.Equal(ConfigLoadStatus.Migrated, migration.Status);
            Assert.Contains(migration.Log, l => l.Contains("not carried: [Smoothing] Smoothing=0.0"));
        }

        /// <summary>Every Unity code in core's key table converts to the key name that reads back as it.</summary>
        [Fact]
        public void EveryUnityKeyCodeConvertsToItsName()
        {
            string keys = File.ReadAllText(Path.Combine(Path.Combine(Path.Combine(ConfigTests.RepoRoot(), "cameraunlock-core"), "data"), "keys.json"));
            var codes = new List<int>();
            foreach (Match m in Regex.Matches(keys, "\"unity\":\\s*(\\d+)"))
            {
                codes.Add(int.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture));
            }
            Assert.True(codes.Count > 300, "keys.json gave " + codes.Count + " Unity codes");
            foreach (int code in codes.Where(c => c != 0 && !IsModifier((KeyCode)c)))
            {
                var dropped = new List<DroppedValue>();
                string list = LegacyConfigImport.HotkeyList((KeyCode)code, KeyCode.H, "YawModeKey", dropped);
                Assert.Empty(dropped);
                KeyBinding[] bindings;
                string error;
                Assert.True(KeyBindings.TryParse(list, out bindings, out error), code + ": " + list + ": " + error);
                Assert.Equal(new KeyBinding(KeyModifiers.None, code), bindings[0]);
                Assert.Equal(new KeyBinding(KeyModifiers.Ctrl | KeyModifiers.Shift, (int)KeyCode.H), bindings[1]);
            }
            var none = new List<DroppedValue>();
            Assert.Equal("Ctrl+Shift+G", LegacyConfigImport.HotkeyList(KeyCode.None, KeyCode.G, "CycleTrackingModeKey", none));
            Assert.Empty(none);
        }

        /// <summary>
        /// The test build's KeyCode is core's stub, which declares about a third of the game's
        /// members, so the parse half of comparisons 1 and 2 reads a name the stub lacks
        /// (Semicolon, JoystickButton0) as invalid on both sides, where the game reads it as a key.
        /// This covers what those comparisons cannot, over the game's own KeyCode as
        /// data/game-keycodes.tsv records it: every key the game parses a name to converts to a
        /// hotkey list that reads back as that key, and every name the stub declares holds the
        /// game's value.
        /// </summary>
        [Fact]
        public void EveryGameKeyCodeConvertsToAListThatReadsBackAsIt()
        {
            string[] lines = File.ReadAllLines(Path.Combine(Path.Combine(Inputs.DifferentialDir(), "data"), "game-keycodes.tsv"));
            Assert.Equal(321, lines.Length);
            foreach (string line in lines)
            {
                string[] fields = line.Split('	');
                Assert.Equal(2, fields.Length);
                string name = fields[0];
                int code = int.Parse(fields[1], System.Globalization.CultureInfo.InvariantCulture);

                if (Enum.IsDefined(typeof(KeyCode), name))
                {
                    Assert.Equal(code, (int)(KeyCode)Enum.Parse(typeof(KeyCode), name));
                }

                var dropped = new List<DroppedValue>();
                string list = LegacyConfigImport.HotkeyList((KeyCode)code, KeyCode.Y, "ToggleKey", dropped);
                KeyBinding[] bindings;
                string error;
                Assert.True(KeyBindings.TryParse(list, out bindings, out error), name + ": " + list + ": " + error);
                if (code == 0 || IsModifier((KeyCode)code))
                {
                    Assert.Equal(new[] { new KeyBinding(KeyModifiers.Ctrl | KeyModifiers.Shift, (int)KeyCode.Y) }, bindings);
                    Assert.Equal(code == 0 ? 0 : 1, dropped.Count);
                    continue;
                }
                Assert.Empty(dropped);
                Assert.Equal(2, bindings.Length);
                Assert.Equal(new KeyBinding(KeyModifiers.None, code), bindings[0]);
                Assert.Equal(new KeyBinding(KeyModifiers.Ctrl | KeyModifiers.Shift, (int)KeyCode.Y), bindings[1]);
            }
        }

        /// <summary>
        /// Normalisation N1: a key code Unity names no key for is left unbound, the drop is logged,
        /// and the action keeps its Ctrl+Shift chord. A file cannot reach it: the dev build's parse
        /// gave its default key for a number Unity's KeyCode does not define, and so does the import,
        /// so the file migrates holding that default.
        /// </summary>
        [Fact]
        public void AKeyCodeUnityNamesNoKeyForImportsAsUnboundAndKeepsTheChord()
        {
            foreach (int code in new[] { -1, 1, 2, 10, 999 })
            {
                Assert.False(KeyBindings.HasName(code), code + " names a key");
                var dropped = new List<DroppedValue>();
                Assert.Equal("Ctrl+Shift+H", LegacyConfigImport.HotkeyList((KeyCode)code, KeyCode.H, "YawModeKey", dropped));
                DroppedValue drop = Assert.Single(dropped);
                Assert.Equal(DropRule.KeyCodeOutOfRange, drop.Rule);
                Assert.Equal("", drop.Section);
                Assert.Equal("YawModeKey", drop.Key);
                Assert.Equal(code.ToString(System.Globalization.CultureInfo.InvariantCulture), drop.Value);
            }

            var none = new List<DroppedValue>();
            Assert.Equal("End, Ctrl+Shift+Y", LegacyConfigImport.HotkeyList("999", LegacyKeyCodes.ToggleDefault, LegacyKeyCodes.ToggleChordLetter, "ToggleKey", none));
            Assert.Empty(none);
        }

        /// <summary>
        /// Normalisation N3: a Ctrl, Shift or Alt key on its own is left unbound, the drop is logged,
        /// and the action keeps its Ctrl+Shift chord.
        /// </summary>
        [Fact]
        public void AModifierKeyOnItsOwnImportsAsUnboundAndKeepsTheChord()
        {
            foreach (KeyCode modifier in Modifiers)
            {
                var dropped = new List<DroppedValue>();
                Assert.Equal("Ctrl+Shift+Y", LegacyConfigImport.HotkeyList(modifier, KeyCode.Y, "ToggleKey", dropped));
                DroppedValue drop = Assert.Single(dropped);
                Assert.Equal(DropRule.ModifierKey, drop.Rule);
                Assert.Equal("", drop.Section);
                Assert.Equal("ToggleKey", drop.Key);
                Assert.Equal(modifier.ToString(), drop.Value);
            }

            MigrationOutcome migration = MigrationOutcome.Run(
                new DifferentialInput("ToggleKey = RightShift", Edited("ToggleKey = End ", "ToggleKey = RightShift ")), null, false);
            Assert.Equal(ConfigLoadStatus.Migrated, migration.Status);
            Assert.Equal("Ctrl+Shift+Y", migration.Config.ToggleKeyName);
            Assert.Contains("\r\nToggleKey=Ctrl+Shift+Y\r\n", Encoding.ASCII.GetString(migration.Created!));
            Assert.Contains(migration.Log, l => l.Contains("ToggleKey=RightShift, it is a Ctrl, Shift or Alt key"));
        }

        /// <summary>
        /// A setting the player never changed from what the dev build ran on takes Defaults.ini's
        /// value and is written default, whatever Defaults.ini holds; the tracking mode, which that
        /// build never read, always does. A setting the player changed keeps its value, written
        /// default only where it equals what default gives.
        /// </summary>
        [Fact]
        public void AnUntouchedSettingFollowsDefaultsIniAndAChangedOneStays()
        {
            MigrationOutcome untouched = MigrationOutcome.Run(
                new DifferentialInput("README example 795a88c", Inputs.NewestExample()), OtherDefaults, false);
            Assert.Equal(ConfigLoadStatus.Migrated, untouched.Status);
            PainscreekConfig c = untouched.Config;
            Assert.Equal(4343, c.UdpPort);
            Assert.False(c.EnableOnStartup);
            Assert.False(c.WorldSpaceYaw);
            Assert.True(c.RotationEnabled);
            Assert.False(c.PositionEnabled);
            Assert.Equal(0.25f, c.LocalSmoothing);
            Assert.Equal(0.35f, c.RemoteSmoothing);
            Assert.Equal("F8", c.ToggleKeyName);
            Assert.Equal("F7", c.CycleTrackingModeKeyName);
            Assert.Equal("F6", c.YawModeKeyName);
            Assert.Equal(Encoding.ASCII.GetString(File.ReadAllBytes(ConfigTests.Committed())),
                Encoding.ASCII.GetString(untouched.Created!));

            byte[] legacy = Edited("ToggleKey = End ", "ToggleKey = F9 ", "WorldSpaceYaw = true ", "WorldSpaceYaw = false ",
                "UdpPort = 4242 ", "UdpPort = 4343 ", "LocalSmoothing = 0.0 ", "LocalSmoothing = 0.3 ");
            MigrationOutcome changed = MigrationOutcome.Run(new DifferentialInput("changed", legacy), OtherDefaults, false);
            Assert.Equal(ConfigLoadStatus.Migrated, changed.Status);
            string text = Encoding.ASCII.GetString(changed.Created!);
            Assert.Contains("\r\nToggleKey=F9, Ctrl+Shift+Y\r\n", text);
            Assert.Contains("\r\nLocalSmoothing=0.3\r\n", text);
            // Changed, and equal to what default gives over these defaults, so still written default.
            Assert.Contains("\r\nWorldSpaceYaw=default\r\n", text);
            Assert.Contains("\r\nUdpPort=default\r\n", text);
            Assert.Contains("\r\nRotationEnabled=default\r\n", text);
            Assert.Contains("\r\nPositionEnabled=default\r\n", text);
            Assert.Contains("\r\nYawModeKey=default\r\n", text);
            Assert.Equal("F9, Ctrl+Shift+Y", changed.Config.ToggleKeyName);
            Assert.Equal(0.3f, changed.Config.LocalSmoothing);
            Assert.False(changed.Config.WorldSpaceYaw);
            Assert.False(changed.Config.PositionEnabled);
        }

        /// <summary>The README's newest example with each pair of texts replaced.</summary>
        private static byte[] Edited(params string[] pairs)
        {
            string text = Encoding.ASCII.GetString(Inputs.NewestExample());
            for (int i = 0; i < pairs.Length; i += 2)
            {
                Assert.Contains(pairs[i], text);
                text = text.Replace(pairs[i], pairs[i + 1]);
            }
            return Encoding.ASCII.GetBytes(text);
        }

        private static readonly KeyCode[] Modifiers =
        {
            KeyCode.LeftControl, KeyCode.RightControl, KeyCode.LeftShift, KeyCode.RightShift, KeyCode.LeftAlt, KeyCode.RightAlt,
        };

        private static bool IsModifier(KeyCode code)
        {
            return Array.IndexOf(Modifiers, code) >= 0;
        }

        private static string Codec(float value)
        {
            return Encoding.ASCII.GetString(new FloatCodec().Render(value));
        }

        private static string Text(bool value)
        {
            return value ? "true" : "false";
        }
    }
}
