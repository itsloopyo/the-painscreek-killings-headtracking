using System;
using UnityEngine;

namespace PainscreekHeadTracking
{
    /// <summary>
    /// Locates the active gameplay camera. Painscreek doesn't tag its camera
    /// "MainCamera", so Camera.main is often null and we have to fall back to
    /// scanning Camera.allCameras and picking the highest-depth one.
    /// </summary>
    internal static class CameraResolver
    {
        /// <summary>
        /// Finds the active gameplay camera, logging the discovery process.
        /// Returns null if no camera is currently in the scene.
        /// </summary>
        public static Camera? Resolve(Action<string> log)
        {
            Camera? mainCamera = Camera.main;
            if (mainCamera != null)
            {
                log($"Found camera: '{mainCamera.name}' depth={mainCamera.depth}");
                return mainCamera;
            }

            var allCameras = Camera.allCameras;
            log($"Camera.main is null, searching Camera.allCameras ({allCameras.Length} found)");
            foreach (var cam in allCameras)
            {
                log($"  Camera: '{cam.name}' depth={cam.depth}");
            }

            if (allCameras.Length == 0) return null;

            Camera best = allCameras[0];
            for (int i = 1; i < allCameras.Length; i++)
            {
                if (allCameras[i].depth > best.depth)
                {
                    best = allCameras[i];
                }
            }
            log($"Found camera: '{best.name}' depth={best.depth}");
            return best;
        }
    }
}
