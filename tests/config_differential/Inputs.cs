using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CameraUnlock.Core.Config.Testing;
using PainscreekHeadTracking.Legacy;

namespace PainscreekHeadTracking.Tests.Differential
{
    /// <summary>One differential input: a legacy file's bytes, or no file at all.</summary>
    internal sealed class DifferentialInput
    {
        public DifferentialInput(string name, byte[]? bytes)
        {
            Name = name;
            Bytes = bytes;
        }

        public string Name { get; }

        /// <summary>Null for no file.</summary>
        public byte[]? Bytes { get; }
    }

    internal static class Inputs
    {
        public const string LegacyName = "HeadTracking.cfg";

        public static string RepoRoot()
        {
            DirectoryInfo? dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "pixi.toml"))) dir = dir.Parent;
            if (dir == null) throw new InvalidOperationException("no pixi.toml above " + AppDomain.CurrentDomain.BaseDirectory);
            return dir.FullName;
        }

        public static string DifferentialDir()
        {
            return Path.Combine(Path.Combine(RepoRoot(), "tests"), "config_differential");
        }

        public static string ExampleDir()
        {
            return Path.Combine(Path.Combine(DifferentialDir(), "data"), "readme-example");
        }

        /// <summary>
        /// The README's example as the dev pre-release documented it, the base the corpus mutates.
        /// </summary>
        public static byte[] NewestExample()
        {
            return File.ReadAllBytes(Path.Combine(ExampleDir(), "795a88c.cfg"));
        }

        /// <summary>
        /// No build shipped HeadTracking.cfg, seeded it or wrote one at its first start: the file
        /// exists only where a player made it, and the README told them what to write. These are
        /// the three versions of that example up to the dev pre-release.
        /// </summary>
        public static IEnumerable<DifferentialInput> Examples()
        {
            string[] files = Directory.GetFiles(ExampleDir(), "*.cfg");
            Array.Sort(files, StringComparer.Ordinal);
            if (files.Length != 3) throw new InvalidOperationException(ExampleDir() + " holds " + files.Length + " examples, not 3");
            foreach (string file in files)
            {
                yield return new DifferentialInput("README example " + Path.GetFileNameWithoutExtension(file), File.ReadAllBytes(file));
            }
        }

        public static IEnumerable<DifferentialInput> Corpus()
        {
            foreach (IniMutation m in IniMutations.Generate(NewestExample(), LegacyConfigKeys.All(), Descriptors()))
            {
                yield return new DifferentialInput("corpus " + m.Name, m.Bytes);
            }
        }

        public static IEnumerable<DifferentialInput> All()
        {
            yield return new DifferentialInput("no file", null);
            yield return new DifferentialInput("empty file", new byte[0]);
            foreach (DifferentialInput input in Examples()) yield return input;
            foreach (DifferentialInput input in Corpus()) yield return input;
        }

        /// <summary>
        /// A descriptor per key the reader reads, in the order of <see cref="LegacyConfigKeys"/>:
        /// another valid value, and values past the ends of the ranges the reader refuses (the port)
        /// or clamps (the smoothing pair, the colour).
        /// </summary>
        public static List<MutationKey> Descriptors()
        {
            var none = new string[0];
            var noChords = new ChordSwitch[0];
            Func<string, string, string[], MutationKey> plain =
                (key, alternate, outOfRange) => new MutationKey("", key, alternate, outOfRange, false, noChords);
            Func<string, string, MutationKey> hotkey =
                (key, alternate) => new MutationKey("", key, alternate, none, true, noChords);
            string[] port = { "0", "70000" };
            string[] unit = { "-0.1", "1.5" };
            string[] color = { "255,128,0", "0.5,0.5,0.5,-1" };
            return new List<MutationKey>
            {
                plain("UdpPort", "4343", port),
                plain("Port", "4343", port),
                plain("EnableOnStartup", "false", none),
                plain("Enabled", "false", none),
                plain("YawSensitivity", "1.5", none),
                plain("YawSens", "1.5", none),
                plain("PitchSensitivity", "1.5", none),
                plain("PitchSens", "1.5", none),
                plain("RollSensitivity", "0.5", none),
                plain("RollSens", "0.5", none),
                plain("InvertYaw", "true", none),
                plain("InvertPitch", "true", none),
                plain("InvertRoll", "true", none),
                hotkey("RecenterKey", "F5"),
                hotkey("CenterKey", "F5"),
                hotkey("ToggleKey", "F9"),
                hotkey("YawModeKey", "F11"),
                plain("WorldSpaceYaw", "false", none),
                plain("HorizonLockedYaw", "false", none),
                plain("AimDecoupling", "false", none),
                plain("DecoupleAim", "false", none),
                plain("AimDecouple", "false", none),
                plain("ShowReticle", "false", none),
                plain("ShowDecoupledReticle", "false", none),
                plain("ShowCrosshair", "false", none),
                plain("ReticleColor", "1.0,0.0,0.0,1.0", color),
                plain("CrosshairColor", "1.0,0.0,0.0,1.0", color),
                plain("LocalSmoothing", "0.3", unit),
                plain("RemoteSmoothing", "0.5", unit),
            };
        }
    }

    /// <summary>A scratch folder holding at most the legacy file, deleted on dispose.</summary>
    internal sealed class LegacyFolder : IDisposable
    {
        public LegacyFolder(DifferentialInput input)
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "painscreek-diff-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
            LegacyPath = System.IO.Path.Combine(Path, Inputs.LegacyName);
            if (input.Bytes != null) File.WriteAllBytes(LegacyPath, input.Bytes);
        }

        public string Path { get; }

        public string LegacyPath { get; }

        public string[] Entries()
        {
            string[] names = Directory.GetFileSystemEntries(Path).Select(System.IO.Path.GetFileName).ToArray();
            Array.Sort(names, StringComparer.Ordinal);
            return names;
        }

        public void Dispose()
        {
            foreach (string file in Directory.GetFiles(Path)) File.SetAttributes(file, FileAttributes.Normal);
            Directory.Delete(Path, true);
        }
    }
}
