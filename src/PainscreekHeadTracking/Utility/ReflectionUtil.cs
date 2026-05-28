using System;

namespace PainscreekHeadTracking
{
    /// <summary>
    /// Shared reflection helpers for locating game types at runtime.
    /// </summary>
    internal static class ReflectionUtil
    {
        /// <summary>
        /// Searches every loaded assembly for a type by name, returning the first
        /// match or null if none defines it. Assemblies that throw on inspection
        /// (dynamic or sandboxed) are skipped so one bad assembly can't abort the scan.
        /// </summary>
        public static Type? FindType(string typeName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    // ReferenceEquals: Unity 5.3.4 Mono lacks Type.op_Inequality.
                    Type? type = asm.GetType(typeName);
                    if (!ReferenceEquals(type, null))
                    {
                        return type;
                    }
                }
                catch
                {
                    // Skip assemblies that can't be inspected.
                }
            }
            return null;
        }
    }
}
