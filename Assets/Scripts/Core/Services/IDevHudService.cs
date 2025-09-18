using UnityEngine;

namespace Core.Services
{
    /// <summary>
    /// Service interface for DevHUD updates
    /// Push-based updates instead of polling
    /// </summary>
    public interface IDevHudService : IResettable
    {
        /// <summary>
        /// Update session statistics (time, particles, score)
        /// </summary>
        void UpdateSessionStats(float timeElapsed, int particlesBlocked, int particlesEscaped, int maxEscaped);

        /// <summary>
        /// Update difficulty display
        /// </summary>
        void UpdateDifficulty(float emissionRate, float multiplier, float rubberBand);

        /// <summary>
        /// Update resupply countdown
        /// </summary>
        void UpdateResupplyStatus(bool isActive, float nextAirDropIn, float nextBargeIn, int activePackages);

        /// <summary>
        /// Update leak system status
        /// </summary>
        void UpdateLeakStatus(int activeParticles, float currentPressure, int activeLeaks);

        /// <summary>
        /// Update performance metrics
        /// </summary>
        void UpdatePerformance(float fps, int activeItems);

        /// <summary>
        /// Update game state info
        /// </summary>
        void UpdateGameState(GameFlowState state, bool isEndlessMode);

        /// <summary>
        /// Show/hide the HUD
        /// </summary>
        void SetVisible(bool visible);
    }
}