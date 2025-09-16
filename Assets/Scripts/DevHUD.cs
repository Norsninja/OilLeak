using UnityEngine;
using System.Linq;

public class DevHUD : MonoBehaviour
{
    // References (wire in Inspector or find at runtime)
    public GameController gameController;
    public LeakManager leakManager;
    public DifficultyManager difficultyManager;
    public OilLeakData oilLeakData;
    public GameRulesConfig gameRules;
    public bool showDevHUD = true;

    // Performance tracking
    private float deltaTime = 0.0f;
    private int frameCount = 0;
    private float fps = 0.0f;
    private float updateInterval = 0.5f; // Update at 2Hz
    private float timeSinceUpdate = 0.0f;

    // Stats tracking for windows
    private float windowStartTime;
    private int blockedInWindow;
    private int escapedInWindow;
    private float windowDuration = 10f; // 10 second window

    // Additional references
    private ResupplyManager resupplyManager;

    void Start()
    {
        // Auto-find references if not set
        if (gameController == null)
            gameController = FindObjectOfType<GameController>();
        if (leakManager == null)
            leakManager = LeakManager.Instance ?? FindObjectOfType<LeakManager>();
        if (difficultyManager == null)
        {
            difficultyManager = DifficultyManager.Instance ?? FindObjectOfType<DifficultyManager>();
            // Create DifficultyManager if it doesn't exist (important for singleton pattern)
            if (difficultyManager == null)
            {
                GameObject managerObj = new GameObject("DifficultyManager");
                difficultyManager = managerObj.AddComponent<DifficultyManager>();
                Debug.Log("DevHUD: Created DifficultyManager instance");
            }
        }
        if (oilLeakData == null && gameController != null)
            oilLeakData = gameController.oilLeakData;
        if (gameRules == null)
            gameRules = Resources.Load<GameRulesConfig>("GameRulesConfig");

        resupplyManager = FindObjectOfType<ResupplyManager>();

        windowStartTime = Time.time;
    }

    void Update()
    {
        // Toggle with F3
        if (Input.GetKeyDown(KeyCode.F3))
            showDevHUD = !showDevHUD;

        // Calculate FPS using unscaled time
        deltaTime += Time.unscaledDeltaTime;
        frameCount++;
        timeSinceUpdate += Time.unscaledDeltaTime;

        if (timeSinceUpdate >= updateInterval)
        {
            fps = frameCount / deltaTime;
            frameCount = 0;
            deltaTime = 0.0f;
            timeSinceUpdate = 0.0f;
        }

        // Update window stats
        if (Time.time - windowStartTime >= windowDuration)
        {
            windowStartTime = Time.time;
            blockedInWindow = 0;
            escapedInWindow = 0;
        }
    }

