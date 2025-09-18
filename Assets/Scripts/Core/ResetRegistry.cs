using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Core
{
    /// <summary>
    /// Explicit registration system for IResettable objects
    /// Replaces ResettableExtensions.ResetAll() with deterministic cleanup
    /// Performance critical: must complete under 5ms for WebGL
    /// </summary>
    public static class ResetRegistry
    {
        private static readonly List<IResettable> registered = new List<IResettable>();
        private static readonly HashSet<int> registeredIds = new HashSet<int>(); // Prevent duplicates

        /// <summary>
        /// Explicitly register an IResettable object
        /// </summary>
        public static void Register(IResettable obj)
        {
            if (obj == null) return;

            int id = obj.GetHashCode();
            if (!registeredIds.Contains(id))
            {
                registered.Add(obj);
                registeredIds.Add(id);

                #if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[ResetRegistry] Registered {obj.GetType().Name} (Total: {registered.Count})");
                #endif
            }
        }

        /// <summary>
        /// Unregister an IResettable object (for cleanup)
        /// </summary>
        public static void Unregister(IResettable obj)
        {
            if (obj == null) return;

            int id = obj.GetHashCode();
            if (registeredIds.Remove(id))
            {
                registered.Remove(obj);

                #if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[ResetRegistry] Unregistered {obj.GetType().Name} (Remaining: {registered.Count})");
                #endif
            }
        }

        /// <summary>
        /// Reset all registered objects - replaces ResettableExtensions.ResetAll()
        /// CRITICAL: Must complete under 5ms for WebGL
        /// </summary>
        public static void ResetAll()
        {
            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            float startTime = Time.realtimeSinceStartup;
            var timings = new List<string>();
            #endif

            foreach (var obj in registered)
            {
                if (obj == null) continue;

                try
                {
                    #if UNITY_EDITOR || DEVELOPMENT_BUILD
                    float objStart = Time.realtimeSinceStartup;
                    #endif

                    obj.Reset();

                    #if UNITY_EDITOR || DEVELOPMENT_BUILD
                    float objTime = (Time.realtimeSinceStartup - objStart) * 1000f;
                    timings.Add($"{obj.GetType().Name}: {objTime:F2}ms");

                    // Immediately verify IsClean
                    if (!obj.IsClean)
                    {
                        Debug.LogError($"[ResetRegistry] {obj.GetType().Name} failed IsClean check after Reset!");
                    }
                    #endif
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[ResetRegistry] Reset failed for {obj.GetType().Name}: {e}");
                }
            }

            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            float totalTime = (Time.realtimeSinceStartup - startTime) * 1000f;

            // Single summary line for performance tracking
            string summary = string.Join(", ", timings);
            Debug.Log($"[ResetRegistry] {summary}, Total: {totalTime:F2}ms");

            // CRITICAL WARNING if over 5ms
            if (totalTime > 5f)
            {
                Debug.LogError($"[ResetRegistry] PERFORMANCE WARNING: Reset took {totalTime:F2}ms (target: 5ms)");
            }

            // Verify all clean
            VerifyAllClean();
            #endif
        }

        /// <summary>
        /// Clear all registrations (for scene changes or shutdown)
        /// </summary>
        public static void Clear()
        {
            registered.Clear();
            registeredIds.Clear();

            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("[ResetRegistry] Cleared all registrations");
            #endif
        }

        /// <summary>
        /// Get count of registered objects (for monitoring)
        /// </summary>
        public static int RegisteredCount => registered.Count;

        #if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>
        /// Development-only verification that all objects are clean
        /// </summary>
        private static void VerifyAllClean()
        {
            var dirty = registered.Where(r => r != null && !r.IsClean).ToList();
            if (dirty.Any())
            {
                Debug.LogError($"[ResetRegistry] {dirty.Count} objects failed to clean properly:");
                foreach (var obj in dirty)
                {
                    Debug.LogError($"  - {obj.GetType().Name} is still dirty");
                }
            }
        }

        /// <summary>
        /// Development-only: Get list of registered types for debugging
        /// </summary>
        public static string GetRegisteredTypes()
        {
            return string.Join(", ", registered.Where(r => r != null).Select(r => r.GetType().Name));
        }
        #endif
    }
}