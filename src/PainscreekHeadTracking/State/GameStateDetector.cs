using UnityEngine;
using UnityEngine.SceneManagement;

namespace PainscreekHeadTracking
{
    /// <summary>
    /// Detects when the player is in gameplay vs menus/pause to gate tracking.
    /// Uses Time.timeScale and scene name heuristics since Painscreek Killings
    /// doesn't expose a clean gameplay state API.
    /// </summary>
    public static class GameStateDetector
    {
        private const float CheckIntervalSeconds = 0.1f;

        private static float _lastCheckTime = float.NegativeInfinity;
        private static bool _isGameplay = true;

        // Scene name doesn't change between scene loads; cache the classification
        // (gameplay vs non-gameplay) so we don't allocate a lowered string and
        // run six Contains() calls every 100ms.
        private static string? _lastSceneName;
        private static bool _lastSceneIsNonGameplay;

        /// <summary>
        /// Whether the player is currently in active gameplay (not paused/menu).
        /// </summary>
        public static bool IsGameplay => _isGameplay;

        /// <summary>
        /// Check for state changes using a caller-supplied timestamp. The tracking loop
        /// already samples Time.realtimeSinceStartup once per frame; passing it in avoids
        /// a second native interop read on every gameplay frame.
        /// </summary>
        public static void Update(float now)
        {
            if (now - _lastCheckTime < CheckIntervalSeconds)
            {
                return;
            }
            _lastCheckTime = now;
            _isGameplay = DetectGameplay();
        }

        private static bool DetectGameplay()
        {
            // Paused: the game sets timeScale to 0 when paused or in menus
            if (Time.timeScale < 0.01f)
            {
                return false;
            }

            // Scene name only changes on scene load - keep the classification result
            // between checks instead of re-lowering and re-scanning every 100ms.
            string sceneName = SceneManager.GetActiveScene().name;
            if (string.IsNullOrEmpty(sceneName))
            {
                return false;
            }

            if (sceneName != _lastSceneName)
            {
                _lastSceneName = sceneName;
                _lastSceneIsNonGameplay = IsNonGameplayScene(sceneName);
            }

            return !_lastSceneIsNonGameplay;
        }

        private static bool IsNonGameplayScene(string sceneName)
        {
            string lower = sceneName.ToLowerInvariant();
            return lower.Contains("menu") || lower.Contains("title") ||
                   lower.Contains("intro") || lower.Contains("credit") ||
                   lower.Contains("load") || lower.Contains("splash");
        }
    }
}
