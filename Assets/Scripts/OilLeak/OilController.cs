using UnityEngine;

/// <summary>
/// Minimal ParticleSystem actuator for oil leaks
/// Only handles direct particle system control - no game logic
/// LeakManager is the sole authority that controls this
/// </summary>
public class OilController : MonoBehaviour
{
    // References
    public OilLeakData oilLeakData; // For counters only - does not drive emission
    public ParticleSystem oilParticles;

    // Cached modules for performance
    private ParticleSystem.MainModule mainModule;
    private ParticleSystem.EmissionModule emissionModule;
    private ParticleSystem.CollisionModule collisionModule;

    void Awake()
    {
        // Cache ParticleSystem and modules
        oilParticles = GetComponent<ParticleSystem>();
        if (oilParticles != null)
        {
            mainModule = oilParticles.main;
            emissionModule = oilParticles.emission;
            collisionModule = oilParticles.collision;
        }
        else
        {
            Debug.LogError($"OilController on {gameObject.name} has no ParticleSystem!");
        }
    }

    /// <summary>
    /// Set the emission rate
    /// </summary>
    public void ApplyEmission(float rate)
    {
        if (oilParticles == null) return;
        emissionModule.rateOverTime = rate;
    }

    /// <summary>
    /// Enable or disable emission
    /// </summary>
    public void EnableEmission(bool on)
    {
        if (oilParticles == null) return;
        emissionModule.enabled = on;

        if (on)
        {
            oilParticles.Play();
        }
        else
        {
            // Stop emitting but keep existing particles
            oilParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    /// <summary>
    /// Enable or disable collisions with specified layer mask
    /// </summary>
    public void EnableCollisions(bool on, LayerMask mask = default)
    {
        if (oilParticles == null) return;

        collisionModule.enabled = on;

        // Only set layer mask if provided and collisions are being enabled
        if (on && mask != default)
        {
            collisionModule.collidesWith = mask;
        }
    }

    /// <summary>
    /// Reset the oil system - clears particles and resets counters
    /// Does NOT re-enable emission - LeakManager decides that
    /// </summary>
    public void ResetOilSystem()
    {
        if (oilParticles != null)
        {
            // Stop and clear all particles
            oilParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            oilParticles.Clear();

            // Leave emission disabled - LeakManager will enable when needed
            emissionModule.enabled = false;
        }

        // Reset counts if we have OilLeakData
        if (oilLeakData != null)
        {
            oilLeakData.ResetCounts();
        }
    }

    /// <summary>
    /// Get current particle count (for DevHUD aggregation)
    /// </summary>
    public int GetActiveParticleCount()
    {
        return oilParticles != null ? oilParticles.particleCount : 0;
    }

    /// <summary>
    /// Pause particle simulation (for pause state)
    /// </summary>
    public void PauseSimulation()
    {
        if (oilParticles != null)
        {
            oilParticles.Pause();
        }
    }

    /// <summary>
    /// Resume particle simulation (from pause)
    /// </summary>
    public void ResumeSimulation()
    {
        if (oilParticles != null)
        {
            oilParticles.Play();
        }
    }
}