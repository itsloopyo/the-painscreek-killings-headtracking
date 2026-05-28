using System;
using System.IO;
using UnityEngine;

namespace PainscreekHeadTracking
{
    /// <summary>
    /// Static loader called from patched Assembly-CSharp.dll.
    /// Creates the HeadTracking mod GameObject on first call.
    /// Auto-recreates if destroyed.
    /// </summary>
    public static class ModLoader
    {
        private static bool _initialized;
        private static bool _needsRecreate;
        private static ModRecreator? _recreator;

        /// <summary>
        /// Called from patched Assembly-CSharp.dll entry point.
        /// </summary>
        public static void Initialize()
        {
            // VERY FIRST THING: Setup logging before ANYTHING else
            Logger.Initialize();
            Log("=== ModLoader.Initialize() ENTRY ===");

            try
            {
                // Log assembly info
                var thisAsm = typeof(ModLoader).Assembly;
                Log($"PainscreekHeadTracking.dll loaded from: {thisAsm.Location}");

                // Check for CameraUnlock.Core.dll
                string? asmDir = Path.GetDirectoryName(thisAsm.Location);
                if (asmDir != null)
                {
                    string coreDllPath = Path.Combine(asmDir, "CameraUnlock.Core.dll");
                    Log($"Looking for CameraUnlock.Core.dll at: {coreDllPath}");
                    Log($"CameraUnlock.Core.dll exists: {File.Exists(coreDllPath)}");

                    if (!File.Exists(coreDllPath))
                    {
                        Log("FATAL: CameraUnlock.Core.dll NOT FOUND! Mod cannot function.");
                        return;
                    }
                }

                if (_initialized && HeadTrackingMod.Instance != null)
                {
                    Log("Already initialized, skipping");
                    return;
                }

                Log("Creating mod GameObject...");
                _initialized = true;

                // Create the mod GameObject with protection against destruction
                var modObject = new GameObject("HeadTracking");
                modObject.hideFlags = HideFlags.HideAndDontSave;
                UnityEngine.Object.DontDestroyOnLoad(modObject);

                Log("Adding HeadTrackingMod component...");
                modObject.AddComponent<HeadTrackingMod>();

                // Create recreator helper if needed
                if (_recreator == null)
                {
                    Log("Creating recreator...");
                    var recreatorObj = new GameObject("HeadTrackingRecreator");
                    recreatorObj.hideFlags = HideFlags.HideAndDontSave;
                    UnityEngine.Object.DontDestroyOnLoad(recreatorObj);
                    _recreator = recreatorObj.AddComponent<ModRecreator>();
                }

                Log("=== HeadTracking mod initialized successfully! ===");
            }
            catch (TypeLoadException ex)
            {
                Log($"TYPE LOAD FAILED: {ex.TypeName}");
                Log($"Full exception: {ex}");
            }
            catch (FileNotFoundException ex)
            {
                Log($"FILE NOT FOUND: {ex.FileName}");
                Log($"Full exception: {ex}");
            }
            catch (Exception ex)
            {
                Log($"EXCEPTION: {ex.GetType().Name}: {ex.Message}");
                Log($"Stack trace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Schedule recreation of the mod on next frame.
        /// </summary>
        public static void ScheduleRecreate()
        {
            _needsRecreate = true;
        }

        /// <summary>
        /// Check if recreation is needed and perform it.
        /// </summary>
        internal static void CheckRecreate()
        {
            if (_needsRecreate && HeadTrackingMod.Instance == null)
            {
                _needsRecreate = false;
                Log("Recreating mod after destruction...");
                Initialize();
            }
        }

        /// <summary>
        /// Reset the recreator reference so it can be recreated.
        /// </summary>
        internal static void ResetRecreator()
        {
            _recreator = null;
        }

        /// <summary>
        /// Logs a message to the shared logger.
        /// </summary>
        internal static void Log(string message)
        {
            Logger.Log(message);
        }
    }

    /// <summary>
    /// Helper MonoBehaviour that checks for mod recreation needs every frame.
    /// </summary>
    internal class ModRecreator : MonoBehaviour
    {
        private void Update()
        {
            ModLoader.CheckRecreate();
        }

        private void OnDestroy()
        {
            ModLoader.Log("[Recreator] OnDestroy - recreator being destroyed!");
            // Null the cached reference so the next ModLoader.Initialize() call
            // creates a fresh recreator GameObject instead of reusing this dead one.
            ModLoader.ResetRecreator();
        }
    }
}
