using System;
using UnityEngine;
using Core.Systems;
using OilLeak.Toast.Services;

namespace Core.Services
{
    /// <summary>
    /// Adapter that connects GameSession, FutilityGameplaySystem, and ResupplyManager
    /// to the Toast system's IGameStateProvider interface
    /// </summary>
    public class ToastGameStateProvider : IGameStateProvider, IResettable, IDisposable
    {
        private readonly GameSession session;

        // Cached values exposed via properties
        private float timeElapsed = 0f;
        private float gallonsBlocked = 0f;
        private float gallonsEscaped = 0f;
        private int itemsDegraded = 0;
        private float integrity = 100f;
        private int resupplyCount = 0;
        private int runSeed;

        // IGameStateProvider implementation
        public float Timer => timeElapsed;
        public float GallonsBlocked => gallonsBlocked;
        public float GallonsEscaped => gallonsEscaped;
        public int ItemsDegraded => itemsDegraded;
        public float Integrity => integrity;
        public int ResupplyCount => resupplyCount;
        public int RunSeed => runSeed;

        // Events from IGameStateProvider
        public event Action<float> OnTimeUpdated;
        public event Action<float> OnGallonsBlockedChanged;
        public event Action<float> OnGallonsEscapedChanged;
        public event Action<float> OnIntegrityChanged;
        public event Action OnResupplyEvent;

        public ToastGameStateProvider(GameSession gameSession)
        {
            session = gameSession ?? throw new ArgumentNullException(nameof(gameSession));

            // Initialize run seed immediately to avoid 0 seed before first reset
            runSeed = UnityEngine.Random.Range(1000, 99999);

            // Subscribe to GameSession events
            SubscribeToSession();

            // Subscribe to FutilityGameplaySystem events
            SubscribeToFutility();

            // Subscribe to ResupplyManager events
            SubscribeToResupply();

            Debug.Log($"[ToastGameStateProvider] Initialized with seed {runSeed} and subscribed to all game events");
        }

        private void SubscribeToSession()
        {
            session.OnTimeUpdated += HandleTimeUpdate;
            session.OnGallonsBlockedChanged += HandleGallonsBlockedChange;
            session.OnGallonsEscapedChanged += HandleGallonsEscapedChange;
            session.OnItemsThrownChanged += HandleItemsThrownChange;
        }

        private void SubscribeToFutility()
        {
            // Subscribe to integrity changes from FutilityGameplaySystem
            FutilityGameplaySystem.OnIntegrityTierChanged += HandleIntegrityTierChange;
        }

        private void SubscribeToResupply()
        {
            // Subscribe to resupply events
            ResupplyManager.OnResupplySpawned += HandleResupplySpawned;
            ResupplyManager.OnResupplyPicked += HandleResupplyPicked;
        }

        // Session event handlers
        private void HandleTimeUpdate(float time)
        {
            timeElapsed = time;
            OnTimeUpdated?.Invoke(time);
        }

        private void HandleGallonsBlockedChange(int gallons)
        {
            gallonsBlocked = gallons;
            OnGallonsBlockedChanged?.Invoke(gallons);
        }

        private void HandleGallonsEscapedChange(int gallons)
        {
            gallonsEscaped = gallons;
            OnGallonsEscapedChanged?.Invoke(gallons);
        }

        private void HandleItemsThrownChange(int items)
        {
            // Track items thrown as degraded items for now
            // This could be refined later to track actual degraded items
            itemsDegraded = items;
        }

        // Futility event handler
        private void HandleIntegrityTierChange(int tier, string tierName, float currentIntegrity)
        {
            integrity = currentIntegrity;
            OnIntegrityChanged?.Invoke(currentIntegrity);

            // Log tier changes for debugging
            Debug.Log($"[ToastGameStateProvider] Integrity changed: {currentIntegrity:F1}% ({tierName})");
        }

        // Resupply event handlers
        private void HandleResupplySpawned(string message)
        {
            resupplyCount++;
            OnResupplyEvent?.Invoke();

            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[ToastGameStateProvider] Resupply spawned: {message}, Count now: {resupplyCount}");
            #endif
        }

        private void HandleResupplyPicked(string message)
        {
            // Note: We don't decrement resupplyCount as it tracks total spawned
            // We only fire OnResupplyEvent on spawn, not pickup (architectural decision)
            // This prevents double-triggering toasts for the same resupply event
            Debug.Log($"[ToastGameStateProvider] Resupply picked: {message}");
        }

        /// <summary>
        /// Reset all cached values and re-subscribe if needed
        /// </summary>
        public void Reset()
        {
            // Zero all cached values
            timeElapsed = 0f;
            gallonsBlocked = 0f;
            gallonsEscaped = 0f;
            itemsDegraded = 0;
            integrity = 100f;
            resupplyCount = 0;
            // Generate a new run seed on reset
            runSeed = UnityEngine.Random.Range(1000, 99999);

            Debug.Log("[ToastGameStateProvider] Reset to initial state");
        }

        /// <summary>
        /// Clean up event subscriptions
        /// </summary>
        public void Dispose()
        {
            // Unsubscribe from GameSession
            if (session != null)
            {
                session.OnTimeUpdated -= HandleTimeUpdate;
                session.OnGallonsBlockedChanged -= HandleGallonsBlockedChange;
                session.OnGallonsEscapedChanged -= HandleGallonsEscapedChange;
                session.OnItemsThrownChanged -= HandleItemsThrownChange;
            }

            // Unsubscribe from FutilityGameplaySystem
            FutilityGameplaySystem.OnIntegrityTierChanged -= HandleIntegrityTierChange;

            // Unsubscribe from ResupplyManager
            ResupplyManager.OnResupplySpawned -= HandleResupplySpawned;
            ResupplyManager.OnResupplyPicked -= HandleResupplyPicked;

            Debug.Log("[ToastGameStateProvider] Disposed and unsubscribed from all events");
        }

        /// <summary>
        /// IResettable implementation - returns true when all values are in initial state
        /// </summary>
        public bool IsClean => timeElapsed == 0f &&
                               gallonsBlocked == 0f &&
                               gallonsEscaped == 0f &&
                               itemsDegraded == 0 &&
                               integrity == 100f &&
                               resupplyCount == 0;

        // TODO: DevHUD Integration
        // - Add method to expose last N toast messages for DevHUD display
        // - Add method to return active trigger counts from ToastManager.GetDebugInfo()
        // - Consider adding toast statistics (total shown, queue depth, etc.)
    }
}