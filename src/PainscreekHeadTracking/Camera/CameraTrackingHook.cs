using System;
using UnityEngine;

namespace PainscreekHeadTracking
{
    // ApplyTracking is driven by a Cecil-injected call at the end of
    // FirstPersonPlayerController.LateUpdate. That gives us a deterministic
    // execution point after the game has positioned the camera, and before
    // any camera's OnPreCull fires - which is what HxVolumetricCamera (and
    // TOD_Camera, etc) need to capture the head-tracked matrix instead of
    // the clean one.
    //
    // This MonoBehaviour exists solely to restore the camera transform in
    // OnPostRender, after all image effects have run. OnPostRender is a
    // camera-attached callback, so the script must live on the camera
    // GameObject - that's the only reason this component is here at all.
    public sealed class CameraTrackingHook : MonoBehaviour
    {
        // OnPostRender runs every frame, so a failure here repeats every frame. Logged
        // once per distinct message: unthrottled this wrote ~17 MB an hour at 60fps.
        private string? _lastLoggedError;

        private void OnPostRender()
        {
            try
            {
                StaticTracker.RestoreCamera();
            }
            catch (Exception ex)
            {
                if (ex.Message == _lastLoggedError) return;
                _lastLoggedError = ex.Message;
                ModLoader.Log("[CameraTrackingHook] OnPostRender error: " + ex.Message);
            }
        }
    }
}
