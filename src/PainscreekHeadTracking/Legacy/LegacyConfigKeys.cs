using CameraUnlock.Core.Config;

namespace PainscreekHeadTracking.Legacy
{
    /// <summary>
    /// Every key <see cref="LegacyConfigReader"/> takes a value from, in each spelling its switch
    /// names. The reader ignores sections, so each is read wherever it sits in the file. It also
    /// strips '_' and '-' from a key before matching, so a spelling such as Yaw_Sensitivity is read
    /// too; the owner logs such a line as not carried. Smoothing and SmoothingFactor are left out:
    /// the reader only warns that they are retired and takes nothing from them. Frozen with it.
    /// </summary>
    internal static class LegacyConfigKeys
    {
        public static LegacyKey[] All()
        {
            return new[]
            {
                new LegacyKey("", "UdpPort"),
                new LegacyKey("", "Port"),
                new LegacyKey("", "EnableOnStartup"),
                new LegacyKey("", "Enabled"),
                new LegacyKey("", "YawSensitivity"),
                new LegacyKey("", "YawSens"),
                new LegacyKey("", "PitchSensitivity"),
                new LegacyKey("", "PitchSens"),
                new LegacyKey("", "RollSensitivity"),
                new LegacyKey("", "RollSens"),
                new LegacyKey("", "InvertYaw"),
                new LegacyKey("", "InvertPitch"),
                new LegacyKey("", "InvertRoll"),
                new LegacyKey("", "RecenterKey"),
                new LegacyKey("", "CenterKey"),
                new LegacyKey("", "ToggleKey"),
                new LegacyKey("", "YawModeKey"),
                new LegacyKey("", "WorldSpaceYaw"),
                new LegacyKey("", "HorizonLockedYaw"),
                new LegacyKey("", "AimDecoupling"),
                new LegacyKey("", "DecoupleAim"),
                new LegacyKey("", "AimDecouple"),
                new LegacyKey("", "ShowReticle"),
                new LegacyKey("", "ShowDecoupledReticle"),
                new LegacyKey("", "ShowCrosshair"),
                new LegacyKey("", "ReticleColor"),
                new LegacyKey("", "CrosshairColor"),
                new LegacyKey("", "LocalSmoothing"),
                new LegacyKey("", "RemoteSmoothing"),
            };
        }
    }
}
