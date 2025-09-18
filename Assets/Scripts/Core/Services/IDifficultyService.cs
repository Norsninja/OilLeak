using UnityEngine;

namespace Core.Services
{
    /// <summary>
    /// Service interface for difficulty management
    /// Provides controlled access to difficulty curves and metrics
    /// </summary>
    public interface IDifficultyService
    {
        /// <summary>
        /// Set the difficulty curve for emission rates
        /// </summary>
        void SetEmissionCurve(AnimationCurve curve, float baseRate, float maxRate);

        /// <summary>
        /// Set the difficulty multiplier curve
        /// </summary>
        void SetMultiplierCurve(AnimationCurve curve);

        /// <summary>
        /// Update difficulty if interval has passed (2Hz tick)
        /// </summary>
        /// <returns>True if difficulty was updated</returns>
        bool TickIfDue();

        /// <summary>
        /// Report particles blocked for rubber band system
        /// </summary>
        void OnParticleBlocked(int count);

        /// <summary>
        /// Report particles escaped for rubber band system
        /// </summary>
        void OnParticleEscaped(int count);

        /// <summary>
        /// Reset difficulty to initial state
        /// </summary>
        void Reset();

        /// <summary>
        /// Set difficulty time for testing/debugging
        /// </summary>
        void SetDifficultyTime(float minutes);

        // Telemetry getters
        float GetCurrentEmissionRate();
        float GetCurrentMultiplier();
        float GetRubberBandAdjustment();
        float GetElapsedMinutes();

        /// <summary>
        /// Enable/disable rubber band difficulty adjustment
        /// </summary>
        void SetRubberBandEnabled(bool enabled);
    }
}