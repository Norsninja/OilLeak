using UnityEngine;

/// <summary>
/// Interface for all objects that need cleanup on game restart
/// Enforces proper cleanup discipline across the codebase
/// </summary>
public interface IResettable
{
    /// <summary>
    /// Reset the object to initial state
    /// Called during state transition to Cleaning
    /// </summary>
    void Reset();

    /// <summary>
    /// Verify the object is properly cleaned
    /// Used for debug assertions to catch leaks
    /// </summary>
    bool IsClean { get; }
}

/// <summary>
/// Extension methods for bulk reset operations
/// </summary>
public static class ResettableExtensions
{
    /// <summary>
    /// Reset all IResettable objects in scene
    /// </summary>
    public static void ResetAll()
    {
        var resettables = Object.FindObjectsOfType<MonoBehaviour>();
        int resetCount = 0;

        foreach (var obj in resettables)
        {
            if (obj is IResettable resettable)
            {
                resettable.Reset();
                resetCount++;

                // Debug assertion in development builds
                #if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (!resettable.IsClean)
                {
                    Debug.LogError($"{obj.name} failed to clean properly!", obj);
                }
                #endif
            }
        }

        Debug.Log($"Reset {resetCount} objects");
    }

    /// <summary>
    /// Verify all objects are clean (for debug)
    /// </summary>
    public static bool VerifyAllClean()
    {
        var resettables = Object.FindObjectsOfType<MonoBehaviour>();
        bool allClean = true;

        foreach (var obj in resettables)
        {
            if (obj is IResettable resettable && !resettable.IsClean)
            {
                Debug.LogError($"{obj.name} is not clean!", obj);
                allClean = false;
            }
        }

        return allClean;
    }
}