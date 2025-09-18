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

    // Delta tracking for bi-directional sync
    private int lastBlockedCount = 0;
    private int lastEscapedCount = 0;
    private bool syncing = false; // Prevent sync loops during updates

    void Awake()
    {
        if (legacyOilLeakData == null)
        {
            Debug.LogWarning("OilLeakDataAdapter: No legacy OilLeakData assigned yet. Will be set via reflection or Inspector.");
            // Don't return - it might be set later
        }
        else
        {
            Debug.LogWarning("OilLeakDataAdapter: TEMPORARY migration adapter active with bi-directional sync. Remove after refactor!");
            // Initialize baseline counts
            lastBlockedCount = legacyOilLeakData.particlesBlocked;
            lastEscapedCount = legacyOilLeakData.particlesEscaped;
        }
    }

    void OnEnable()
    {
        // Re-initialize baseline counts when enabled (handles scene reloads)
        if (legacyOilLeakData != null)
        {
            lastBlockedCount = legacyOilLeakData.particlesBlocked;
            lastEscapedCount = legacyOilLeakData.particlesEscaped;
            Debug.Log($"[OilLeakDataAdapter] OnEnable - Baseline counts: Blocked={lastBlockedCount}, Escaped={lastEscapedCount}");
        }
    }


    void Update()
    {
        // Throttle updates for performance
        if (Time.time - lastUpdate < updateInterval)
            return;

        lastUpdate = Time.time;

        // Only sync if we have both systems and not already syncing
        if (legacyOilLeakData == null || session == null || syncing)
            return;

        syncing = true;

        // Bi-directional sync: Detect changes in legacy system
        if (legacyOilLeakData.particlesBlocked != lastBlockedCount)
        {
            int delta = legacyOilLeakData.particlesBlocked - lastBlockedCount;
            
            if (delta > 0)
            {
                // Legacy system incremented (ItemController/RagdollController collision)
                Debug.Log($"[OilLeakDataAdapter] Detected {delta} blocked particles from legacy system, forwarding to GameSession");
                for (int i = 0; i < delta; i++)
                {
                    session.RecordParticleBlocked();
                }
            }
            else if (delta < 0)
            {
                // Negative delta means reset occurred
                Debug.Log("[OilLeakDataAdapter] Detected reset in legacy system");
                // Don't call session.Reset() here as it might already be resetting
            }
            
            lastBlockedCount = legacyOilLeakData.particlesBlocked;
        }

        // Same for escaped particles
        if (legacyOilLeakData.particlesEscaped != lastEscapedCount)
        {
            int delta = legacyOilLeakData.particlesEscaped - lastEscapedCount;
            
            if (delta > 0)
            {
                // Legacy system incremented (WaterSurfaceController collision)
                Debug.Log($"[OilLeakDataAdapter] Detected {delta} escaped particles from legacy system, forwarding to GameSession");
                for (int i = 0; i < delta; i++)
                {
                    session.RecordParticleEscaped();
                }
            }
            
            lastEscapedCount = legacyOilLeakData.particlesEscaped;
        }

        // Sync from new to old (for UI that still reads legacy)
        SyncToLegacy();
        
        syncing = false;
    }

    /// <summary>
    /// Sync new GameSession data to legacy OilLeakData
    /// </summary>
    private void SyncToLegacy()
    {
        // Only update legacy if values differ (avoid unnecessary writes)
        if (legacyOilLeakData.particlesBlocked != session.ParticlesBlocked)
        {
            legacyOilLeakData.particlesBlocked = session.ParticlesBlocked;
        }
        
        if (legacyOilLeakData.particlesEscaped != session.ParticlesEscaped)
        {
            legacyOilLeakData.particlesEscaped = session.ParticlesEscaped;
        }
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
        // Reset our tracking when counts are reset
        lastBlockedCount = 0;
        lastEscapedCount = 0;
        Debug.Log("[OilLeakDataAdapter] Reset requested - clearing baseline counts");
    }
}