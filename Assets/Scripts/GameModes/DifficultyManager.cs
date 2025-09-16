using UnityEngine;

/// <summary>
/// Manages difficulty progression for endless mode
/// Controls emission rates, particle caps, and difficulty multipliers
/// Updates at 2Hz for performance
/// </summary>
public class DifficultyManager : MonoBehaviour
{
    // Singleton instance for fast access from collision events
    public static DifficultyManager Instance { get; private set; }
    [Header("Difficulty Curves")]
    [SerializeField] private AnimationCurve emissionCurve = AnimationCurve.Linear(0, 5, 600, 50); // 5 to 50 over 10 minutes
    [SerializeField] private AnimationCurve difficultyMultiplierCurve = AnimationCurve.EaseInOut(0, 1, 600, 3); // 1x to 3x over 10 minutes

    [Header("Base Configuration")]
    [SerializeField] private float baseEmissionRate = 5f;
    [SerializeField] private float maxEmissionRate = 100f;
    [SerializeField] private float updateInterval = 0.5f; // 2Hz update rate

    [Header("Rubber Band System")]
    [SerializeField] private bool enableRubberBand = true;
    [SerializeField] private float rubberBandStrength = 0.2f; // How much to adjust based on performance
    [SerializeField] private float targetBlockPercentage = 0.6f; // Target 60% block rate
    [SerializeField] private float rubberBandSmoothTime = 5f; // Smooth changes over 5 seconds

    [Header("Particle Limits")]
    [SerializeField] private int maxParticlesWebGL = 500;
    [SerializeField] private int maxParticlesDesktop = 1000;

    [Header("Debug")]
    [SerializeField] private bool debugLogging = false;

    // References
    private LeakManager leakManager;
    private OilLeakData oilLeakData;

    // State tracking
    private float gameStartTime;
    private float lastUpdateTime;
    private float currentEmissionRate;
    private float currentMultiplier = 1f;
    private float rubberBandAdjustment = 1f;
    private float smoothedRubberBand = 1f;

    // Performance tracking for rubber band
    private int recentParticlesBlocked = 0;
    private int recentParticlesTotal = 0;
    private float performanceWindowStart;
    private float performanceWindowDuration = 10f; // Track performance over 10 seconds

    // Events
    public static event System.Action<float> OnDifficultyChanged;
    public static event System.Action<float> OnEmissionRateChanged;

    void Awake()
    {
        // Set up singleton
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        gameStartTime = Time.time;
        lastUpdateTime = Time.time;
        performanceWindowStart = Time.time;

        // Find references
        leakManager = FindObjectOfType<LeakManager>();
        if (leakManager == null)
        {
            Debug.LogWarning("DifficultyManager: LeakManager not found, creating one");
            GameObject leakManagerObj = new GameObject("LeakManager");
            leakManager = leakManagerObj.AddComponent<LeakManager>();
        }

        // Find OilLeakData
        OilController oilController = FindObjectOfType<OilController>();
        if (oilController != null)
        {
            oilLeakData = oilController.oilLeakData;
        }

        // Set particle cap based on platform
        #if UNITY_WEBGL
        SetParticleCap(maxParticlesWebGL);
        #else
        SetParticleCap(maxParticlesDesktop);
        #endif

        // Set initial emission rate
        currentEmissionRate = baseEmissionRate;
        ApplyEmissionRate();
    }

    void Update()
    {
        // Update at specified interval (2Hz default)
        if (Time.time - lastUpdateTime >= updateInterval)
        {
            UpdateDifficulty();
            lastUpdateTime = Time.time;
        }

        // Update performance tracking window
        if (Time.time - performanceWindowStart >= performanceWindowDuration)
        {
            UpdateRubberBand();
            ResetPerformanceWindow();
        }
    }

