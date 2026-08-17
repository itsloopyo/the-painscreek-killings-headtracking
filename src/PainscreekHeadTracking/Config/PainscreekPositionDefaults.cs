using CameraUnlock.Core.Data;

namespace PainscreekHeadTracking
{
    /// <summary>
    /// Painscreek-tuned position settings for PositionProcessor.
    /// Values are intentionally tighter than the shared Core defaults - Painscreek's
    /// interior geometry is cramped and full 0.30m / 0.40m lean ranges clip through
    /// walls and furniture. X and Z are inverted to match the tracker's axis
    /// convention against Painscreek's camera basis.
    /// Smoothing is the user's configured pair; position uses the same values as
    /// rotation and the processor selects between them per connection.
    /// </summary>
    internal static class PainscreekPositionDefaults
    {
        public static PositionSettings Build(float localSmoothing, float remoteSmoothing)
        {
            return PositionSettings.Symmetric(
                0.5f, 0.3f, 0.5f,
                0.15f, 0.04f, 0.20f, 0.05f,
                localSmoothing, remoteSmoothing,
                invertX: true, invertY: false, invertZ: true
            );
        }
    }
}
