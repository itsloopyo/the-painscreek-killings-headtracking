using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace PainscreekHeadTracking.Tests.Differential
{
    [CollectionDefinition(Name, DisableParallelization = true)]
    public class DifferentialCollection
    {
        public const string Name = "config differential";
    }

    /// <summary>
    /// Comparison 1: the dev pre-release's reader (oracle/, core's files at 34656598 byte for byte,
    /// and its StaticTracker's key parse) against the frozen reader in
    /// src/PainscreekHeadTracking/Legacy, on every input: the settings, whether a file was found,
    /// the startup state and every log line. A difference here is something players would see
    /// change that the conversion did not cause, and each one is listed with the commit that made
    /// it. There are none.
    /// </summary>
    [Collection(DifferentialCollection.Name)]
    public class ComparisonOneTests
    {
        [Fact]
        public void TheFrozenReaderReadsEveryInputAsTheDevBuildDid()
        {
            List<DifferentialInput> inputs = Inputs.All().ToList();
            Assert.True(inputs.Count > 1000, "the corpus gave only " + inputs.Count + " inputs");
            var failures = new List<string>();
            foreach (DifferentialInput input in inputs)
            {
                string oracle = Oracle.Run(input).Describe();
                string frozen = FrozenReader.Run(input).Describe();
                if (oracle != frozen) failures.Add(input.Name + ":\n" + Diff(oracle, frozen));
            }
            Assert.True(failures.Count == 0, string.Join("\n", failures.Take(20)));
        }

        /// <summary>
        /// The retired-key warning is logged on every run over a file that holds the key, the
        /// second oracle run included, so the comparison above sees it on every such input.
        /// </summary>
        [Fact]
        public void TheRetiredSmoothingWarningIsLoggedOnEveryRun()
        {
            DifferentialInput old = Inputs.Examples().Single(i => i.Name == "README example 99d6e86");
            Assert.Contains(Oracle.Run(old).Log, l => l.Contains("'Smoothing' has been retired"));
            Assert.Contains(FrozenReader.Run(old).Log, l => l.Contains("'Smoothing' has been retired"));
            Assert.Contains(Oracle.Run(old).Log, l => l.Contains("'Smoothing' has been retired"));
        }

        [Fact]
        public void EveryRecordedFileHoldsTheBytesItsProvenanceNames()
        {
            string root = Inputs.RepoRoot();
            int checkedFiles = 0;
            foreach (string line in File.ReadAllLines(Path.Combine(Inputs.DifferentialDir(), "provenance.tsv")))
            {
                if (line.Length == 0 || line[0] == '#') continue;
                string[] fields = line.Split('\t');
                Assert.Equal(3, fields.Length);
                string path = fields[1];
                if (path.Contains(":") || path.Contains(" ")) continue;
                string full = Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar));
                Assert.True(File.Exists(full), path + " is missing");
                Assert.Equal(fields[2], Sha256(File.ReadAllBytes(full)));
                checkedFiles++;
            }
            Assert.True(checkedFiles >= 10, "provenance.tsv names only " + checkedFiles + " repo files");
        }

        internal static string Sha256(byte[] bytes)
        {
            using (SHA256 sha = SHA256.Create())
            {
                var text = new StringBuilder();
                foreach (byte b in sha.ComputeHash(bytes)) text.Append(b.ToString("x2"));
                return text.ToString();
            }
        }

        internal static string Diff(string expected, string actual)
        {
            string[] e = expected.Split('\n');
            string[] a = actual.Split('\n');
            var lines = new List<string>();
            for (int i = 0; i < Math.Max(e.Length, a.Length); i++)
            {
                string x = i < e.Length ? e[i] : "(none)";
                string y = i < a.Length ? a[i] : "(none)";
                if (x != y) lines.Add("  expected " + x + " | got " + y);
            }
            return string.Join("\n", lines);
        }
    }
}
