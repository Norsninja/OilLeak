using UnityEngine;

namespace Core.Services
{
    /// <summary>
    /// Adapter wrapping LeakManager to provide ILeakService interface
    /// Handles interface mismatches and takes ownership of total emission budget
    /// </summary>
    public class LeakManagerAdapter : ILeakService
    {
        private readonly LeakManager wrapped;

        public LeakManagerAdapter(LeakManager manager)
        {
            wrapped = manager ?? throw new System.ArgumentNullException(nameof(manager));
        }

        // === ILeakService Implementation ===

        /// <summary>
        /// Start oil leaks for gameplay
        /// </summary>
        public void StartLeaks()
        {
            wrapped.StartRun();
        }

        /// <summary>
        /// Pause all leaks (maintains state)
        /// </summary>
        public void PauseLeaks()
        {
            wrapped.PauseRun();
        }

        /// <summary>
        /// Resume paused leaks
        /// </summary>
        public void ResumeLeaks()
        {
            wrapped.ResumeRun();
        }

        /// <summary>
        /// End leak run (stop emission but keep particles)
        /// </summary>
        public void EndLeaks()
        {
            wrapped.EndRun();
        }

        /// <summary>
        /// Set total emission rate across all leaks
        /// Takes ownership of the total emission budget
        /// </summary>
        public void SetTotalEmissionRate(float particlesPerSecond)
        {
            // Adapter owns the concept of "total" - LeakManager.SetEmissionRate already handles distribution
            wrapped.SetEmissionRate(particlesPerSecond);
        }

        /// <summary>
        /// Immediately clear all particles
        /// </summary>
        public void Clear()
        {
            wrapped.StopAndClear();
        }

        /// <summary>
        /// Initialize menu state (ambient oil, no collisions)
        /// </summary>
        public void InitializeMenuState()
        {
            wrapped.InitializeMenuState();
        }

        /// <summary>
        /// Get current number of active leaks
        /// </summary>
        public int ActiveLeakCount => wrapped.GetActiveLeakCount();

        /// <summary>
        /// Get total particle count across all systems
        /// </summary>
        public int TotalParticleCount => wrapped.GetTotalActiveParticles();

        /// <summary>
        /// Get pressure percentage for burst mechanics
        /// </summary>
        public float PressurePercentage => wrapped.GetPressurePercentage();

        // === IResettable Implementation (forward to wrapped) ===

        /// <summary>
        /// Reset to initial state
        /// </summary>
        public void Reset()
        {
            wrapped.Reset();
        }

        /// <summary>
        /// Verify the service is properly cleaned
        /// </summary>
        public bool IsClean => wrapped.IsClean;

        // === Additional Helper Methods ===

        /// <summary>
        /// Trigger a pressure burst manually (for testing/events)
        /// </summary>
        public void TriggerPressureBurst()
        {
            // LeakManager doesn't expose this publicly yet, but we could add it if needed
            Debug.Log("[LeakManagerAdapter] TriggerPressureBurst not yet implemented in LeakManager");
        }

        /// <summary>
        /// Check if currently in burst state
        /// </summary>
        public bool IsBursting => wrapped.IsBursting();

        /// <summary>
        /// Get current state for debugging
        /// </summary>
        public LeakManagerState CurrentState => wrapped.CurrentState;
    }
}