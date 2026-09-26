using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using CameraUnlock.Core.Config;
using PainscreekHeadTracking.Legacy;

namespace PainscreekHeadTracking.Tests.Differential
{
    /// <summary>The import on one input: the frozen reader, then the map.</summary>
    internal sealed class ImportOutcome
    {
        public ImportResult Result = ImportResult.Absent(new DroppedValue[0]);
        public PainscreekConfig Config = new PainscreekConfig();

        public static ImportOutcome Run(DifferentialInput input)
        {
            using (var folder = new LegacyFolder(input))
            {
                var config = new PainscreekConfig();
                ImportResult result = LegacyConfigImport.Run(new LegacyImportInput(folder.LegacyPath), config);
                return new ImportOutcome { Result = result, Config = config };
            }
        }
    }

    /// <summary>
    /// The migration on one input: the owner's Load in a folder holding only the legacy file, then
    /// a second Load over the same Defaults.ini. Every check asked of the files and the folder is
    /// made here, and a broken one throws.
    /// </summary>
    internal sealed class MigrationOutcome
    {
        public ConfigLoadStatus Status;
        public PainscreekConfig Config = new PainscreekConfig();
        public byte[]? Created;
        public string Reason = "";
        public IList<string> Log = new List<string>();

        /// <param name="defaultsIni">What Defaults.ini holds before the load, or null for none, so
        /// the owner creates it with the built-in values.</param>
        public static MigrationOutcome Run(DifferentialInput input, string? defaultsIni, bool readOnly)
        {
            string defaultsDir = Path.Combine(Path.GetTempPath(), "painscreek-defaults-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(defaultsDir);
            try
            {
                string defaultsPath = Path.Combine(defaultsDir, "Defaults.ini");
                if (defaultsIni != null) File.WriteAllText(defaultsPath, defaultsIni, Encoding.ASCII);
                using (var folder = new LegacyFolder(input))
                {
                    return Run(input, folder, DefaultsFile.At(defaultsPath), readOnly);
                }
            }
            finally
            {
                Directory.Delete(defaultsDir, true);
            }
        }

        private static MigrationOutcome Run(DifferentialInput input, LegacyFolder folder, DefaultsFile defaults, bool readOnly)
        {
            string configPath = Path.Combine(folder.Path, PainscreekConfig.FileName);
            DateTime written = DateTime.MinValue;
            if (input.Bytes != null)
            {
                if (readOnly) File.SetAttributes(folder.LegacyPath, FileAttributes.ReadOnly);
                written = File.GetLastWriteTimeUtc(folder.LegacyPath);
            }

            ConfigLoadResult<PainscreekConfig> loaded = Owner(folder, defaults).Load();
            var outcome = new MigrationOutcome
            {
                Status = loaded.Status,
                Config = loaded.Config,
                Reason = loaded.Reason,
                Log = loaded.Log,
            };
            CheckLegacyFile(input, folder, written, readOnly);

            string[] expected;
            if (loaded.Status == ConfigLoadStatus.Migrated || loaded.Status == ConfigLoadStatus.Created)
            {
                expected = input.Bytes == null ? new[] { PainscreekConfig.FileName } : new[] { PainscreekConfig.FileName, Inputs.LegacyName };
                outcome.Created = File.ReadAllBytes(configPath);
            }
            else
            {
                expected = input.Bytes == null ? new string[0] : new[] { Inputs.LegacyName };
            }
            if (!expected.SequenceEqual(folder.Entries()))
                throw new InvalidOperationException(input.Name + ": " + loaded.Status + " left " + string.Join(", ", folder.Entries()));

            if (outcome.Created != null)
            {
                DateTime createdAt = File.GetLastWriteTimeUtc(configPath);
                ConfigLoadResult<PainscreekConfig> second = Owner(folder, defaults).Load();
                if (second.Status != ConfigLoadStatus.Canonical)
                    throw new InvalidOperationException(input.Name + ": the second load is " + second.Status);
                if (Describe(second.Config) != Describe(loaded.Config))
                    throw new InvalidOperationException(input.Name + ": the second load reads another config:\n" + Describe(second.Config));
                if (input.Bytes != null && !second.Log.Any(l => l.Contains("settings are read from this file") && l.Contains("is not read")))
                    throw new InvalidOperationException(input.Name + ": the second load does not say the legacy file is not read");
                if (!File.ReadAllBytes(configPath).SequenceEqual(outcome.Created) || File.GetLastWriteTimeUtc(configPath) != createdAt)
                    throw new InvalidOperationException(input.Name + ": the second load rewrote CameraUnlock.ini");
                CheckLegacyFile(input, folder, written, readOnly);
            }
            return outcome;
        }

        private static void CheckLegacyFile(DifferentialInput input, LegacyFolder folder, DateTime written, bool readOnly)
        {
            if (input.Bytes == null) return;
            if (!File.ReadAllBytes(folder.LegacyPath).SequenceEqual(input.Bytes))
                throw new InvalidOperationException(input.Name + ": the legacy file's bytes changed");
            if (File.GetLastWriteTimeUtc(folder.LegacyPath) != written)
                throw new InvalidOperationException(input.Name + ": the legacy file's write time changed");
            bool isReadOnly = (File.GetAttributes(folder.LegacyPath) & FileAttributes.ReadOnly) != 0;
            if (isReadOnly != readOnly)
                throw new InvalidOperationException(input.Name + ": the legacy file's read-only attribute changed");
        }

        private static ConfigOwner<PainscreekConfig> Owner(LegacyFolder folder, DefaultsFile defaults)
        {
            return new ConfigOwner<PainscreekConfig>(PainscreekConfig.OwnerOptions(folder.Path, defaults));
        }

        /// <summary>Every setting a row of the table holds, floats with their bits.</summary>
        public static string Describe(PainscreekConfig c)
        {
            var s = new StringBuilder();
            Action<string, string> line = (name, value) => s.Append(name).Append('=').Append(value).Append('\n');
            line("UdpPort", c.UdpPort.ToString(CultureInfo.InvariantCulture));
            line("EnableOnStartup", LegacyStartup.Text(c.EnableOnStartup));
            line("WorldSpaceYaw", LegacyStartup.Text(c.WorldSpaceYaw));
            line("RotationEnabled", LegacyStartup.Text(c.RotationEnabled));
            line("PositionEnabled", LegacyStartup.Text(c.PositionEnabled));
            line("LocalSmoothing", LegacyStartup.Text(c.LocalSmoothing));
            line("RemoteSmoothing", LegacyStartup.Text(c.RemoteSmoothing));
            line("Position.LocalSmoothing", LegacyStartup.Text(c.Position.LocalSmoothing));
            line("Position.RemoteSmoothing", LegacyStartup.Text(c.Position.RemoteSmoothing));
            line("ToggleKey", c.ToggleKeyName);
            line("CycleTrackingModeKey", c.CycleTrackingModeKeyName);
            line("YawModeKey", c.YawModeKeyName);
            return s.ToString();
        }
    }

