using UnityEngine;
using Core.Services;

namespace Core.Systems
{
    /// <summary>
    /// Orchestrates the futility gameplay loop
    /// Subscribes to GameCore events and coordinates services
    /// Does NOT own gameplay logic - only orchestrates
    /// </summary>
    public class FutilityGameplaySystem : IResettable
    {
        // Configuration
        private const float BASE_EMISSION_RATE = 5f;
        private const float MAX_EMISSION_RATE = 100f;
        private const float FUTILITY_UPDATE_INTERVAL = 2f; // Update difficulty at 2Hz

        // Exponential difficulty curves
        private readonly AnimationCurve emissionCurve;
        private readonly AnimationCurve multiplierCurve;

        // State tracking
        private float lastUpdateTime;
        private float gameStartTime;
        private bool isRunning;

        // Metrics for futility scoring
        private int totalParticlesBlocked;
        private int totalParticlesEscaped;
        private float peakDifficulty;

        // Integrity tier system
        private float currentIntegrity = 100f;
        private int currentTier = 5; // 5=Pristine, 4=Damaged, 3=Critical, 2=Failing, 1=Collapsed
        private readonly float[] tierThresholds = { 0f, 30f, 60f, 80f, 90f, 100f };
        private readonly string[] tierNames = { "Collapsed", "Failing", "Critical", "Damaged", "Stable", "Pristine" };

        // Events for integrity changes
        public delegate void IntegrityTierChanged(int newTier, string tierName, float integrity);
        public static event IntegrityTierChanged OnIntegrityTierChanged;

        public FutilityGameplaySystem()
        {
            // Create exponential curves for futility
            // Emission: 5 → 50 particles/sec over 10 minutes (exponential)
            emissionCurve = AnimationCurve.EaseInOut(0f, 5f, 600f, 50f);

            // Multiplier: 1x → 3x difficulty over 10 minutes
            multiplierCurve = AnimationCurve.EaseInOut(0f, 1f, 600f, 3f);

            // Subscribe to state changes
            if (GameCore.Flow != null)
            {
                GameCore.Flow.OnStateChanged += OnStateChanged;
                Debug.Log("[FutilitySystem] Subscribed to state changes");
            }
        }

        private void OnStateChanged(GameFlowState oldState, GameFlowState newState)
        {
            Debug.Log($"[FutilitySystem] State transition: {oldState} → {newState}");

            switch (newState)
            {
                case GameFlowState.Starting:
                    OnStarting();
                    break;

                case GameFlowState.Running:
                    OnRunning();
                    break;

                case GameFlowState.Ending:
                    OnEnding();
                    break;

                case GameFlowState.Cleaning:
                    OnCleaning();
                    break;

                case GameFlowState.ShowingResults:
                    OnShowingResults();
                    break;
            }
        }

        private void OnStarting()
        {
            // Reset internal metrics
            ResetMetrics();

            // Disable player movement during starting
            if (GameCore.Player != null)
            {
                GameCore.Player.EnableMovement(false);
                GameCore.Player.ResetPosition();
                Debug.Log("[FutilitySystem] Player movement disabled during starting");
            }

            // Register for particle events if leak service exists
            if (GameCore.Leaks != null)
            {
                // We'll need to add event hooks to LeakService
                Debug.Log("[FutilitySystem] Ready to track particle events");
            }

            // Initialize difficulty service
            if (GameCore.Difficulty != null)
            {
                GameCore.Difficulty.Reset();
                GameCore.Difficulty.SetEmissionCurve(emissionCurve, BASE_EMISSION_RATE, MAX_EMISSION_RATE);
                GameCore.Difficulty.SetMultiplierCurve(multiplierCurve);
                GameCore.Difficulty.SetRubberBandEnabled(true);
                Debug.Log("[FutilitySystem] Difficulty service configured for futility");
            }

            // Prepare HUD
            if (GameCore.HUD != null)
            {
                GameCore.HUD.HideResults();
                // HUD updates now handled by HudUpdateCoordinator
            }

            Debug.Log("[FutilitySystem] Starting state initialized");
        }

