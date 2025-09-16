using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// Game flow states - NO VICTORY STATE EXISTS
/// This is a futility simulator - you can only fail
/// </summary>
public enum GameFlowState
{
    Menu,           // Pre-game, ambient oil
    Starting,       // Transition state for setup
    Running,        // Active gameplay
    Paused,         // Gameplay paused
    Ending,         // Oil has won, stopping game
    Cleaning,       // Resetting all systems
    ShowingResults  // Displaying failure statistics
}

/// <summary>
/// State machine for game flow control
/// Enforces fail-only contract and legal transitions
/// </summary>
public class GameFlowStateMachine
{
    // Current state
    private GameFlowState currentState = GameFlowState.Menu;
    public GameFlowState CurrentState => currentState;

    // State change event (will be wired later)
    public event Action<GameFlowState, GameFlowState> OnStateChanged;

    // Transition validation matrix
    private readonly Dictionary<GameFlowState, HashSet<GameFlowState>> validTransitions = new()
    {
        [GameFlowState.Menu] = new() { GameFlowState.Starting },
        [GameFlowState.Starting] = new() { GameFlowState.Running },
        [GameFlowState.Running] = new() { GameFlowState.Paused, GameFlowState.Ending },
        [GameFlowState.Paused] = new() { GameFlowState.Running, GameFlowState.Ending },
        [GameFlowState.Ending] = new() { GameFlowState.Cleaning },
        [GameFlowState.Cleaning] = new() { GameFlowState.ShowingResults },
        [GameFlowState.ShowingResults] = new() { GameFlowState.Menu }
    };

    // Debug mode for verbose logging
    private bool debugMode = true;

    /// <summary>
    /// CRITICAL: Block any attempt to create victory state
    /// </summary>
    static GameFlowStateMachine()
    {
        // Runtime assertion - if someone adds "Victory" to enum, crash immediately
        var states = Enum.GetNames(typeof(GameFlowState));
        foreach (var state in states)
        {
            string stateLower = state.ToLower();
            // Be specific - don't catch "ShowingResults" as "success"
            if (stateLower == "victory" ||
                stateLower == "win" ||
                stateLower == "winning" ||
                stateLower == "success" ||
                stateLower == "succeeded" ||
                stateLower.Contains("victory") ||
                stateLower.Contains("winner"))
            {
                throw new System.InvalidOperationException(
                    $"ILLEGAL STATE DETECTED: {state}\n" +
                    "This is a futility simulator. There is no victory. The oil always wins.\n" +
                    "Remove any victory states immediately.");
            }
        }
    }

    /// <summary>
    /// Attempt to transition to a new state
    /// </summary>
    public bool TransitionTo(GameFlowState newState)
    {
        // CRITICAL: Block any victory transitions (belt and suspenders with static check)
        string stateName = newState.ToString().ToLower();
        if (stateName.Contains("victory") || stateName.Contains("win") || stateName.Contains("success"))
        {
            Debug.LogError($"ILLEGAL: Attempted transition to victory state '{newState}'! This is a futility simulator!");
            return false;
        }

        // Check if transition is valid
        if (!IsValidTransition(currentState, newState))
        {
            Debug.LogError($"INVALID TRANSITION: {currentState} → {newState} is not allowed!");
            LogValidTransitions();
            return false;
        }

        // Log transition
        if (debugMode)
        {
            Debug.Log($"STATE TRANSITION: {currentState} → {newState}");
        }

        // Store old state
        var oldState = currentState;

        // Exit current state
        ExitState(currentState);

        // Update state
        currentState = newState;

        // Enter new state
        EnterState(newState);

        // Notify listeners
        OnStateChanged?.Invoke(oldState, newState);

        return true;
    }

    /// <summary>
    /// Check if a transition is valid
    /// </summary>
    private bool IsValidTransition(GameFlowState from, GameFlowState to)
    {
        if (!validTransitions.ContainsKey(from))
        {
            Debug.LogError($"State {from} has no defined transitions!");
            return false;
        }

        return validTransitions[from].Contains(to);
    }

    /// <summary>
    /// Log valid transitions from current state (for debugging)
    /// </summary>
    private void LogValidTransitions()
    {
        if (!validTransitions.ContainsKey(currentState))
        {
            Debug.LogWarning($"No valid transitions from {currentState}");
            return;
        }

        var valid = validTransitions[currentState];
        Debug.Log($"Valid transitions from {currentState}: {string.Join(", ", valid)}");
    }

    /// <summary>
    /// Handle state exit logic
    /// </summary>
    private void ExitState(GameFlowState state)
    {
        if (debugMode)
        {
            Debug.Log($"Exiting state: {state}");
        }

        switch (state)
        {
            case GameFlowState.Menu:
                // Stop ambient oil effects
                break;

            case GameFlowState.Running:
                // Will pause oil emission
                break;

            case GameFlowState.Paused:
                // Resume time scale if needed
                break;

            case GameFlowState.Ending:
                // Ensure game is stopped
                break;

            case GameFlowState.Cleaning:
                // Verify cleanup complete
                if (!ResettableExtensions.VerifyAllClean())
                {
                    Debug.LogWarning("Some objects failed to clean properly!");
                }
                break;
        }
    }

    /// <summary>
    /// Handle state entry logic
    /// </summary>
    private void EnterState(GameFlowState state)
    {
        if (debugMode)
        {
            Debug.Log($"Entering state: {state}");
        }

        switch (state)
        {
            case GameFlowState.Menu:
                // Start ambient oil effects
                // No gameplay, just aesthetics
                break;

            case GameFlowState.Starting:
                // Reset all systems
                // Prepare for new game
                // Auto-transition to Running when ready
                break;

            case GameFlowState.Running:
                // Start oil spawning
                // Enable player controls
                // Start timer
                break;

            case GameFlowState.Paused:
                // Stop oil spawning
                // Disable controls
                // Keep physics running (or not, design choice)
                break;

            case GameFlowState.Ending:
                // The oil has won
                // Stop spawning
                // Calculate final stats
                break;

            case GameFlowState.Cleaning:
                // Reset ALL systems
                // Clear particles
                // Return items to pools
                // Force GC if needed
                break;

            case GameFlowState.ShowingResults:
                // Display failure statistics
                // Show "The Oil Won" message
                // Wait for player input
                break;
        }
    }

    /// <summary>
    /// Force a specific state (use with caution, bypasses validation)
    /// </summary>
    public void ForceState(GameFlowState state)
    {
        Debug.LogWarning($"FORCING STATE: {currentState} → {state} (bypassing validation)");
        var oldState = currentState;
        currentState = state;
        OnStateChanged?.Invoke(oldState, state);
    }

    /// <summary>
    /// Reset to initial menu state
    /// </summary>
    public void Reset()
    {
        if (currentState != GameFlowState.Menu)
        {
            Debug.Log("Resetting state machine to Menu");
            ForceState(GameFlowState.Menu);
        }
    }

    /// <summary>
    /// Check if currently in gameplay states
    /// </summary>
    public bool IsInGameplay()
    {
        return currentState == GameFlowState.Running || currentState == GameFlowState.Paused;
    }

    /// <summary>
    /// Check if game is active (not paused)
    /// </summary>
    public bool IsActive()
    {
        return currentState == GameFlowState.Running;
    }

    /// <summary>
    /// Get state info for debugging
    /// </summary>
    public string GetStateInfo()
    {
        return $"Current: {currentState}, InGame: {IsInGameplay()}, Active: {IsActive()}";
    }
}