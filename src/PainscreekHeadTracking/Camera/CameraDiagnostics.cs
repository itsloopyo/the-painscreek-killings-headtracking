using System;
using System.Reflection;
using UnityEngine;

namespace PainscreekHeadTracking
{
    // Bright pixels smearing during head rotation point at a temporal image effect
    // (motion blur, sun shafts, temporal AA) reading our view-matrix delta as real
    // camera motion. We need the exact type names to disable the right one without
    // collateral damage. Fires per attach (which fires per scene change), because
    // gameplay scenes can carry effects that the menu/intro scene doesn't.
    internal static class CameraDiagnostics
    {
        public static void DumpAttachedCamera(GameObject cameraGo, Camera cam)
        {
            ModLoader.Log("[Diag] === Camera component dump (per-attach) ===");
            // Stub UnityEngine.dll only exposes clearFlags + cullingMask; the other props
            // (allowHDR, depthTextureMode, actualRenderingPath) come back via reflection
            // against the real runtime types so we don't have to regenerate the stub.
            ModLoader.Log("[Diag] camera settings: clearFlags=" + cam.clearFlags
                + " cullingMask=0x" + cam.cullingMask.ToString("X")
                + " " + ReflectCameraProps(cam));
            DumpComponents(cameraGo, "camera");

            Transform parent = cameraGo.transform.parent;
            if (!ReferenceEquals(parent, null) && parent != null)
            {
                DumpComponents(parent.gameObject, "camera.parent");
            }

            int childCount = cameraGo.transform.childCount;
            for (int i = 0; i < childCount; i++)
            {
                Transform child = cameraGo.transform.GetChild(i);
                DumpComponents(child.gameObject, "camera.child[" + i + "]");
            }
            ModLoader.Log("[Diag] === end component dump ===");
        }

        private static string ReflectCameraProps(Camera cam)
        {
            Type t = cam.GetType();
            string? allowHdr = ReadProp(cam, t, "allowHDR") ?? ReadProp(cam, t, "hdr");
            string? depthMode = ReadProp(cam, t, "depthTextureMode");
            string? renderPath = ReadProp(cam, t, "actualRenderingPath");
            return "allowHDR=" + (allowHdr ?? "?")
                + " depthTextureMode=" + (depthMode ?? "?")
                + " renderingPath=" + (renderPath ?? "?");
        }

        private static string? ReadProp(object obj, Type t, string name)
        {
            var p = t.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (p == null) return null;
            object? v = p.GetValue(obj, null);
            return v != null ? v.ToString() : "null";
        }

        private static void DumpComponents(GameObject go, string label)
        {
            ModLoader.Log("[Diag] " + label + ": '" + go.name + "' (active=" + go.activeInHierarchy + ")");
            var components = go.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                var c = components[i];
                if (ReferenceEquals(c, null)) continue;
                Type t = c.GetType();
                string enabledStr = c is Behaviour b ? (b.enabled ? "enabled" : "disabled") : "n/a";
                ModLoader.Log("  - " + t.FullName + "  [" + enabledStr + "]");
            }
        }
    }
}