        private void OnRunning()
        {
            isRunning = true;
            gameStartTime = Time.time;
            lastUpdateTime = Time.time;

            // Log initial integrity state with dynamic threshold
            int threshold = GameCore.Session?.GetMaxEscapedForDisplay() ?? 100;
            Debug.Log($"[FutilitySystem] Game Starting - Integrity: {currentIntegrity:F1}%, Tier: {currentTier} ({tierNames[currentTier]})");
            Debug.Log($"[FutilitySystem] Failure Threshold: {threshold} particles");
            Debug.Log($"[FutilitySystem] Tier Thresholds: 90%=Stable, 80%=Damaged, 60%=Critical, 30%=Failing, <30%=Collapsed");

            // Enable player movement
            if (GameCore.Player != null)
            {
                GameCore.Player.EnableMovement(true);
                Debug.Log("[FutilitySystem] Player movement enabled - game is running");
            }

            // Kick off the exponential escalation
            if (GameCore.Difficulty != null)
            {
                // Start the difficulty ticker
                Debug.Log("[FutilitySystem] Futility escalation begun - you cannot win");
            }

            // Show encouraging message (that will age poorly)
            if (GameCore.HUD != null)
            {
                GameCore.HUD.ShowMessage("Stop the leak! You can do this!", 3f);
            }

            // Start monitoring session events
            if (GameCore.Session != null)
            {
                // Subscribe to session metrics updates
                Debug.Log("[FutilitySystem] Monitoring session metrics");
            }
        }

        private void OnEnding()
        {
            isRunning = false;

            // Disable player movement
            if (GameCore.Player != null)
            {
                GameCore.Player.EnableMovement(false);
                Debug.Log("[FutilitySystem] Player movement disabled - game ending");
            }

            // Stop difficulty updates
            Debug.Log($"[FutilitySystem] Game ended - Peak difficulty: {peakDifficulty:F1}x");

            // Calculate futility score
            float survivalTime = Time.time - gameStartTime;
            int futilityScore = CalculateFutilityScore(survivalTime);

            // Update session with final stats
            if (GameCore.Session != null)
            {
                // GameSession doesn't have RecordAttempt - just end the session
                // The session tracks its own metrics internally
                GameCore.Session.EndSession();
            }

            // DO NOT call GameCore.EndGame() - GameCore owns state transitions!
            Debug.Log($"[FutilitySystem] Futility complete - Score: {futilityScore}, Time: {survivalTime:F1}s");
        }

        private void OnCleaning()
        {
            // Ensure all observers are stopped
            isRunning = false;

            // Ensure player is disabled
            if (GameCore.Player != null)
            {
                GameCore.Player.EnableMovement(false);
            }

            // Clear any lingering subscriptions
            Debug.Log("[FutilitySystem] Cleaning phase - clearing subscriptions");
        }

        private void OnShowingResults()
        {
            // Push final stats to UI
            if (GameCore.HUD != null && GameCore.Session != null)
            {
                // Get session stats and pass to the new ShowResults overload
                SessionStats stats = GameCore.Session.GetStats();
                GameCore.HUD.ShowResults(stats, peakDifficulty);

                // The futility message is now handled in UIController
                // No need to call ShowMessage separately
            }
        }

        /// <summary>
        /// Update tick - should be called from GameCore or a MonoBehaviour
        /// </summary>
        public void Update()
        {
            if (!isRunning) return;

            // Check for failure condition every frame
            if (GameCore.Session != null && GameCore.Session.IsFailing)
            {
                // Only trigger end if we're still in Running state (avoid spam)
                if (GameCore.Flow != null && GameCore.Flow.CurrentState == GameFlowState.Running)
                {
                    Debug.Log($"[FutilitySystem] Failure threshold reached - {GameCore.Session.ParticlesEscaped} particles escaped");
                    // Use GameCore's API for state transitions (maintains guard rails and logging)
                    GameCore.EndGame();
                    return; // Don't process other updates once we're ending
                }
            }

            // Update integrity based on escaped particles
            UpdateIntegrity();

            // Update difficulty at 2Hz
            if (GameCore.Difficulty != null && GameCore.Difficulty.TickIfDue())
            {
                float currentDifficulty = GameCore.Difficulty.GetCurrentMultiplier();
                if (currentDifficulty > peakDifficulty)
                {
                    peakDifficulty = currentDifficulty;
                }

                // Update HUD with escalation
                if (GameCore.HUD != null)
                {
                    GameCore.HUD.UpdateDifficultyDisplay(currentDifficulty);
                }

                // Push to DevHUD
                if (GameCore.DevHud != null)
                {
                    GameCore.DevHud.UpdateDifficulty(
                        GameCore.Difficulty.GetCurrentEmissionRate(),
                        currentDifficulty,
                        GameCore.Difficulty.GetRubberBandAdjustment()
                    );
                }
            }

            // Monitor for particle events
            if (GameCore.Difficulty != null)
            {
                // Difficulty service tracks particle blocked/escaped internally
                // We just need to update our totals periodically
                float elapsed = GameCore.Difficulty.GetElapsedMinutes();

                // Show increasingly desperate messages
                if (elapsed > 1f && elapsed < 1.1f)
                {
                    GameCore.HUD?.ShowMessage("The pressure is building...", 2f);
                }
                else if (elapsed > 3f && elapsed < 3.1f)
                {
                    GameCore.HUD?.ShowMessage("It's getting worse!", 2f);
                }
                else if (elapsed > 5f && elapsed < 5.1f)
                {
                    GameCore.HUD?.ShowMessage("This is futile...", 2f);
                }
            }
        }

