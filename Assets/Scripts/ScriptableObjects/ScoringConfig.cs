using UnityEngine;

/// <summary>
/// Configuration for the dynamic scoring system
/// Allows hot-swapping and A/B testing without code changes
/// </summary>
[CreateAssetMenu(fileName = "ScoringConfig", menuName = "OilLeak/ScoringConfig", order = 3)]
public class ScoringConfig : ScriptableObject
{
    [Header("Particle Blocking")]
    [Tooltip("Base points awarded per particle blocked (before multipliers)")]
    [Range(5, 100)]
    public int basePointsPerParticle = 20;

    [Tooltip("Time in seconds for each multiplier increment (60 = per minute)")]
    [Range(30f, 120f)]
    public float secondsPerMultiplier = 60f;

    [Tooltip("Maximum multiplier to prevent runaway scores")]
    [Range(5, 50)]
    public int maxMultiplier = 20;

    [Tooltip("Starting multiplier (usually 1)")]
    [Range(1, 5)]
    public int startingMultiplier = 1;

    [Header("Survival Bonus")]
    [Tooltip("Points awarded per second of survival")]
    [Range(0f, 20f)]
    public float pointsPerSecond = 5f;

    [Tooltip("Apply survival bonus continuously or only at end")]
    public bool continuousSurvivalBonus = true;

    [Header("Display")]
    [Tooltip("Show current block value on HUD")]
    public bool showCurrentBlockValue = true;

    [Tooltip("Format string for block value display")]
    public string blockValueFormat = "Next Block: {0} pts";

    [Tooltip("Show multiplier separately")]
    public bool showMultiplier = true;

    [Tooltip("Format string for multiplier display")]
    public string multiplierFormat = "Multiplier: {0}x";

    [Header("Difficulty Integration")]
    [Tooltip("Apply difficulty multiplier to block scores")]
    public bool useDifficultyMultiplier = false;

    [Tooltip("Bonus points for pressure burst clears")]
    [Range(0, 1000)]
    public int pressureBurstBonus = 250;

    /// <summary>
    /// Calculate the current score multiplier based on elapsed time
    /// </summary>
    public int GetMultiplier(float timeElapsed)
    {
        int multiplier = startingMultiplier + Mathf.FloorToInt(timeElapsed / secondsPerMultiplier);
        return Mathf.Min(multiplier, maxMultiplier);
    }

    /// <summary>
    /// Calculate points for a blocked particle at the given time
    /// </summary>
    public int CalculateBlockScore(float timeElapsed)
    {
        return basePointsPerParticle * GetMultiplier(timeElapsed);
    }

    /// <summary>
    /// Calculate survival bonus for elapsed time
    /// </summary>
    public int CalculateSurvivalBonus(float timeElapsed)
    {
        return Mathf.RoundToInt(timeElapsed * pointsPerSecond);
    }

    /// <summary>
    /// Format the block value for display
    /// </summary>
    public string FormatBlockValue(int value)
    {
        return string.Format(blockValueFormat, value);
    }

    /// <summary>
    /// Format the multiplier for display
    /// </summary>
    public string FormatMultiplier(int multiplier)
    {
        return string.Format(multiplierFormat, multiplier);
    }
}