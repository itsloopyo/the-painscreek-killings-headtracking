using System;
using System.Reflection;
using UnityEngine;

namespace PainscreekHeadTracking
{
    // Scion's DoF temporal supersampling blends 0.9 of the previous frame's
    // output (sampled at the previous-frame screen position) with 0.1 of this
    // frame's. With head tracking applied via the static Camera.onPreCull event,
    // Scion now captures previousViewProjection (in its OnRenderImage, post scene
    // render) against the same head-tracked matrix the scene was rendered with,
    // so frame-over-frame the delta is just the head-movement delta - small, and
    // the reprojection should be correct. The suppression here is kept defensive:
    // the property toggle is a single field write, removing it costs DoF clarity
    // if the reprojection is ever subtly off.
    internal static class PostProcessSuppressor
    {
        private const string ScionTypeName = "ScionEngine.ScionPostProcessNoTonemap";
        private const string ScionTemporalProperty = "depthOfFieldTemporalSupersampling";

        private static Component? _scion;
        private static PropertyInfo? _scionTemporalProperty;
        private static bool _scionOriginalValue;
        private static bool _suppressed;

        public static void Apply(Camera mainCamera, Action<string> log)
        {
            if (_suppressed) return;
            _suppressed = true;

            if (_scion == null || _scionTemporalProperty == null)
            {
                if (!ResolveScion(mainCamera, log)) return;
            }

            _scionOriginalValue = (bool)_scionTemporalProperty!.GetValue(_scion, null);
            if (_scionOriginalValue)
            {
                _scionTemporalProperty.SetValue(_scion, false, null);
                log("PostProcessSuppressor: disabled Scion DoF temporal supersampling");
            }
        }

        public static void Restore(Action<string> log)
        {
            if (!_suppressed) return;
            _suppressed = false;

            if (_scion == null || _scionTemporalProperty == null) return;
            _scionTemporalProperty.SetValue(_scion, _scionOriginalValue, null);
            log("PostProcessSuppressor: restored Scion DoF temporal supersampling");
        }

        private static bool ResolveScion(Camera mainCamera, Action<string> log)
        {
            var components = mainCamera.gameObject.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                var c = components[i];
                if (ReferenceEquals(c, null)) continue;
                if (c.GetType().FullName == ScionTypeName)
                {
                    _scion = c;
                    break;
                }
            }

            if (_scion == null)
            {
                log("PostProcessSuppressor: " + ScionTypeName + " not found on " + mainCamera.name + " - skipping");
                return false;
            }

            _scionTemporalProperty = _scion.GetType().GetProperty(
                ScionTemporalProperty,
                BindingFlags.Public | BindingFlags.Instance);

            if (_scionTemporalProperty == null)
            {
                log("PostProcessSuppressor: property '" + ScionTemporalProperty + "' missing on Scion type - asset version mismatch");
                _scion = null;
                return false;
            }

            return true;
        }
    }
}
