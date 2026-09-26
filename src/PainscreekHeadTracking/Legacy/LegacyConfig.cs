namespace PainscreekHeadTracking.Legacy
{
    /// <summary>
    /// The settings the dev pre-release (e4b385b) read from Painscreek_Data\Managed\HeadTracking.cfg
    /// through core's HeadTrackingConfigData at 34656598, with that commit's defaults. Frozen: a
    /// later change to core's types or constants must never change what an old file, or one missing
    /// a key, reads as, so the defaults are literals. 4242 was OpenTrackReceiver.DefaultPort, and
    /// 0.0 and 0.15 were SmoothingUtils.DefaultLocalSmoothing and DefaultRemoteSmoothing.
    /// </summary>
    internal sealed class LegacyConfig
    {
        public int UdpPort = 4242;
        public bool EnableOnStartup = true;

        public float YawSensitivity = 1.0f;
        public float PitchSensitivity = 1.0f;
        public float RollSensitivity = 1.0f;
        public bool InvertYaw = false;
        public bool InvertPitch = false;
        public bool InvertRoll = false;

        public string RecenterKeyName = "Home";
        public string ToggleKeyName = "End";
        public string YawModeKeyName = "PageDown";

        public bool WorldSpaceYaw = true;
        public bool AimDecouplingEnabled = true;
        public bool ShowDecoupledReticle = true;
        public float[] ReticleColorRgba = new float[] { 1f, 1f, 1f, 1f };

        public float LocalSmoothing = 0.0f;
        public float RemoteSmoothing = 0.15f;
    }
}