    /// <summary>
    /// What the converted mod sets up from its settings, in the same terms as
    /// <see cref="LegacyStartup"/>: rotation sensitivity is identity with no axis flips, in code now.
    /// </summary>
    internal static class ConvertedStartup
    {
        public static SortedDictionary<string, string> Of(PainscreekConfig c)
        {
            var s = new SortedDictionary<string, string>(StringComparer.Ordinal);
            string one = LegacyStartup.Text(LegacyConfigImport.ShippedSensitivity);
            string flip = LegacyStartup.Text(LegacyConfigImport.ShippedInvert);
            s["TrackingEnabled"] = LegacyStartup.Text(c.EnableOnStartup);
            s["RotationEnabled"] = LegacyStartup.Text(c.RotationEnabled);
            s["PositionEnabled"] = LegacyStartup.Text(c.PositionEnabled);
            s["UdpPort"] = c.UdpPort.ToString(CultureInfo.InvariantCulture);
            s["WorldSpaceYaw"] = LegacyStartup.Text(c.WorldSpaceYaw);
            s["LocalSmoothing"] = LegacyStartup.Text(c.LocalSmoothing);
            s["RemoteSmoothing"] = LegacyStartup.Text(c.RemoteSmoothing);
            s["RotationSensitivity"] = one + " " + one + " " + one;
            s["RotationInversion"] = flip + " " + flip + " " + flip;
            s["ToggleKey"] = c.ToggleKeyName;
            s["CycleTrackingModeKey"] = c.CycleTrackingModeKeyName;
            s["YawModeKey"] = c.YawModeKeyName;
            return s;
        }
    }
}
