using UnityEngine;
using Core.Services;

/// <summary>
/// Refactored DevHUD that receives data from DevHudAdapter
/// No more polling or FindObjectOfType - pure display component
/// </summary>
public class DevHUD : MonoBehaviour
{
    public bool showDevHUD = true;

    private DevHudAdapter adapter;
    private GUIStyle headerStyle;
    private GUIStyle style;

    // Performance tracking
    private float deltaTime = 0.0f;
    private float updateInterval = 0.5f;
    private float timeSinceUpdate = 0.0f;
    private int frameCount = 0;

    void Start()
    {
        Debug.Log("DevHUD: Created");
    }

    /// <summary>
    /// Called by DevHudAdapter to establish connection
    /// </summary>
    public void SetAdapter(DevHudAdapter adapter)
    {
        this.adapter = adapter;
        Debug.Log("[DevHUD] Connected to adapter");
    }

    void Update()
    {
        // Calculate FPS
        deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;
        timeSinceUpdate += Time.unscaledDeltaTime;
        frameCount++;

        if (timeSinceUpdate >= updateInterval)
        {
            float fps = frameCount / timeSinceUpdate;

            // Push FPS to adapter if connected
            if (adapter != null)
            {
                var leakData = adapter.GetLeakData();
                adapter.UpdatePerformance(fps, 0); // ActiveItems handled by adapter
            }

            frameCount = 0;
            timeSinceUpdate = 0.0f;
        }

        // Toggle with F3
        if (Input.GetKeyDown(KeyCode.F3))
        {
            showDevHUD = !showDevHUD;
        }
    }

