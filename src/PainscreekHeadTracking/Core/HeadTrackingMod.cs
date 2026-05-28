using UnityEngine;

namespace PainscreekHeadTracking
{
    /// <summary>
    /// Simple mod that attaches CameraTrackingHook to the main camera.
    /// All actual work is done by StaticTracker which handles:
    /// - UDP receiving
    /// - Camera rotation
    /// - Moving the REAL game cursor via RectTransform
    /// - Hotkeys (Home=recenter, End=toggle)
    /// </summary>
    public sealed class HeadTrackingMod : MonoBehaviour
    {
        // Camera.main is implemented as FindGameObjectWithTag internally - avoid
        // calling it every frame. Re-poll only when our hook is gone (camera was
        // destroyed by a scene change) or every CameraRecheckInterval frames as a
        // safety net for games that swap cameras without destroying the old one.
        private const int CameraRecheckInterval = 30;

        public static HeadTrackingMod? Instance { get; private set; }

        private CameraTrackingHook? _cameraHook;
        private GameObject? _cachedCameraGameObject;
        private int _frameSinceLastRecheck;

        private void Awake()
        {
            Instance = this;
            ModLoader.Log("[Mod] HeadTrackingMod initialized - using StaticTracker");
        }

        private void LateUpdate()
        {
            // Fast path: hook is alive and was attached recently. Skip Camera.main
            // (~unity-tag-find cost) entirely on the vast majority of frames.
            if (!ReferenceEquals(_cameraHook, null)
                && _cameraHook != null
                && !ReferenceEquals(_cachedCameraGameObject, null)
                && _cachedCameraGameObject != null
                && ++_frameSinceLastRecheck < CameraRecheckInterval)
            {
                return;
            }
            _frameSinceLastRecheck = 0;

            Camera mainCamera = Camera.main;
            if (ReferenceEquals(mainCamera, null)) return;

            GameObject mainCameraGo = mainCamera.gameObject;
            if (ReferenceEquals(_cameraHook, null) || !ReferenceEquals(_cachedCameraGameObject, mainCameraGo))
            {
                if (!ReferenceEquals(_cameraHook, null))
                {
                    Destroy(_cameraHook);
                    _cameraHook = null;
                }

                _cameraHook = mainCameraGo.AddComponent<CameraTrackingHook>();
                _cachedCameraGameObject = mainCameraGo;
                ModLoader.Log("[Mod] Attached CameraTrackingHook to: " + mainCamera.name);
                CameraDiagnostics.DumpAttachedCamera(mainCameraGo, mainCamera);
            }
        }

        private void OnDestroy()
        {
            ModLoader.Log("[Mod] OnDestroy - scheduling recreate");
            if (!ReferenceEquals(_cameraHook, null))
            {
                Destroy(_cameraHook);
                _cameraHook = null;
            }
            _cachedCameraGameObject = null;
            Instance = null;
            ModLoader.ScheduleRecreate();
        }
    }
}