        private void ResetMetrics()
        {
            totalParticlesBlocked = 0;
            totalParticlesEscaped = 0;
            peakDifficulty = 1f;
            gameStartTime = 0f;
            lastUpdateTime = 0f;
            isRunning = false;

            // Reset integrity
            currentIntegrity = 100f;
            currentTier = 5; // Back to Pristine
        }

        private int CalculateFutilityScore(float survivalTime)
        {
            // Futility scoring: Points for trying, bonus for lasting longer
            int baseScore = 100; // You tried
            int timeBonus = (int)(survivalTime * 10); // 10 points per second
            int difficultyBonus = (int)(peakDifficulty * 100); // Bonus for surviving high difficulty

            return baseScore + timeBonus + difficultyBonus;
        }

        private void UpdateIntegrity()
        {
            if (GameCore.Session == null) return;

            // Calculate integrity based on escaped particles relative to failure threshold
            // This ensures integrity scales with whatever threshold we're using (100, 1000, etc)
            int particlesEscaped = GameCore.Session.ParticlesEscaped;
            int threshold = GameCore.Session?.GetMaxEscapedForDisplay() ?? 100;

            // Calculate percentage of threshold reached (0 to 1)
            float escapedPercent = threshold > 0 ? (float)particlesEscaped / threshold : 1f;

            // Convert to integrity percentage (100% = pristine, 0% = dead)
            float newIntegrity = Mathf.Clamp01(1f - escapedPercent) * 100f;

            // Log every 60 frames (approximately 1 second) to avoid spam
            if (Time.frameCount % 60 == 0 && particlesEscaped > 0)
            {
                Debug.Log($"[FutilitySystem] Integrity: {particlesEscaped}/{threshold} escaped = {escapedPercent*100:F1}% of threshold = {newIntegrity:F1}% integrity");
            }

            currentIntegrity = newIntegrity;

            // Determine current tier
            int newTier = CalculateIntegrityTier(currentIntegrity);

            // Fire event if tier changed
            if (newTier != currentTier)
            {
                currentTier = newTier;
                string tierName = tierNames[currentTier];

                Debug.Log($"[FutilitySystem] Integrity degraded to {tierName} ({currentIntegrity:F0}%)");
                OnIntegrityTierChanged?.Invoke(currentTier, tierName, currentIntegrity);

                // Show tier-specific messages
                ShowIntegrityMessage(currentTier);
            }
        }

        private int CalculateIntegrityTier(float integrity)
        {
            // Find which tier we're in (0=Collapsed through 5=Pristine)
            for (int i = tierThresholds.Length - 1; i >= 0; i--)
            {
                if (integrity >= tierThresholds[i])
                {
                    return i;
                }
            }
            return 0; // Collapsed
        }

        private void ShowIntegrityMessage(int tier)
        {
            if (GameCore.HUD == null) return;

            switch (tier)
            {
                case 4: // Damaged (90% → 80%)
                    GameCore.HUD.ShowMessage("The ocean is starting to turn...", 3f);
                    break;
                case 3: // Critical (80% → 60%)
                    GameCore.HUD.ShowMessage("Wildlife is dying. The damage is spreading.", 4f);
                    break;
                case 2: // Failing (60% → 30%)
                    GameCore.HUD.ShowMessage("Ecological collapse imminent!", 4f);
                    break;
                case 1: // Collapsed (30% → 0%)
                    GameCore.HUD.ShowMessage("The Gulf is dead. You failed.", 5f);
                    break;
            }
        }

        // GetFutilityMessage moved to UIController for better cohesion

        /// <summary>
        /// Get current integrity percentage for UI display
        /// </summary>
        public float GetIntegrity() => currentIntegrity;

        /// <summary>
        /// Get current integrity tier (0-5)
        /// </summary>
        public int GetIntegrityTier() => currentTier;

        /// <summary>
        /// Get integrity tier name for display
        /// </summary>
        public string GetIntegrityTierName() => tierNames[currentTier];

        #region IResettable Implementation

        public void Reset()
        {
            ResetMetrics();
            Debug.Log("[FutilitySystem] State reset");
        }

        public bool IsClean => !isRunning &&
            totalParticlesBlocked == 0 &&
            totalParticlesEscaped == 0 &&
            peakDifficulty == 1f;

        #endregion
    }
}