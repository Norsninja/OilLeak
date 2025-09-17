using UnityEngine;
using Core;

namespace Core.Services
{
    /// <summary>
    /// Adapter that bridges DifficultyManager to IDifficultyService
    /// Implements IResettable for clean restart support
    /// </summary>
    public class DifficultyManagerAdapter : IDifficultyService, IResettable
    {
        private readonly DifficultyManager manager;
        private float lastTickTime;
        private const float TICK_INTERVAL = 0.5f; // 2Hz update rate

        public DifficultyManagerAdapter(DifficultyManager manager)
        {
            this.manager = manager;
            if (manager == null)
            {
                Debug.LogError("[DifficultyAdapter] Manager is null!");
            }
        }

        #region IDifficultyService Implementation

        public void SetEmissionCurve(AnimationCurve curve, float baseRate, float maxRate)
        {
            if (manager == null) return;

            // We'll need to expose these setters in DifficultyManager
            // For now, log the intent
            Debug.Log($"[DifficultyAdapter] SetEmissionCurve - base: {baseRate}, max: {maxRate}");
        }

        public void SetMultiplierCurve(AnimationCurve curve)
        {
            if (manager == null) return;

            // We'll need to expose this setter in DifficultyManager
            Debug.Log("[DifficultyAdapter] SetMultiplierCurve");
        }

        public bool TickIfDue()
        {
            if (manager == null) return false;

            float currentTime = Time.time;
            if (currentTime - lastTickTime >= TICK_INTERVAL)
            {
                lastTickTime = currentTime;
                // DifficultyManager updates itself in Update()
                // We're just tracking the tick timing here
                return true;
            }
            return false;
        }

        public void OnParticleBlocked(int count)
        {
            if (manager != null)
            {
                manager.OnParticleBlocked(count);
            }
        }

        public void OnParticleEscaped(int count)
        {
            if (manager != null)
            {
                manager.OnParticleEscaped(count);
            }
        }

        public void Reset()
        {
            if (manager != null)
            {
                manager.ResetDifficulty();
            }
            lastTickTime = 0f;
        }

        public void SetDifficultyTime(float minutes)
        {
            if (manager != null)
            {
                manager.SetDifficultyTime(minutes);
            }
        }

        public float GetCurrentEmissionRate()
        {
            return manager != null ? manager.GetCurrentEmissionRate() : 5f;
        }

        public float GetCurrentMultiplier()
        {
            return manager != null ? manager.GetCurrentMultiplier() : 1f;
        }

        public float GetRubberBandAdjustment()
        {
            return manager != null ? manager.GetRubberBandAdjustment() : 1f;
        }

        public float GetElapsedMinutes()
        {
            return manager != null ? manager.GetElapsedMinutes() : 0f;
        }

        public void SetRubberBandEnabled(bool enabled)
        {
            if (manager == null) return;

            // We'll need to expose this setter in DifficultyManager
            Debug.Log($"[DifficultyAdapter] SetRubberBandEnabled: {enabled}");
        }

        #endregion

        #region IResettable Implementation

        void IResettable.Reset()
        {
            Reset();
            Debug.Log("[DifficultyAdapter] State reset");
        }

        public bool IsClean => lastTickTime == 0f &&
            (manager == null || manager.GetElapsedMinutes() == 0f);

        #endregion
    }
}