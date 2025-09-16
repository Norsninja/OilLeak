using UnityEngine;

/// <summary>
/// Configuration for game rules - failure/victory conditions
/// Keeps the game data-driven and easily tunable
/// </summary>
[CreateAssetMenu(fileName = "GameRulesConfig", menuName = "OilLeak/GameRulesConfig", order = 10)]
public class GameRulesConfig : ScriptableObject
{
    [Header("Failure Conditions")]
    [Tooltip("Maximum particles that can escape before game over")]
    public int maxEscapedParticles = 1000; // Default allows 1000 particles to escape

    [Tooltip("Percentage of max escaped to trigger warning (0.8 = 80%)")]
    [Range(0.5f, 0.95f)]
    public float nearFailWarnPercent = 0.8f;

    [Header("Display Settings")]
    [Tooltip("Gallons per particle for UI display")]
    public int gallonsPerParticle = 100;

    [Header("Difficulty Tuning")]
    [Tooltip("Enable dynamic difficulty based on performance")]
    public bool enableDynamicDifficulty = true;

    [Tooltip("Failure threshold can increase with time survived")]
    public bool scaleFailureThreshold = false;

    [Tooltip("Additional particles allowed per minute if scaling")]
    public float particlesPerMinuteScaling = 10f;

    // Helper methods
    public int GetScaledMaxEscaped(float timeElapsed)
    {
        if (!scaleFailureThreshold) return maxEscapedParticles;

        float minutes = timeElapsed / 60f;
        int bonus = Mathf.FloorToInt(minutes * particlesPerMinuteScaling);
        return maxEscapedParticles + bonus;
    }

    public int GetWarningThreshold(float timeElapsed)
    {
        int max = GetScaledMaxEscaped(timeElapsed);
        return Mathf.FloorToInt(max * nearFailWarnPercent);
    }

    public int GetGallonsEscaped(int particlesEscaped)
    {
        return particlesEscaped * gallonsPerParticle;
    }

    public int GetMaxGallons(float timeElapsed)
    {
        return GetScaledMaxEscaped(timeElapsed) * gallonsPerParticle;
    }

    public float GetEscapedPercentage(int particlesEscaped, float timeElapsed)
    {
        int max = GetScaledMaxEscaped(timeElapsed);
        if (max <= 0) return 0f;
        return (float)particlesEscaped / max;
    }
}