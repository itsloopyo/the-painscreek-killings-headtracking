using System;
using System.IO;
using UnityEngine;

namespace PainscreekHeadTracking
{
    /// <summary>
    /// Centralized logging utility for head tracking mod.
    /// Writes timestamped messages to HeadTracking.log in the assembly directory.
    /// Falls back to temp directory if assembly path is inaccessible.
    /// Uses a persistent StreamWriter to avoid file open/close per log call.
    /// </summary>
    public static class Logger
    {
        private const string LogFileName = "HeadTracking.log";

        private static StreamWriter? _writer;
        private static bool _initialized;
        // OpenTrackReceiver runs on a background thread and routes its Log
        // delegate through here, so writes can race with the Unity main thread.
        // StreamWriter is not thread-safe — torn writes corrupt the log and can
        // throw IndexOutOfRangeException inside the writer's internal buffer.
        private static readonly object _writeLock = new object();

        /// <summary>
        /// Initializes the logger by opening the log file for writing.
        /// Called automatically on first Log() call, but can be called explicitly.
        /// </summary>
        public static void Initialize()
        {
            if (_initialized) return;
            lock (_writeLock)
            {
                if (_initialized) return;

                try
                {
                    string? assemblyDir = Path.GetDirectoryName(typeof(Logger).Assembly.Location);
                    string primaryPath = string.IsNullOrEmpty(assemblyDir)
                        ? Path.Combine(Path.GetTempPath(), LogFileName)
                        : Path.Combine(assemblyDir, LogFileName);
                    _writer = OpenLog(primaryPath);
                }
                catch (Exception ex)
                {
                    // The log file is a non-fatal side channel and must never take down
                    // the mod. The primary location can be unavailable for several
                    // reasons: sandboxed/dynamic assembly (NotSupported/Security), a
                    // read-only Managed dir (UnauthorizedAccess), or the file being
                    // locked by a second game instance or an AV scan (IOException).
                    // Try temp once; if even that fails, run with logging disabled.
                    Debug.LogWarning($"HeadTracking: cannot open log next to assembly: {ex.Message}. Trying temp.");
                    try
                    {
                        _writer = OpenLog(Path.Combine(Path.GetTempPath(), LogFileName));
                    }
                    catch (Exception tempEx)
                    {
                        Debug.LogError($"HeadTracking: logging disabled, temp fallback failed: {tempEx.Message}");
                        _writer = null;
                    }
                }
                finally
                {
                    // Set unconditionally so a failed open degrades to a no-op writer
                    // instead of re-attempting (and re-throwing) on every Log() call.
                    _initialized = true;
                }
            }
        }

        // append=false overwrites the previous run's log so it doesn't grow unboundedly across sessions.
        private static StreamWriter OpenLog(string path) =>
            new StreamWriter(path, append: false) { AutoFlush = true };

        /// <summary>
        /// Logs a timestamped message to the log file.
        /// Thread-safe: callable from the UDP receive thread and the Unity main thread.
        /// </summary>
        /// <param name="message">Message to log</param>
        public static void Log(string message)
        {
            if (!_initialized)
            {
                Initialize();
            }

            // Fast-exit when the file open failed in Initialize() - both the
            // assembly-dir and temp-dir paths can fail (sandboxed/dynamic
            // assembly, read-only Managed dir, AV lock). In that state every
            // call would otherwise still pay DateTime.Now + string format +
            // allocation + lock acquire/release for a write that's a no-op.
            if (_writer == null) return;

            string line = $"[{DateTime.Now:HH:mm:ss.fff}] {message}";
            try
            {
                lock (_writeLock)
                {
                    _writer?.WriteLine(line);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"HeadTracking log write failed: {ex.Message}");
            }
        }
    }
}
