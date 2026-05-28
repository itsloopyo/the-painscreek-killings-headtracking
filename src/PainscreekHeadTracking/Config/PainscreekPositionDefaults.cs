using CameraUnlock.Core.Data;

namespace PainscreekHeadTracking
{
    /// <summary>
    /// Painscreek-tuned position settings for PositionProcessor.
    /// Values are intentionally tighter than the shared Core defaults — Painscreek's
    /// interior geometry is cramped and full 0.30m / 0.40m lean ranges clip through
    /// walls and furniture. X and Z are inverted to match the tracker's axis
    /// convention against Painscreek's camera basis.
    /// </summary>
    internal static class PainscreekPositionDefaults
    {
        public static PositionSettings Build()
        {
            return new PositionSettings(
                0.5f, 0.3f, 0.5f,
                0.15f, 0.04f, 0.20f, 0.05f,
                0.15f,
                invertX: true, invertY: false, invertZ: true
            );
        }
    }
}