    void OnGUI()
    {
        if (!showDevHUD || adapter == null) return;

        // Initialize styles
        if (style == null)
        {
            style = new GUIStyle(GUI.skin.label);
            style.fontSize = 12;
            style.normal.textColor = Color.white;

            headerStyle = new GUIStyle(style);
            headerStyle.fontStyle = FontStyle.Bold;
        }

        // Get data from adapter
        var sessionData = adapter.GetSessionData();
        var difficultyData = adapter.GetDifficultyData();
        var resupplyData = adapter.GetResupplyData();
        var leakData = adapter.GetLeakData();
        var perfData = adapter.GetPerformanceData();
        var stateData = adapter.GetStateData();

        // Calculate background size
        int totalHeight = 750; // Increased for new integrity and scoring sections

        // Background box
        GUI.Box(new Rect(10, 10, 340, totalHeight), "");
        GUI.Label(new Rect(15, 10, 330, 25), "=== DEV HUD (F3 to toggle) ===", headerStyle);

        int y = 35;
        int lineHeight = 20;
        int sectionSpacing = 5;

        // === PERFORMANCE SECTION ===
        GUI.Label(new Rect(15, y, 330, lineHeight), "-- Performance --", headerStyle);
        y += lineHeight;

        style.normal.textColor = perfData.fps > 30 ? Color.green : (perfData.fps > 20 ? Color.yellow : Color.red);
        GUI.Label(new Rect(15, y, 330, lineHeight), $"FPS: {perfData.fps:F1}", style);
        y += lineHeight;

        style.normal.textColor = Color.white;
        GUI.Label(new Rect(15, y, 330, lineHeight), $"Active Particles: {leakData.activeParticles}", style);
        y += lineHeight;

        GUI.Label(new Rect(15, y, 330, lineHeight), $"Active Items: {perfData.activeItems}", style);
        y += lineHeight;
        y += sectionSpacing;

        // === GAME STATE SECTION ===
        style.normal.textColor = Color.cyan;
        GUI.Label(new Rect(15, y, 330, lineHeight), "-- Game State --", headerStyle);
        y += lineHeight;
        style.normal.textColor = Color.white;

        // Time
        int minutes = Mathf.FloorToInt(sessionData.timeElapsed / 60);
        int seconds = Mathf.FloorToInt(sessionData.timeElapsed % 60);
        GUI.Label(new Rect(15, y, 330, lineHeight), $"Time: {minutes:00}:{seconds:00}", style);
        y += lineHeight;

        // State
        GUI.Label(new Rect(15, y, 330, lineHeight), $"State: {stateData.currentState}", style);
        y += lineHeight;

        // Mode
        style.normal.textColor = stateData.isEndlessMode ? Color.green : Color.gray;
        GUI.Label(new Rect(15, y, 330, lineHeight), $"Mode: {(stateData.isEndlessMode ? "Endless (Futility)" : "Round")}", style);
        y += lineHeight;
        style.normal.textColor = Color.white;
        y += sectionSpacing;

        // === OIL STATUS SECTION ===
        style.normal.textColor = Color.cyan;
        GUI.Label(new Rect(15, y, 330, lineHeight), "-- Oil Status --", headerStyle);
        y += lineHeight;
        style.normal.textColor = Color.white;

        // Blocked
        GUI.Label(new Rect(15, y, 330, lineHeight),
            $"Blocked: {sessionData.particlesBlocked} ({sessionData.particlesBlocked * 100:N0} gal)", style);
        y += lineHeight;

        // Escaped with threshold
        style.normal.textColor = sessionData.escapedPercent > 95 ? Color.red :
                               sessionData.escapedPercent > 80 ? Color.yellow : Color.white;
        GUI.Label(new Rect(15, y, 330, lineHeight),
            $"Escaped: {sessionData.particlesEscaped}/{sessionData.maxEscaped} ({sessionData.escapedPercent:F1}%)", style);
        y += lineHeight;
        style.normal.textColor = Color.white;
        y += sectionSpacing;

        // === INTEGRITY SECTION ===
        style.normal.textColor = Color.cyan;
        GUI.Label(new Rect(15, y, 330, lineHeight), "-- Ocean Integrity --", headerStyle);
        y += lineHeight;

        // Tier with color coding
        Color integrityColor = sessionData.integrityTier switch
        {
            5 => Color.green,     // Pristine
            4 => Color.yellow,    // Stable
            3 => new Color(1f, 0.5f, 0f), // Damaged (orange)
            2 => Color.red,       // Critical
            1 => Color.magenta,   // Failing
            _ => Color.gray       // Collapsed
        };
        style.normal.textColor = integrityColor;
        GUI.Label(new Rect(15, y, 330, lineHeight),
            $"Tier: {sessionData.integrityTierName} ({sessionData.integrityPercent:F0}%)", style);
        y += lineHeight;
        style.normal.textColor = Color.white;
        y += sectionSpacing;

        // === SCORING SECTION ===
        style.normal.textColor = Color.cyan;
        GUI.Label(new Rect(15, y, 330, lineHeight), "-- Scoring --", headerStyle);
        y += lineHeight;
        style.normal.textColor = Color.white;

        GUI.Label(new Rect(15, y, 330, lineHeight),
            $"Block Value: {sessionData.currentBlockValue} x{sessionData.scoreMultiplier}", style);
        y += lineHeight;

        GUI.Label(new Rect(15, y, 330, lineHeight),
            $"Block Score: {sessionData.runningBlockScore:N0}", style);
        y += lineHeight;

        GUI.Label(new Rect(15, y, 330, lineHeight),
            $"Time Bonus: {sessionData.survivalBonus:N0}", style);
        y += lineHeight;

        style.normal.textColor = Color.yellow;
        GUI.Label(new Rect(15, y, 330, lineHeight),
            $"Total Score: {sessionData.totalScore:N0}", style);
        y += lineHeight;
        style.normal.textColor = Color.white;
        y += sectionSpacing;

        // === LEAK SYSTEM SECTION ===
        style.normal.textColor = Color.cyan;
        GUI.Label(new Rect(15, y, 330, lineHeight), "-- Leak System --", headerStyle);
        y += lineHeight;
        style.normal.textColor = Color.white;

        GUI.Label(new Rect(15, y, 330, lineHeight), $"Active Leaks: {leakData.activeLeaks}", style);
        y += lineHeight;

        GUI.Label(new Rect(15, y, 330, lineHeight), $"Pressure: {leakData.currentPressure:F1}%", style);
        y += lineHeight;
        y += sectionSpacing;

        // === DIFFICULTY SECTION ===
        style.normal.textColor = Color.cyan;
        GUI.Label(new Rect(15, y, 330, lineHeight), "-- Difficulty --", headerStyle);
        y += lineHeight;
        style.normal.textColor = Color.white;

        GUI.Label(new Rect(15, y, 330, lineHeight), $"Emission: {difficultyData.emissionRate:F1}/s", style);
        y += lineHeight;

        GUI.Label(new Rect(15, y, 330, lineHeight), $"Multiplier: {difficultyData.multiplier:F2}x", style);
        y += lineHeight;

        GUI.Label(new Rect(15, y, 330, lineHeight), $"Rubber Band: {difficultyData.rubberBand:F2}x", style);
        y += lineHeight;
        y += sectionSpacing;

        // === RESUPPLY SECTION ===
        style.normal.textColor = Color.cyan;
        GUI.Label(new Rect(15, y, 330, lineHeight), "-- Resupply --", headerStyle);
        y += lineHeight;
        style.normal.textColor = Color.white;

        style.normal.textColor = resupplyData.isActive ? Color.green : Color.gray;
        GUI.Label(new Rect(15, y, 330, lineHeight), $"Status: {(resupplyData.isActive ? "Active" : "Inactive")}", style);
        y += lineHeight;
        style.normal.textColor = Color.white;

        if (resupplyData.nextAirDropIn > 0)
        {
            GUI.Label(new Rect(15, y, 330, lineHeight), $"Next Air-Drop: {resupplyData.nextAirDropIn:F0}s", style);
            y += lineHeight;
        }
        else if (resupplyData.isActive)
        {
            style.normal.textColor = Color.yellow;
            GUI.Label(new Rect(15, y, 330, lineHeight), "Air-Drop: Ready!", style);
            y += lineHeight;
            style.normal.textColor = Color.white;
        }

        if (resupplyData.nextBargeIn > 0 && resupplyData.nextBargeIn < 300)
        {
            GUI.Label(new Rect(15, y, 330, lineHeight), $"Next Barge: {resupplyData.nextBargeIn:F0}s", style);
            y += lineHeight;
        }

        if (resupplyData.activePackages > 0)
        {
            style.normal.textColor = Color.green;
            GUI.Label(new Rect(15, y, 330, lineHeight), $"Active Packages: {resupplyData.activePackages}", style);
            y += lineHeight;
            style.normal.textColor = Color.white;
        }
    }
}