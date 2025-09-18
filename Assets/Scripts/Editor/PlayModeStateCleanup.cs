#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using Core;
using Core.Services;

/// <summary>
/// Editor-only utility to reset static state when using Enter Play Mode without domain reload
/// Ensures clean state between play sessions for fast iteration
/// Uses RuntimeInitializeOnLoadMethod with SubsystemRegistration for guaranteed timing
/// </summary>
public static class PlayModeStateCleanup
{
    /// <summary>
    /// Runs after assemblies load but BEFORE any Awake() methods in the scene
    /// This is Unity's official mechanism for resetting static state with domain reload disabled
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void OnSubsystemRegistration()
    {
        Debug.Log("[PlayModeStateCleanup] Subsystem registration - running static reset");
        StaticResetRunner.Run();
    }
}

/// <summary>
/// Central runner that invokes all static reset helpers
/// Add new reset calls here as static fields are discovered
/// </summary>
public static class StaticResetRunner
{
    public static void Run()
    {
        int resetCount = 0;

        // Reset singletons - GameController needs resetting too
        if (GameController.EditorResetSingleton())
        {
            Debug.Log("  - Reset GameController singleton");
            resetCount++;
        }

        if (GameCore.EditorResetStatics())
        {
            Debug.Log("  - Reset GameCore statics");
            resetCount++;
        }

        if (LeakManager.EditorResetSingleton())
        {
            Debug.Log("  - Reset LeakManager singleton");
            resetCount++;
        }

        // Reset other known singletons
        // TODO: Add EditorResetSingleton to DifficultyManager when needed
        // if (DifficultyManager.EditorResetSingleton())
        // {
        //     Debug.Log("  - Reset DifficultyManager singleton");
        //     resetCount++;
        // }

        // TODO: Add EditorResetSingleton to GameTimer when needed
        // if (GameTimer.EditorResetSingleton())
        // {
        //     Debug.Log("  - Reset GameTimer singleton");
        //     resetCount++;
        // }

        // Clear registries
        ResetRegistry.Clear();
        Debug.Log("  - Cleared ResetRegistry");
        resetCount++;

        // Clear static events in state machine
        if (GameFlowStateMachine.EditorClearEvents())
        {
            Debug.Log("  - Cleared GameFlowStateMachine events");
            resetCount++;
        }

        // Clear any other static caches
        // Add more reset calls as needed...

        Debug.Log($"[StaticResetRunner] Reset {resetCount} static systems - Clean state ready");
    }
}
#endif