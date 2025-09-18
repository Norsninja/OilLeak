using UnityEngine;

/// <summary>
/// Service interface for oil leak management
/// OWNS all ParticleSystem lifecycle - no one else touches particles
/// </summary>
public interface ILeakService : IResettable
{
    /// <summary>
    /// Start oil leaks for gameplay
    /// </summary>
    void StartLeaks();

    /// <summary>
    /// Pause all leaks (maintains state)
    /// </summary>
    void PauseLeaks();

    /// <summary>
    /// Resume paused leaks
    /// </summary>
    void ResumeLeaks();

    /// <summary>
    /// End leak run (stop emission but keep particles)
    /// </summary>
    void EndLeaks();

    /// <summary>
    /// Set total emission rate across all leaks
    /// Called by DifficultyManager only
    /// </summary>
    void SetTotalEmissionRate(float particlesPerSecond);

    /// <summary>
    /// Immediately clear all particles
    /// Used during Cleaning state
    /// </summary>
    void Clear();

    /// <summary>
    /// Initialize menu state (ambient oil, no collisions)
    /// </summary>
    void InitializeMenuState();

    /// <summary>
    /// Get current number of active leaks
    /// </summary>
    int ActiveLeakCount { get; }

    /// <summary>
    /// Get total particle count across all systems
    /// Performance: Cached, not calculated per-frame
    /// </summary>
    int TotalParticleCount { get; }

    /// <summary>
    /// Get pressure percentage for burst mechanics
    /// </summary>
    float PressurePercentage { get; }
}