    private void UpdateDifficulty()
    {
        float elapsedTime = Time.time - gameStartTime;

        // Calculate base difficulty from curves
        float curveEmission = emissionCurve.Evaluate(elapsedTime);
        currentMultiplier = difficultyMultiplierCurve.Evaluate(elapsedTime);

        // Apply rubber band adjustment
        if (enableRubberBand)
        {
            smoothedRubberBand = Mathf.Lerp(smoothedRubberBand, rubberBandAdjustment, Time.deltaTime / rubberBandSmoothTime);
            curveEmission *= smoothedRubberBand;
        }

        // Calculate final emission rate
        currentEmissionRate = Mathf.Clamp(curveEmission, baseEmissionRate, maxEmissionRate);

        // Apply to leak manager
        ApplyEmissionRate();

        // Fire events
        OnDifficultyChanged?.Invoke(currentMultiplier);
        OnEmissionRateChanged?.Invoke(currentEmissionRate);

        if (debugLogging)
        {
            Debug.Log($"Difficulty Update - Time: {elapsedTime:F1}s, Emission: {currentEmissionRate:F1}, Multiplier: {currentMultiplier:F2}, RubberBand: {smoothedRubberBand:F2}");
        }
    }

    private void ApplyEmissionRate()
    {
        // Single source of truth: if LeakManager exists, it handles all emission
        if (leakManager != null)
        {
            leakManager.SetEmissionRate(currentEmissionRate);
        }
        else
        {
            // Fallback: update standalone OilControllers only if no LeakManager
            OilController[] controllers = FindObjectsOfType<OilController>();
            foreach (var controller in controllers)
            {
                if (controller.oilParticles != null)
                {
                    var emission = controller.oilParticles.emission;
                    emission.rateOverTime = currentEmissionRate;
                }
            }
        }
    }

    private void UpdateRubberBand()
    {
        if (!enableRubberBand || recentParticlesTotal == 0) return;

        float blockPercentage = (float)recentParticlesBlocked / recentParticlesTotal;
        float performanceDelta = blockPercentage - targetBlockPercentage;

        // If player is blocking too much, increase difficulty
        // If player is struggling, decrease difficulty
        rubberBandAdjustment = 1f - (performanceDelta * rubberBandStrength);
        rubberBandAdjustment = Mathf.Clamp(rubberBandAdjustment, 0.5f, 1.5f); // Limit adjustment range

        if (debugLogging)
        {
            Debug.Log($"Rubber Band - Block%: {blockPercentage:P}, Target: {targetBlockPercentage:P}, Adjustment: {rubberBandAdjustment:F2}");
        }
    }

    private void ResetPerformanceWindow()
    {
        recentParticlesBlocked = 0;
        recentParticlesTotal = 0;
        performanceWindowStart = Time.time;
    }

    private void SetParticleCap(int maxParticles)
    {
        ParticleSystem[] allParticleSystems = FindObjectsOfType<ParticleSystem>();
        foreach (var ps in allParticleSystems)
        {
            // Check by name only since OilSpill tag doesn't exist
            if (ps.name.Contains("Oil") || ps.gameObject.layer == LayerMask.NameToLayer("OilSpill"))
            {
                var main = ps.main;
                main.maxParticles = maxParticles;
            }
        }

        Debug.Log($"Particle cap set to {maxParticles}");
    }

    // Called by game systems to track performance
    public void OnParticleBlocked(int count)
    {
        recentParticlesBlocked += count;
        recentParticlesTotal += count;

        // Also notify leak manager for pressure system
        if (leakManager != null)
        {
            leakManager.OnParticleBlocked(count);
        }
    }

    public void OnParticleEscaped(int count)
    {
        recentParticlesTotal += count;
    }

    // Public getters
    public float GetCurrentEmissionRate() => currentEmissionRate;
    public float GetCurrentMultiplier() => currentMultiplier;
    public float GetRubberBandAdjustment() => smoothedRubberBand;
    public float GetElapsedMinutes() => (Time.time - gameStartTime) / 60f;

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    // Reset for new game
    public void ResetDifficulty()
    {
        gameStartTime = Time.time;
        lastUpdateTime = Time.time;
        performanceWindowStart = Time.time;
        currentEmissionRate = baseEmissionRate;
        currentMultiplier = 1f;
        rubberBandAdjustment = 1f;
        smoothedRubberBand = 1f;
        ResetPerformanceWindow();

        ApplyEmissionRate();

        // LeakManager handles its own reset as part of StartRun()
        // DifficultyManager only manages emission rates, not leak lifecycle
    }

    // For testing - force difficulty level
    public void SetDifficultyTime(float minutes)
    {
        gameStartTime = Time.time - (minutes * 60f);
        UpdateDifficulty();
    }
}