    void OnGUI()
    {
        if (!showDevHUD) return;

        // Style setup
        GUIStyle style = new GUIStyle();
        style.normal.textColor = Color.white;
        style.fontSize = 14;

        GUIStyle headerStyle = new GUIStyle(style);
        headerStyle.fontStyle = FontStyle.Bold;
        headerStyle.normal.textColor = Color.cyan;

        // Calculate dynamic height based on content
        int sectionCount = 6; // Performance, Leak, Difficulty, Resupply, Game Stats, Mode
        int linesPerSection = 7; // Average lines per section
        int totalHeight = 35 + (sectionCount * linesPerSection * 20) + 30; // Header + content + padding

        // Background box with calculated height
        GUI.Box(new Rect(10, 10, 340, totalHeight), "");
        GUI.Label(new Rect(15, 10, 330, 25), "=== DEV HUD (F3 to toggle) ===", headerStyle);

        int y = 35;
        int lineHeight = 20;
        int sectionSpacing = 5;

        // === PERFORMANCE SECTION ===
        GUI.Label(new Rect(15, y, 330, lineHeight), "-- Performance --", headerStyle);
        y += lineHeight;

        style.normal.textColor = fps > 30 ? Color.green : (fps > 20 ? Color.yellow : Color.red);
        GUI.Label(new Rect(15, y, 330, lineHeight), $"FPS: {fps:F1}", style);
        y += lineHeight;

        if (leakManager != null)
        {
            GUI.Label(new Rect(15, y, 330, lineHeight), $"Active Particles: {leakManager.GetTotalActiveParticles()}", style);
            y += lineHeight;
        }
        y += sectionSpacing;

        // === GAME STATE SECTION ===
        style.normal.textColor = Color.cyan;
        GUI.Label(new Rect(15, y, 330, lineHeight), "-- Game State --", headerStyle);
        y += lineHeight;
        style.normal.textColor = Color.white;

        if (gameController != null && gameController.gameState != null)
        {
            var gameState = gameController.gameState;

            // Time
            int minutes = Mathf.FloorToInt(gameState.timer / 60);
            int seconds = Mathf.FloorToInt(gameState.timer % 60);
            GUI.Label(new Rect(15, y, 330, lineHeight), $"Time: {minutes:00}:{seconds:00}", style);
            y += lineHeight;

            // Score
            GUI.Label(new Rect(15, y, 330, lineHeight), $"Score: {gameState.score:N0}", style);
            y += lineHeight;

            // Round State
            GUI.Label(new Rect(15, y, 330, lineHeight), $"State: {gameState.roundState}", style);
            y += lineHeight;

            // Endless Mode
            bool endlessMode = gameController.useEndlessMode;
            style.normal.textColor = endlessMode ? Color.green : Color.gray;
            GUI.Label(new Rect(15, y, 330, lineHeight), $"Mode: {(endlessMode ? "Endless (Futility)" : "Round")}", style);
            y += lineHeight;
            style.normal.textColor = Color.white;
        }
        y += sectionSpacing;

        // === OIL STATUS SECTION ===
        style.normal.textColor = Color.cyan;
        GUI.Label(new Rect(15, y, 330, lineHeight), "-- Oil Status --", headerStyle);
        y += lineHeight;
        style.normal.textColor = Color.white;

        if (oilLeakData != null && gameRules != null && gameController?.gameState != null)
        {
            // Blocked
            GUI.Label(new Rect(15, y, 330, lineHeight), $"Blocked: {oilLeakData.particlesBlocked} ({oilLeakData.particlesBlocked * 100:N0} gal)", style);
            y += lineHeight;

            // Escaped with threshold
            float timeElapsed = gameController.gameState.timer;
            int maxEscaped = gameRules.GetScaledMaxEscaped(timeElapsed);
            float escapedPercent = gameRules.GetEscapedPercentage(oilLeakData.particlesEscaped, timeElapsed) * 100f;

            style.normal.textColor = escapedPercent > 95 ? Color.red :
                                    escapedPercent > 80 ? Color.yellow : Color.white;
            GUI.Label(new Rect(15, y, 330, lineHeight),
                $"Escaped: {oilLeakData.particlesEscaped}/{maxEscaped} ({escapedPercent:F1}%)", style);
            y += lineHeight;
            style.normal.textColor = Color.white;

            // Rates
            float windowTime = Time.time - windowStartTime;
            if (windowTime > 0)
            {
                float blockRate = blockedInWindow / windowTime;
                float escapeRate = escapedInWindow / windowTime;
                GUI.Label(new Rect(15, y, 330, lineHeight), $"Rates (10s): Block {blockRate:F1}/s, Escape {escapeRate:F1}/s", style);
                y += lineHeight;
            }
        }
        y += sectionSpacing;

        // === LEAK MANAGER SECTION ===
        style.normal.textColor = Color.cyan;
        GUI.Label(new Rect(15, y, 330, lineHeight), "-- Leak System --", headerStyle);
        y += lineHeight;

        if (leakManager != null)
        {
            // State
            var state = leakManager.CurrentState;
            style.normal.textColor = state == LeakManagerState.Running ? Color.green :
                                    state == LeakManagerState.Paused ? Color.yellow :
                                    state == LeakManagerState.Menu ? Color.cyan : Color.gray;
            GUI.Label(new Rect(15, y, 330, lineHeight), $"State: {state}", style);
            y += lineHeight;
            style.normal.textColor = Color.white;

            // Leaks
            GUI.Label(new Rect(15, y, 330, lineHeight), $"Active Leaks: {leakManager.GetActiveLeakCount()}", style);
            y += lineHeight;

            // Pressure
            float pressurePercent = leakManager.GetPressurePercentage() * 100f;
            style.normal.textColor = pressurePercent > 80 ? Color.red :
                                    pressurePercent > 50 ? Color.yellow : Color.white;
            GUI.Label(new Rect(15, y, 330, lineHeight), $"Pressure: {pressurePercent:F1}%", style);
            y += lineHeight;

            // Burst
            style.normal.textColor = leakManager.IsBursting() ? Color.red : Color.white;
            GUI.Label(new Rect(15, y, 330, lineHeight), $"Burst: {(leakManager.IsBursting() ? "ACTIVE!" : "Ready")}", style);
            y += lineHeight;
            style.normal.textColor = Color.white;
        }
        y += sectionSpacing;

        // === DIFFICULTY SECTION ===
        style.normal.textColor = Color.cyan;
        GUI.Label(new Rect(15, y, 330, lineHeight), "-- Difficulty --", headerStyle);
        y += lineHeight;
        style.normal.textColor = Color.white;

        if (difficultyManager != null)
        {
            GUI.Label(new Rect(15, y, 330, lineHeight), $"Emission: {difficultyManager.GetCurrentEmissionRate():F1}/s", style);
            y += lineHeight;

            GUI.Label(new Rect(15, y, 330, lineHeight), $"Multiplier: {difficultyManager.GetCurrentMultiplier():F2}x", style);
            y += lineHeight;

            GUI.Label(new Rect(15, y, 330, lineHeight), $"Rubber Band: {difficultyManager.GetRubberBandAdjustment():F2}x", style);
            y += lineHeight;
        }
        y += sectionSpacing;

        // === RESUPPLY SECTION ===
        style.normal.textColor = Color.cyan;
        GUI.Label(new Rect(15, y, 330, lineHeight), "-- Resupply --", headerStyle);
        y += lineHeight;
        style.normal.textColor = Color.white;

        if (resupplyManager != null && resupplyManager.enabled)
        {
            // Get timing info via reflection (since fields are private)
            var resupplyType = resupplyManager.GetType();
            var isActiveField = resupplyType.GetField("isActive", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var nextAirDropField = resupplyType.GetField("nextAirDropTime", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var nextBargeField = resupplyType.GetField("nextBargeTime", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var activePackagesField = resupplyType.GetField("activePackages", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (isActiveField != null)
            {
                bool isActive = (bool)isActiveField.GetValue(resupplyManager);
                style.normal.textColor = isActive ? Color.green : Color.gray;
                GUI.Label(new Rect(15, y, 330, lineHeight), $"Status: {(isActive ? "Active" : "Inactive")}", style);
                y += lineHeight;
                style.normal.textColor = Color.white;
            }

            float currentTime = gameController?.gameState?.timer ?? Time.time;

            if (nextAirDropField != null)
            {
                float nextAirDrop = (float)nextAirDropField.GetValue(resupplyManager);
                float timeToAirDrop = nextAirDrop - currentTime;
                if (timeToAirDrop > 0)
                {
                    GUI.Label(new Rect(15, y, 330, lineHeight), $"Next Air-Drop: {timeToAirDrop:F0}s", style);
                }
                else
                {
                    style.normal.textColor = Color.yellow;
                    GUI.Label(new Rect(15, y, 330, lineHeight), "Air-Drop: Ready!", style);
                    style.normal.textColor = Color.white;
                }
                y += lineHeight;
            }

            if (nextBargeField != null)
            {
                float nextBarge = (float)nextBargeField.GetValue(resupplyManager);
                float timeToBarge = nextBarge - currentTime;
                if (timeToBarge > 0 && timeToBarge < 300) // Only show if within 5 minutes
                {
                    GUI.Label(new Rect(15, y, 330, lineHeight), $"Next Barge: {timeToBarge:F0}s", style);
                    y += lineHeight;
                }
            }

            if (activePackagesField != null)
            {
                var activePackages = activePackagesField.GetValue(resupplyManager) as System.Collections.Generic.List<GameObject>;
                if (activePackages != null && activePackages.Count > 0)
                {
                    style.normal.textColor = Color.yellow;
                    GUI.Label(new Rect(15, y, 330, lineHeight), $"Packages Floating: {activePackages.Count}", style);
                    y += lineHeight;
                    style.normal.textColor = Color.white;
                }
            }
        }
        else
        {
            style.normal.textColor = Color.red;
            GUI.Label(new Rect(15, y, 330, lineHeight), "ResupplyManager not found!", style);
            y += lineHeight;
        }
    }

    // Called by other systems to track window stats
    public void OnParticleBlocked(int count)
    {
        blockedInWindow += count;
    }

    public void OnParticleEscaped(int count)
    {
        escapedInWindow += count;
    }
}