using UnityEngine;
using OilLeak.News;
using OilLeak;
using Core.Systems;

namespace Core.Services
{
    /// <summary>
    /// Adapter that bridges NewsTickerManager to GameCore's service architecture.
    /// Follows the established adapter pattern for service registration.
    /// </summary>
    public class NewsTickerServiceAdapter : INewsTickerService, IResettable
    {
        private readonly NewsTickerManager manager;
        private readonly NewsTickerUI ui;
        private bool subscribed = false;

        public NewsTickerServiceAdapter(NewsTickerManager manager, NewsTickerUI ui)
        {
            this.manager = manager;
            this.ui = ui;

            if (manager == null)
            {
                Debug.LogError("[NewsTickerServiceAdapter] Created with null manager!");
            }

            if (ui == null)
            {
                Debug.LogError("[NewsTickerServiceAdapter] Created with null UI!");
            }

            // Wire UI to manager
            if (ui != null && manager != null)
            {
                ui.Initialize(manager);
            }
        }

        #region INewsTickerService Implementation

        public bool IsReady => manager?.IsReady ?? false;

        public void Initialize()
        {
            manager?.Initialize();

            // Load config if available
            var config = Resources.Load<NewsTickerConfig>("NewsTickerConfig");
            if (config != null && ui != null)
            {
                ui.ApplyConfiguration(config);
            }
        }

        public void Start()
        {
            // Subscribe to events if not already subscribed
            if (!subscribed)
            {
                SubscribeToEvents();
            }

            manager?.Start();
            ui?.StartTicker();
        }

        public void Pause()
        {
            manager?.Pause();
            ui?.PauseTicker();
        }

        public void Resume()
        {
            manager?.Resume();
            ui?.ResumeTicker();
        }

        public void Stop()
        {
            // Unsubscribe events
            UnsubscribeFromEvents();

            manager?.Stop();
            ui?.StopTicker();
        }

        public string GetNextHeadline()
        {
            return manager?.GetNextHeadline() ?? "Service unavailable";
        }

        public void NotifyBreaking(BreakingEventType type, object context)
        {
            manager?.NotifyBreaking(type, context);
        }

        public void UpdateState(SessionStats stats, int tier)
        {
            manager?.UpdateState(stats, tier);
        }

        #endregion

        #region IResettable Implementation

        public void Reset()
        {
            Debug.Log("[NewsTickerServiceAdapter] Reset starting");

            // Order matters to avoid race with UI
            // 1. Stop UI first to halt scroller and prevent new requests
            ui?.StopTicker();

            // 2. Unsubscribe from events
            UnsubscribeFromEvents();

            // 3. Reset manager internal state
            manager?.ResetInternal();

            // 4. Mark adapter state clean
            subscribed = false;

            Debug.Log("[NewsTickerServiceAdapter] Reset complete");
        }

        public bool IsClean
        {
            get
            {
                // Check manager state
                bool managerClean = manager == null || (
                    !manager.IsInitialized &&
                    !manager.IsActive &&
                    !manager.IsPaused &&
                    manager.RegularQueueCount == 0 &&
                    manager.BreakingQueueCount == 0 &&
                    manager.CooldownCount == 0 &&
                    manager.RecentCount == 0 &&
                    !manager.HasSubscriptions
                );

                // Check UI state
                bool uiClean = ui == null || !ui.IsActive;

                // Check adapter state
                bool adapterClean = !subscribed;

                return managerClean && uiClean && adapterClean;
            }
        }

        #endregion

        #region Event Management

        private void SubscribeToEvents()
        {
            if (subscribed)
            {
                Debug.LogWarning("[NewsTickerServiceAdapter] Already subscribed to events");
                return;
            }

            // Subscribe to FutilityGameplaySystem tier changes
            FutilityGameplaySystem.OnIntegrityTierChanged += HandleIntegrityTierChange;

            // Subscribe to LeakManager pressure bursts
            LeakManager.OnPressureBurst += HandlePressureBurst;

            subscribed = true;
            Debug.Log("[NewsTickerServiceAdapter] Subscribed to events");
        }

        private void UnsubscribeFromEvents()
        {
            if (!subscribed)
            {
                return;
            }

            // Unsubscribe from FutilityGameplaySystem
            FutilityGameplaySystem.OnIntegrityTierChanged -= HandleIntegrityTierChange;

            // Unsubscribe from LeakManager
            LeakManager.OnPressureBurst -= HandlePressureBurst;

            subscribed = false;
            Debug.Log("[NewsTickerServiceAdapter] Unsubscribed from events");
        }

        private void HandleIntegrityTierChange(int newTier, string tierName, float integrity)
        {
            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[NewsTickerServiceAdapter] Integrity tier changed to {newTier} ({tierName})");
            #endif

            // Notify manager of breaking news
            manager?.NotifyBreaking(BreakingEventType.IntegrityTierChange, newTier);
        }

        private void HandlePressureBurst(float burstMultiplier)
        {
            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[NewsTickerServiceAdapter] Pressure burst with multiplier {burstMultiplier}");
            #endif

            // Notify manager of breaking news
            manager?.NotifyBreaking(BreakingEventType.PressureBurst, burstMultiplier);
        }

        #endregion
    }
}