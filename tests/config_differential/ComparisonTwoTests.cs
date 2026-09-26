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
        /// The inputs whose ToggleKey or YawModeKey made the dev build's ParseKeyCode throw, which
        /// left that build with no receiver. No approved rule covers the name, so the owner defers
        /// these imports: the session runs on what the import gave, nothing is written, and the
        /// import runs again at the next start.
        /// </summary>
        private static readonly Lazy<string[]> Deferred = new Lazy<string[]>(() =>
            Inputs.All()
                .Where(i => HotkeyRows.Any(row => Oracle.Run(i).Startup[row].StartsWith("throws ", StringComparison.Ordinal)))
                .Select(i => i.Name)
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToArray());

        [Fact]
        public void OnlyAKeyNamePastTheIntRangeIsDeferred()
        {
            Assert.Equal(new[] { "corpus ToggleKey: value 1100 characters", "corpus YawModeKey: value 1100 characters" }, Deferred.Value);
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
            var deferred = new ConcurrentBag<string>();
            var created = new ConcurrentDictionary<string, byte[]>(StringComparer.Ordinal);
            byte[] committed = File.ReadAllBytes(ConfigTests.Committed());
            Parallel.ForEach(inputs, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, input =>
            {
                ImportOutcome import = ImportOutcome.Run(input);
                foreach (bool readOnly in input.Bytes == null ? new[] { false } : new[] { false, true })
                {
                    string name = input.Name + (readOnly ? " (read-only)" : "");
                    MigrationOutcome migration = MigrationOutcome.Run(input, defaultsIni, readOnly);

                    string imported = MigrationOutcome.Describe(import.Config);
                    string migrated = MigrationOutcome.Describe(migration.Config);
                    if (input.Bytes == null)
                    {
                        if (migration.Status != ConfigLoadStatus.Created) failures.Add(name + ": " + migration.Status);
                        if (migration.Created == null || !migration.Created.SequenceEqual(committed)) failures.Add(name + ": the created file is not config/CameraUnlock.ini");
                        // With no file the dev build ran on its defaults, which are the built-in values.
                        if (defaultsIni == null && imported != migrated) failures.Add(name + ":\n" + ComparisonOneTests.Diff(imported, migrated));
                        continue;
                    }

                    if (migration.Status == ConfigLoadStatus.Deferred)
                    {
                        if (!readOnly) deferred.Add(input.Name);
                        if (!migration.Reason.Contains("cannot be converted")) failures.Add(name + ": deferred: " + migration.Reason);
                    }
                    else if (migration.Status != ConfigLoadStatus.Migrated)
                    {
                        failures.Add(name + ": " + migration.Status + ": " + migration.Reason);
                        continue;
                    }
                    else
                    {
                        created[ComparisonOneTests.Sha256(migration.Created!)] = migration.Created!;
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
            Assert.Equal(Deferred.Value, deferred.OrderBy(n => n, StringComparer.Ordinal));
        }

        /// <summary>
        /// The map proof: on every input, what the converted mod runs on from the import is what the
        /// dev build ran on, apart from exactly the values the approved changes drop. Where that
        /// build's key parse threw, the import writes the name as it was beside the chord.
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

                SortedDictionary<string, string> before = oracle.Startup;
                SortedDictionary<string, string> after = ConvertedStartup.Of(import.Config);
                Assert.Equal(before.Keys, after.Keys);
                foreach (string key in before.Keys)
                {
                    if (key == "RotationSensitivity" || key == "RotationInversion") continue;
                    string expected = before[key];
                    if (expected.StartsWith("throws ", StringComparison.Ordinal))
                    {
                        expected = (key == "ToggleKey" ? old.ToggleKeyName + ", Ctrl+Shift+Y" : old.YawModeKeyName + ", Ctrl+Shift+H");
                    }
                    if (expected != after[key]) failures.Add(input.Name + ": " + key + " " + expected + " -> " + after[key]);
                }

                var expectedDrops = new List<string>();
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

        /// <summary>Every KeyCode a file can name converts to the key name that reads back as it.</summary>
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
            foreach (int code in codes.Where(c => c != 0))
            {
                string list = LegacyConfigImport.HotkeyList((KeyCode)code, KeyCode.H);
                KeyBinding[] bindings;
                string error;
                Assert.True(KeyBindings.TryParse(list, out bindings, out error), code + ": " + list + ": " + error);
                Assert.Equal(new KeyBinding(KeyModifiers.None, code), bindings[0]);
                Assert.Equal(new KeyBinding(KeyModifiers.Ctrl | KeyModifiers.Shift, (int)KeyCode.H), bindings[1]);
            }
            Assert.Equal("Ctrl+Shift+G", LegacyConfigImport.HotkeyList(KeyCode.None, KeyCode.G));
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
