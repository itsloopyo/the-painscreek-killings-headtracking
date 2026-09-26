using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace PainscreekHeadTracking.Legacy
{
    /// <summary>
    /// HeadTrackingConfigData.LoadFromFile and ApplyValues as cameraunlock-core 34656598 shipped
    /// them in the dev pre-release (e4b385b), frozen. It fills <see cref="LegacyConfig"/> instead of
    /// core's type and writes nothing, as that build wrote nothing either. The parse helpers are
    /// <see cref="LegacyIniParsing"/>, core's as that build pinned them. The log lines are that
    /// build's.
    /// </summary>
    internal static class LegacyConfigReader
    {
        // Warned once per process, as the published build did.
        private static bool _warnedRetiredSmoothingKey;

        /// <param name="filePath">The legacy file.</param>
        /// <param name="log">Where the published build wrote its config lines, or null.</param>
        /// <param name="found">false when there is no file, which the published build answered
        /// with its defaults.</param>
        /// <param name="loadError">The message of the exception the published build caught while
        /// reading, after which it ran on its defaults; null when the read finished.</param>
        public static LegacyConfig Read(string filePath, Action<string>? log, out bool found, out string? loadError)
        {
            var config = new LegacyConfig();
            found = File.Exists(filePath);
            loadError = null;

            try
            {
                var values = LegacyIniParsing.ParseIniFile(filePath);
                if (values.Count == 0)
                {
                    log?.Invoke("No config file found, using defaults");
                    return config;
                }

                ApplyValues(config, values, log);
                log?.Invoke("Config loaded successfully");
            }
            catch (Exception ex)
            {
                loadError = ex.Message;
                log?.Invoke(string.Format("Config load error (using defaults): {0}", ex.Message));
            }

            return config;
        }

        private static void ApplyValues(LegacyConfig config, Dictionary<string, string> values, Action<string>? log)
        {
            float yawSens = config.YawSensitivity;
            float pitchSens = config.PitchSensitivity;
            float rollSens = config.RollSensitivity;
            bool invertYaw = config.InvertYaw;
            bool invertPitch = config.InvertPitch;
            bool invertRoll = config.InvertRoll;

            foreach (var kvp in values)
            {
                string key = kvp.Key.ToLowerInvariant().Replace("_", "").Replace("-", "");
                string value = kvp.Value;

                int intVal;
                float floatVal;
                bool boolVal;

                switch (key)
                {
                    case "udpport":
                    case "port":
                        if (LegacyIniParsing.TryParseInt(value, out intVal))
                        {
                            if (intVal >= 1 && intVal <= 65535)
                            {
                                config.UdpPort = intVal;
                            }
                            else
                            {
                                log?.Invoke(string.Format(
                                    "Config key '{0}' has an out-of-range value '{1}' (expected 1-65535) - using {2}",
                                    key, value, config.UdpPort.ToString(CultureInfo.InvariantCulture)));
                            }
                        }
                        break;

                    case "enableonstartup":
                    case "enabled":
                        if (LegacyIniParsing.TryParseBool(value, out boolVal))
                            config.EnableOnStartup = boolVal;
                        break;

                    case "yawsensitivity":
                    case "yawsens":
                        if (LegacyIniParsing.TryParseFloat(value, out floatVal))
                            yawSens = floatVal;
                        break;

                    case "pitchsensitivity":
                    case "pitchsens":
                        if (LegacyIniParsing.TryParseFloat(value, out floatVal))
                            pitchSens = floatVal;
                        break;

                    case "rollsensitivity":
                    case "rollsens":
                        if (LegacyIniParsing.TryParseFloat(value, out floatVal))
                            rollSens = floatVal;
                        break;

                    case "invertyaw":
                        if (LegacyIniParsing.TryParseBool(value, out boolVal))
                            invertYaw = boolVal;
                        break;

                    case "invertpitch":
                        if (LegacyIniParsing.TryParseBool(value, out boolVal))
                            invertPitch = boolVal;
                        break;

                    case "invertroll":
                        if (LegacyIniParsing.TryParseBool(value, out boolVal))
                            invertRoll = boolVal;
                        break;

                    case "recenterkey":
                    case "centerkey":
                        config.RecenterKeyName = value;
                        break;

                    case "togglekey":
                        config.ToggleKeyName = value;
                        break;

                    case "yawmodekey":
                        config.YawModeKeyName = value;
                        break;

                    case "worldspaceyaw":
                    case "horizonlockedyaw":
                        if (LegacyIniParsing.TryParseBool(value, out boolVal))
                            config.WorldSpaceYaw = boolVal;
                        break;

                    case "aimdecoupling":
                    case "decoupleaim":
                    case "aimdecouple":
                        if (LegacyIniParsing.TryParseBool(value, out boolVal))
                            config.AimDecouplingEnabled = boolVal;
                        break;

                    case "showreticle":
                    case "showdecoupledreticle":
                    case "showcrosshair":
                        if (LegacyIniParsing.TryParseBool(value, out boolVal))
                            config.ShowDecoupledReticle = boolVal;
                        break;

                    case "reticlecolor":
                    case "crosshaircolor":
                        float[] color;
                        if (LegacyIniParsing.TryParseColor(value, out color))
                            config.ReticleColorRgba = color;
                        break;

                    case "localsmoothing":
                        if (LegacyIniParsing.TryParseFloat(value, out floatVal))
                        {
                            config.LocalSmoothing = Clamp01(floatVal);
                        }
                        else
                        {
                            config.LocalSmoothing = 0.0f;
                            WarnUnusable(log, "LocalSmoothing", value, config.LocalSmoothing);
                        }
                        break;

                    case "remotesmoothing":
                        if (LegacyIniParsing.TryParseFloat(value, out floatVal))
                        {
                            config.RemoteSmoothing = Clamp01(floatVal);
                        }
                        else
                        {
                            config.RemoteSmoothing = 0.15f;
                            WarnUnusable(log, "RemoteSmoothing", value, config.RemoteSmoothing);
                        }
                        break;

                    case "smoothing":
                    case "smoothingfactor":
                        WarnRetiredSmoothingKey(log, kvp.Key);
                        break;
                }
            }

            config.YawSensitivity = yawSens;
            config.PitchSensitivity = pitchSens;
            config.RollSensitivity = rollSens;
            config.InvertYaw = invertYaw;
            config.InvertPitch = invertPitch;
            config.InvertRoll = invertRoll;
        }

        private static void WarnUnusable(Action<string>? log, string key, string value, float fallback)
        {
            log?.Invoke(string.Format(
                "Config key '{0}' has an unusable value '{1}' (not a finite number) - using {2}",
                key, value, fallback.ToString(CultureInfo.InvariantCulture)));
        }

        private static void WarnRetiredSmoothingKey(Action<string>? log, string key)
        {
            if (_warnedRetiredSmoothingKey) return;
            _warnedRetiredSmoothingKey = true;

            log?.Invoke(string.Format(
                "Config key '{0}' has been retired and is IGNORED. Smoothing is now two keys: " +
                "LocalSmoothing (default {1}, applies to a tracker on this machine) and " +
                "RemoteSmoothing (default {2}, applies to a tracker on the network). The old " +
                "value is not migrated because the semantics changed - it carried a hidden " +
                "{2} floor that no longer exists. Set the two new keys.",
                key,
                0.0f.ToString(CultureInfo.InvariantCulture),
                0.15f.ToString(CultureInfo.InvariantCulture)));
        }

        private static float Clamp01(float value)
        {
            if (value < 0f) return 0f;
            if (value > 1f) return 1f;
            return value;
        }
    }
}
