using System.Collections;
using UnityEngine;
using Core;

namespace Core.Systems
{
    /// <summary>
    /// Centralized input controller for game flow (Pause/Resume/Restart).
    /// Handles ESC for pause and R for restart with state-aware gating.
    /// Movement and item inputs remain in their respective controllers.
    /// </summary>
    public class FlowInputController : MonoBehaviour
    {
        [Header("Restart Configuration")]
        [SerializeField] private float holdToRestartDuration = 1.5f;
        [SerializeField] private bool debugLogging = true;

        // Restart state tracking
        private float restartHoldTime = 0f;
        private bool isRestarting = false;
        private bool pendingResultsRestart = false;

        // Cached state for efficiency
        private GameFlowState currentState;

        void Start()
        {
            LogDebug("FlowInputController Starting...");

            // Subscribe to state changes
            if (GameCore.Flow != null)
            {
                GameCore.Flow.OnStateChanged += OnStateChanged;
                currentState = GameCore.Flow.CurrentState;
                Debug.Log($"[FlowInput] Subscribed to state changes. Current state: {currentState}");
            }
            else
            {
                Debug.LogError("[FlowInput] GameCore.Flow is null! Cannot subscribe to state changes.");
            }

            LogDebug("FlowInputController initialized");

        }

        void OnDestroy()
        {
            // Unsubscribe from state changes
            if (GameCore.Flow != null)
            {
                GameCore.Flow.OnStateChanged -= OnStateChanged;
            }
        }

        private void OnStateChanged(GameFlowState oldState, GameFlowState newState)
        {
            // CRITICAL FIX: Verify this is actually the current state, not an old queued event
            if (GameCore.Flow != null && GameCore.Flow.CurrentState != newState)
            {
                LogDebug($"Ignoring stale state change: {oldState} → {newState} (actual state is {GameCore.Flow.CurrentState})");
                return;
            }

            currentState = newState;
            LogDebug($"State changed: {oldState} → {newState}");


            // Clear restart guard and progress on entry to ShowingResults or Menu
            if (newState == GameFlowState.ShowingResults || newState == GameFlowState.Menu)
            {
                if (isRestarting || restartHoldTime > 0f)
                {
                    LogDebug($"Clearing restart state on entry to {newState}");
                    ResetRestartState();
                }
            }

            // Clear pending restart if we've left ShowingResults for any other reason
            if (oldState == GameFlowState.ShowingResults && pendingResultsRestart)
            {
                LogDebug("Clearing pending restart - left ShowingResults without executing");
                pendingResultsRestart = false;
            }

            // Auto-start is now handled in GameCore.HandleMenuState()
            // This ensures the modal is properly gated
        }

        void Update()
        {
            // Always use the actual current state from GameCore.Flow to avoid stale state issues
            GameFlowState actualState = GameCore.Flow?.CurrentState ?? currentState;


            // Special case: Handle ShowingResults input BEFORE checking IsInitialized
            // This allows restart to work even during editor static reset windows
            if (actualState == GameFlowState.ShowingResults)
            {
                HandleShowingResultsInput();
                return; // Don't process other inputs in this state
            }

            // Check for pending restart that can now execute
            if (pendingResultsRestart && GameCore.IsInitialized)
            {
                LogDebug("Executing pending restart from ShowingResults");
                pendingResultsRestart = false;
                GameCore.SetAutoStartNextRun(true);
                GameCore.RestartGame();
                return;
            }

            // For all other states, require GameCore to be initialized
            if (!GameCore.IsInitialized)
            {
                return;
            }

            HandleStartInput(actualState);
            HandlePauseInput(actualState);
            HandleRestartInput(actualState);
        }

        private void HandleStartInput(GameFlowState state)
        {
            // E key to start from Menu
            if (Input.GetKeyDown(KeyCode.E))
            {
                if (state == GameFlowState.Menu)
                {
                    LogDebug("E pressed - Starting game");

                    // Immediately hide the start modal to prevent one-frame flicker
                    if (GameCore.StartModal != null)
                    {
                        GameCore.StartModal.HideModal();
                    }

                    GameCore.StartGame();
                }
            }
        }

        private void HandlePauseInput(GameFlowState state)
        {
            // ESC key for pause/resume
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                switch (state)
                {
                    case GameFlowState.Running:
                        LogDebug("ESC pressed - Pausing game");
                        GameCore.PauseGame();
                        break;

                    case GameFlowState.Paused:
                        LogDebug("ESC pressed - Resuming game");
                        GameCore.ResumeGame();
                        break;

                    default:
                        // ESC does nothing in other states
                        break;
                }
            }
        }

