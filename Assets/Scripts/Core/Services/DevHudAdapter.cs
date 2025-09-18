using UnityEngine;
using Core;

namespace Core.Services
{
    /// <summary>
    /// Adapter that bridges DevHUD to IDevHudService
    /// Converts push-based updates to DevHUD display
    /// </summary>
    public class DevHudAdapter : IDevHudService
    {
        private readonly DevHUD devHud;

        // Cached data for display
        private SessionData sessionData = new SessionData();
        private DifficultyData difficultyData = new DifficultyData();
        private ResupplyData resupplyData = new ResupplyData();
        private LeakData leakData = new LeakData();
        private PerformanceData perfData = new PerformanceData();
        private StateData stateData = new StateData();

        public DevHudAdapter(DevHUD devHud)
        {
            this.devHud = devHud;
            if (devHud == null)
            {
                Debug.LogError("[DevHudAdapter] DevHUD is null!");
            }
            else
            {
                // Give DevHUD reference to this adapter for rendering
                devHud.SetAdapter(this);
                Debug.Log("[DevHudAdapter] Connected to DevHUD");
            }
        }

        #region IDevHudService Implementation

        public void UpdateSessionStats(float timeElapsed, int particlesBlocked, int particlesEscaped, int maxEscaped)
        {
            sessionData.timeElapsed = timeElapsed;
            sessionData.particlesBlocked = particlesBlocked;
            sessionData.particlesEscaped = particlesEscaped;
            sessionData.maxEscaped = maxEscaped;
            sessionData.escapedPercent = maxEscaped > 0 ? (particlesEscaped * 100f / maxEscaped) : 0f;
        }

        public void UpdateDifficulty(float emissionRate, float multiplier, float rubberBand)
        {
            difficultyData.emissionRate = emissionRate;
            difficultyData.multiplier = multiplier;
            difficultyData.rubberBand = rubberBand;
        }

        public void UpdateResupplyStatus(bool isActive, float nextAirDropIn, float nextBargeIn, int activePackages)
        {
            resupplyData.isActive = isActive;
            resupplyData.nextAirDropIn = nextAirDropIn;
            resupplyData.nextBargeIn = nextBargeIn;
            resupplyData.activePackages = activePackages;
        }

        public void UpdateLeakStatus(int activeParticles, float currentPressure, int activeLeaks)
        {
            leakData.activeParticles = activeParticles;
            leakData.currentPressure = currentPressure;
            leakData.activeLeaks = activeLeaks;
        }

        public void UpdatePerformance(float fps, int activeItems)
        {
            perfData.fps = fps;
            perfData.activeItems = activeItems;
        }

        public void UpdateGameState(GameFlowState state, bool isEndlessMode)
        {
            stateData.currentState = state;
            stateData.isEndlessMode = isEndlessMode;
        }

        public void SetVisible(bool visible)
        {
            if (devHud != null)
            {
                devHud.showDevHUD = visible;
            }
        }

        #endregion

        #region IResettable Implementation

        public void Reset()
        {
            sessionData = new SessionData();
            difficultyData = new DifficultyData();
            resupplyData = new ResupplyData();
            leakData = new LeakData();
            perfData = new PerformanceData();
            stateData = new StateData();
            Debug.Log("[DevHudAdapter] State reset");
        }

        public bool IsClean => sessionData.timeElapsed == 0f &&
                               sessionData.particlesEscaped == 0 &&
                               sessionData.particlesBlocked == 0;

        #endregion

        #region Data Access for DevHUD Rendering

        public SessionData GetSessionData() => sessionData;
        public DifficultyData GetDifficultyData() => difficultyData;
        public ResupplyData GetResupplyData() => resupplyData;
        public LeakData GetLeakData() => leakData;
        public PerformanceData GetPerformanceData() => perfData;
        public StateData GetStateData() => stateData;

        #endregion

        #region Data Classes

        public class SessionData
        {
            public float timeElapsed;
            public int particlesBlocked;
            public int particlesEscaped;
            public int maxEscaped = 100; // Default
            public float escapedPercent;
        }

        public class DifficultyData
        {
            public float emissionRate = 5f;
            public float multiplier = 1f;
            public float rubberBand = 1f;
        }

        public class ResupplyData
        {
            public bool isActive;
            public float nextAirDropIn = -1;
            public float nextBargeIn = -1;
            public int activePackages;
        }

        public class LeakData
        {
            public int activeParticles;
            public float currentPressure;
            public int activeLeaks;
        }

        public class PerformanceData
        {
            public float fps = 60f;
            public int activeItems;
        }

        public class StateData
        {
            public GameFlowState currentState = GameFlowState.Menu;
            public bool isEndlessMode = true;
        }

        #endregion
    }
}