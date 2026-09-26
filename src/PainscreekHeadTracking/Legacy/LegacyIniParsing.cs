using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace PainscreekHeadTracking.Legacy
{
    /// <summary>
    /// The parts of core's ConfigParsingUtils that <see cref="LegacyConfigReader"/> calls, copied
    /// from cameraunlock-core 34656598, the commit the dev pre-release (e4b385b) pinned, with
    /// MathUtils.Clamp01 from the same commit. Frozen: core has since changed ParseIniFile (a key
    /// set twice now throws, and the comment and quote tests are ordinal), and the import must read
    /// a player's file as that build did.
    /// </summary>
    internal static class LegacyIniParsing
    {
        public static bool TryParseColor(string value, out float[] rgba)
        {
            rgba = new float[] { 1f, 1f, 1f, 1f };

            if (string.IsNullOrEmpty(value))
                return false;

            string[] parts = value.Split(',');
            if (parts.Length < 3)
                return false;

            float r, g, b, a = 1f;
            if (!TryParseFloat(parts[0], out r) ||
                !TryParseFloat(parts[1], out g) ||
                !TryParseFloat(parts[2], out b))
                return false;

            if (parts.Length >= 4 && !TryParseFloat(parts[3], out a))
                return false;

            // 0-255 input is detected from the largest RGB channel, alpha on its own.
            float maxRgb = r;
            if (g > maxRgb) maxRgb = g;
            if (b > maxRgb) maxRgb = b;
            if (maxRgb > 1f)
            {
                r /= 255f;
                g /= 255f;
                b /= 255f;
            }
            if (a > 1f) a /= 255f;

            rgba[0] = Clamp01(r);
            rgba[1] = Clamp01(g);
            rgba[2] = Clamp01(b);
            rgba[3] = Clamp01(a);
            return true;
        }

        public static bool TryParseFloat(string value, out float result)
        {
            if (string.IsNullOrEmpty(value))
            {
                result = 0f;
                return false;
            }
            if (!float.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out result))
            {
                return false;
            }
            if (float.IsNaN(result) || float.IsInfinity(result))
            {
                result = 0f;
                return false;
            }
            return true;
        }

        public static bool TryParseInt(string value, out int result)
        {
            if (string.IsNullOrEmpty(value))
            {
                result = 0;
                return false;
            }
            return int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out result);
        }

        public static bool TryParseBool(string value, out bool result)
        {
            result = false;
            if (string.IsNullOrEmpty(value))
                return false;

            string trimmed = value.Trim().ToLowerInvariant();
            if (trimmed == "true" || trimmed == "yes" || trimmed == "1")
            {
                result = true;
                return true;
            }
            if (trimmed == "false" || trimmed == "no" || trimmed == "0")
            {
                result = false;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Keys compare case-insensitively and a later line wins. The StartsWith calls are the
        /// culture-sensitive overload the dev build ran, which skips an ignorable leading character.
        /// </summary>
        public static Dictionary<string, string> ParseIniFile(string filePath)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (!File.Exists(filePath))
                return result;

            foreach (string line in File.ReadAllLines(filePath))
            {
                string trimmed = line.Trim();

                if (string.IsNullOrEmpty(trimmed) ||
                    trimmed.StartsWith("#") ||
                    trimmed.StartsWith(";") ||
                    trimmed.StartsWith("["))
                    continue;

                int eqIndex = trimmed.IndexOf('=');
                if (eqIndex <= 0)
                    continue;

                string key = trimmed.Substring(0, eqIndex).Trim();
                string value = trimmed.Substring(eqIndex + 1).Trim();

                value = StripInlineComment(value);

                if (value.Length >= 2 &&
                    ((value.StartsWith("\"") && value.EndsWith("\"")) ||
                     (value.StartsWith("'") && value.EndsWith("'"))))
                {
                    value = value.Substring(1, value.Length - 2);
                }

                result[key] = value;
            }

            return result;
        }

        // Truncates at the first ';' or '#' that is not inside a quoted section.
        private static string StripInlineComment(string value)
        {
            bool inQuotes = false;
            char quote = '\0';
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (inQuotes)
                {
                    if (c == quote) inQuotes = false;
                    continue;
                }
                if (c == '"' || c == '\'')
                {
                    inQuotes = true;
                    quote = c;
                    continue;
                }
                if (c == ';' || c == '#')
                {
                    return value.Substring(0, i).TrimEnd();
                }
            }
            return value;
        }

        private static float Clamp01(float value)
        {
            if (value < 0f) return 0f;
            if (value > 1f) return 1f;
            return value;
        }
    }
}
