using UnityEngine;

/// <summary>
/// Migration adapter to bridge old OilLeakData ScriptableObject to new GameSession
/// This allows particle counting to work during migration
/// DELETE THIS after full migration
/// </summary>
public class OilLeakDataAdapter : MonoBehaviour
{
    // Reference to the old ScriptableObject (set in Inspector)
    [Header("Legacy References")]
    [SerializeField] private OilLeakData legacyOilLeakData;

    // Bridge to new system
    private GameSession session => GameCore.Session;

    // Update frequency
    private float updateInterval = 0.1f; // 10Hz updates
    private float lastUpdate = 0f;

    void Awake()
    {
        if (legacyOilLeakData == null)
        {
            Debug.LogError("OilLeakDataAdapter: No legacy OilLeakData assigned!");
            return;
        }

        Debug.LogWarning("OilLeakDataAdapter: TEMPORARY migration adapter active. Remove after refactor!");
    }

    void Update()
    {
        // Throttle updates for performance
        if (Time.time - lastUpdate < updateInterval)
            return;

        lastUpdate = Time.time;

        // Only sync if we have both systems
        if (legacyOilLeakData == null || session == null)
            return;

        // Sync data from new to old
        SyncToLegacy();
    }

    /// <summary>
    /// Sync new GameSession data to legacy OilLeakData
    /// </summary>
    private void SyncToLegacy()
    {
        // Map particle counts
        legacyOilLeakData.particlesBlocked = session.ParticlesBlocked;
        legacyOilLeakData.particlesEscaped = session.ParticlesEscaped;
    }

    /// <summary>
    /// Called when a particle collides with an item
    /// Routes to new system
    /// </summary>
    public void OnParticleCollisionFromItem()
    {
        session?.RecordParticleBlocked();
    }

    /// <summary>
    /// Called when a particle escapes
    /// Routes to new system
    /// </summary>
    public void OnParticleEscaped()
    {
        session?.RecordParticleEscaped();
    }

    /// <summary>
    /// Reset counts (called from old system)
    /// </summary>
    public void ResetCounts()
    {
        // New system handles this via session.Reset()
        Debug.Log("OilLeakDataAdapter: Reset requested (handled by GameSession)");
    }
}