        private void HandleRestartInput(GameFlowState state)
        {
            // R key behavior depends on state
            switch (state)
            {
                case GameFlowState.Running:
                case GameFlowState.Paused:
                    // Only these states need the isRestarting guard
                    HandleHoldToRestart();
                    break;

                case GameFlowState.ShowingResults:
                    // ShowingResults bypasses the guard entirely - just needs fresh press
                    HandleInstantRestart();
                    break;

                case GameFlowState.Menu:
                    // Allow starting game with R from menu (convenience)
                    if (Input.GetKeyDown(KeyCode.R))
                    {
                        LogDebug("R pressed in Menu - Starting game");
                        GameCore.StartGame();
                    }
                    break;

                case GameFlowState.Ending:
                case GameFlowState.Cleaning:
                case GameFlowState.Starting:
                    // Explicitly ignore R during transitions
                    // No input processing during state transitions
                    break;

                default:
                    break;
            }
        }

        private void HandleHoldToRestart()
        {
            // Hold-to-restart during gameplay
            if (Input.GetKey(KeyCode.R) && !isRestarting)
            {
                // Accumulate hold time (use unscaled for pause compatibility)
                restartHoldTime += Time.unscaledDeltaTime;

                // Update progress UI
                float progress = Mathf.Clamp01(restartHoldTime / holdToRestartDuration);
                UpdateRestartProgress(progress);

                // Check if held long enough
                if (restartHoldTime >= holdToRestartDuration)
                {
                    isRestarting = true;
                    GameCore.SetAutoStartNextRun(true);
                    LogDebug($"R held for {holdToRestartDuration}s - Restarting game");

                    // Hide progress immediately
                    UpdateRestartProgress(0f);

                    // Initiate restart chain
                    GameCore.EndGame();
                }
            }
            else if (Input.GetKeyUp(KeyCode.R) || isRestarting)
            {
                // Reset on release or if restart initiated
                if (restartHoldTime > 0f)
                {
                    LogDebug($"R released after {restartHoldTime:F1}s");
                    ResetRestartState();
                }
            }
        }

        private void HandleShowingResultsInput()
        {
            // Check if modal is blocking input
            if (OilLeak.UI.LeaderboardUIController.IsModalActive)
            {
                // Let the modal handle its own input
                return;
            }

            // L key - View Leaderboard
            if (Input.GetKeyDown(KeyCode.L))
            {
                LogDebug("L pressed in ShowingResults - Opening leaderboard");

                // Get UI controller and open leaderboard
                var uiController = UnityEngine.Object.FindObjectOfType<UIController>();
                if (uiController != null)
                {
                    // Find or create leaderboard controller
                    var leaderboardController = UnityEngine.Object.FindObjectOfType<OilLeak.UI.LeaderboardUIController>();
                    if (leaderboardController != null)
                    {
                        leaderboardController.Open();
                    }
                    else
                    {
                        LogDebug("LeaderboardUIController not found in scene");
                    }
                }
            }

            // Instant restart from results screen - no guard needed
            // Using GetKeyDown ensures fresh press (not held from previous state)
            if (Input.GetKeyDown(KeyCode.R))
            {
                LogDebug("R pressed in ShowingResults - Instant restart");

                // If GameCore is initialized, restart immediately
                if (GameCore.IsInitialized)
                {
                    GameCore.SetAutoStartNextRun(true);
                    GameCore.RestartGame(); // Goes to Menu, then auto-starts
                }
                else
                {
                    // Queue the restart for when GameCore becomes available
                    LogDebug("GameCore not initialized - queuing restart for later execution");
                    pendingResultsRestart = true;
                }
            }
        }

        private void HandleInstantRestart()
        {
            // This method is now replaced by HandleShowingResultsInput
            // Keeping for backwards compatibility but redirecting to new method
            HandleShowingResultsInput();
        }

        private void ResetRestartState()
        {
            restartHoldTime = 0f;
            isRestarting = false;
            UpdateRestartProgress(0f);
        }

        private void UpdateRestartProgress(float progress)
        {
            // Update UI with restart progress
            var uiController = FindObjectOfType<UIController>();
            if (uiController != null)
            {
                if (progress > 0f && progress < 1f)
                {
                    uiController.ShowRestartProgress(progress);
                }
                else if (progress == 0f)
                {
                    uiController.HideRestartProgress();
                }
            }

            // Debug logging
            if (debugLogging && progress > 0f && Mathf.Approximately(progress % 0.25f, 0f))
            {
                LogDebug($"Restart progress: {progress:P0}");
            }
        }

        private void LogDebug(string message)
        {
            if (debugLogging)
            {
                Debug.Log($"[FlowInput] {message}");
            }
        }
    }
}