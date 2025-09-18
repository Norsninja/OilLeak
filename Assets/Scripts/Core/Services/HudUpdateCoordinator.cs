using UnityEngine;
using Core;

namespace Core.Services
{
    /// <summary>
    /// Coordinates UI updates at a fixed rate (4Hz) to avoid frame-by-frame updates
    /// Subscribes to game state changes to know when to update
    /// </summary>
    public class HudUpdateCoordinator : IResettable
    {
        private const float UPDATE_INTERVAL = 0.25f; // 4Hz (250ms)

        private float lastUpdateTime;
        private bool isActive;

        public HudUpdateCoordinator()
        {
            // Subscribe to state changes
            if (GameCore.Flow != null)
            {
                GameCore.Flow.OnStateChanged += OnStateChanged;
                Debug.Log("[HudUpdateCoordinator] Created and subscribed to state changes");
            }
            else
            {
                Debug.LogError("[HudUpdateCoordinator] GameCore.Flow is null - cannot subscribe to state changes");
            }

            Reset();
        }

        /// <summary>
        /// Called from GameCore.Update() to check if UI needs updating
        /// </summary>
        public void Update()
        {
            if (!isActive) return;

            // Check if enough time has passed for next update
            float currentTime = Time.time;
            if (currentTime - lastUpdateTime >= UPDATE_INTERVAL)
            {
                lastUpdateTime = currentTime;
                UpdateAllUI();
            }
        }

        /// <summary>
        /// Push latest data to all UI systems
        /// </summary>
        private void UpdateAllUI()
        {
            // Get latest stats from GameSession
            if (GameCore.Session == null) return;

            var stats = GameCore.Session.GetStats();

            // Update main HUD
            if (GameCore.HUD != null)
            {
                GameCore.HUD.UpdateGameUI();

                // Update specific elements
                GameCore.HUD.UpdateTimer(stats.TimeElapsed);
                GameCore.HUD.UpdateScore(stats.GallonsDelayed);
                GameCore.HUD.UpdateOilLeaked(stats.GallonsEscaped);
            }

            // DevHUD is already updated in GameCore.Update(), but we could consolidate here later

            // Log every 10th update (every 2.5 seconds) for debugging
            if (Random.Range(0, 10) == 0)
            {
                Debug.Log($"[HudUpdateCoordinator] UI Updated - Blocked: {stats.ParticlesBlocked}, Escaped: {stats.ParticlesEscaped}, Time: {stats.TimeElapsed:F1}s");
            }
        }

        /// <summary>
        /// Handle state changes to start/stop UI updates
        /// </summary>
        private void OnStateChanged(GameFlowState oldState, GameFlowState newState)
        {
            Debug.Log($"[HudUpdateCoordinator] State changed: {oldState} → {newState}");

            switch (newState)
            {
                case GameFlowState.Running:
                    isActive = true;
                    lastUpdateTime = Time.time; // Reset timer
                    Debug.Log("[HudUpdateCoordinator] Started updating UI (Running state)");
                    break;

                case GameFlowState.Starting:
                    // Could enable updates here if needed during starting
                    isActive = false;
                    break;

                case GameFlowState.Paused:
                case GameFlowState.Ending:
                case GameFlowState.Cleaning:
                case GameFlowState.ShowingResults:
                case GameFlowState.Menu:
                    isActive = false;
                    Debug.Log("[HudUpdateCoordinator] Stopped updating UI");
                    break;
            }
        }

        #region IResettable Implementation

        public void Reset()
        {
            isActive = false;
            lastUpdateTime = 0f;
            Debug.Log("[HudUpdateCoordinator] State reset");
        }

        public bool IsClean => !isActive && lastUpdateTime == 0f;

        #endregion

        /// <summary>
        /// Cleanup when destroyed
        /// </summary>
        public void Dispose()
        {
            if (GameCore.Flow != null)
            {
                GameCore.Flow.OnStateChanged -= OnStateChanged;
            }

            Reset();
            Debug.Log("[HudUpdateCoordinator] Disposed and unsubscribed");
        }
    }
}