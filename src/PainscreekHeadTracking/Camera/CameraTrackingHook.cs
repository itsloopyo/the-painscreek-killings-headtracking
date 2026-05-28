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
        private void OnPostRender()
        {
            try
            {
                StaticTracker.RestoreCamera();
            }
            catch (Exception ex)
            {
                ModLoader.Log("[CameraTrackingHook] OnPostRender error: " + ex.Message);
            }
        }
    }
}
