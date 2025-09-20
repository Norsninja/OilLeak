using UnityEngine;
using System.Collections.Generic;

namespace OilLeak.Toast.Data
{
    [System.Serializable]
    public class TriggerCondition
    {
        public string type; // time_elapsed, integrity_below, items_degraded, etc.
        public string op;   // >=, <=, ==, !=, <, >
        public float value;
    }

    [CreateAssetMenu(fileName = "ToastTrigger", menuName = "OilLeak/Toast/Trigger")]
    public class ToastTrigger : ScriptableObject
    {
        [Header("Trigger Identity")]
        [SerializeField] private string id;
        [SerializeField] private TriggerType type;
        [SerializeField] private string description;

        [Header("Thresholds")]
        [Tooltip("Multiple thresholds for series triggers (e.g., 3, 5, 8, 12 minutes)")]
        [SerializeField] private float[] thresholds;

        [Header("Conditions")]
        [SerializeField] private List<TriggerCondition> conditions = new List<TriggerCondition>();
        [SerializeField] private LogicalOperator conditionLogic = LogicalOperator.AND;

        [Header("Cooldowns")]
        [SerializeField] private float globalCooldown = 30f;
        [SerializeField] private float perVoiceCooldown = 60f;

        [Header("Priority")]
        [Range(0, 100)]
        [SerializeField] private int priority = 50;
        [SerializeField] private bool isInterruptTrigger = false;
        [SerializeField] private bool oneTimeOnly = false;

        public string Id => id;
        public TriggerType Type => type;
        public float[] Thresholds => thresholds;
        public List<TriggerCondition> Conditions => conditions;
        public LogicalOperator ConditionLogic => conditionLogic;
        public float GlobalCooldown => globalCooldown;
        public float PerVoiceCooldown => perVoiceCooldown;
        public int Priority => priority;
        public bool IsInterruptTrigger => isInterruptTrigger;
        public bool OneTimeOnly => oneTimeOnly;

        public bool EvaluateConditions(TriggerContext context)
        {
            if (conditions == null || conditions.Count == 0)
                return true;

            bool result = conditionLogic == LogicalOperator.AND;

            foreach (var condition in conditions)
            {
                bool conditionMet = EvaluateCondition(condition, context);

                if (conditionLogic == LogicalOperator.AND)
                {
                    result = result && conditionMet;
                    if (!result) break; // Early exit for AND
                }
                else // OR
                {
                    result = result || conditionMet;
                    if (result) break; // Early exit for OR
                }
            }

            return result;
        }

        private bool EvaluateCondition(TriggerCondition condition, TriggerContext context)
        {
            float value = GetConditionValue(condition.type, context);

            switch (condition.op)
            {
                case ">=": return value >= condition.value;
                case "<=": return value <= condition.value;
                case ">": return value > condition.value;
                case "<": return value < condition.value;
                case "==": return Mathf.Approximately(value, condition.value);
                case "!=": return !Mathf.Approximately(value, condition.value);
                default:
                    Debug.LogWarning($"Unknown operator: {condition.op}");
                    return false;
            }
        }

        private float GetConditionValue(string conditionType, TriggerContext context)
        {
            switch (conditionType)
            {
                case "time_elapsed":
                    return context.timeElapsed;
                case "integrity":
                    return context.integrity;
                case "integrity_below":
                    return context.integrity;
                case "gallons_blocked":
                    return context.gallonsBlocked;
                case "gallons_escaped":
                    return context.gallonsEscaped;
                case "items_degraded":
                    return context.itemsDegraded;
                case "resupply_count":
                    return context.resupplyCount;
                case "resupply_event":
                    // For event-based triggers, return 1 if we have any resupplies
                    return context.resupplyCount > 0 ? 1f : 0f;
                default:
                    Debug.LogWarning($"Unknown condition type: {conditionType}");
                    return 0f;
            }
        }

        private void OnValidate()
        {
            // Ensure ID matches asset name
            if (string.IsNullOrEmpty(id))
            {
                id = name.Replace(" ", "_").ToLower();
            }

            // Sort thresholds
            if (thresholds != null && thresholds.Length > 1)
            {
                System.Array.Sort(thresholds);
            }
        }
    }

    public enum TriggerType
    {
        Time,
        Gallons,
        Integrity,
        Resupply,
        Special,
        Milestone
    }

    public enum LogicalOperator
    {
        AND,
        OR
    }

    // Runtime context passed to trigger evaluation
    [System.Serializable]
    public class TriggerContext
    {
        public float timeElapsed;
        public float integrity;
        public float gallonsBlocked;
        public float gallonsEscaped;
        public int itemsDegraded;
        public int resupplyCount;
        public Dictionary<string, float> customValues;

        public TriggerContext()
        {
            customValues = new Dictionary<string, float>();
        }
    }

    // Runtime state for tracking trigger progress (NOT stored on ScriptableObject)
    public class TriggerRuntimeState
    {
        public string triggerId;
        public int currentThresholdIndex = 0;
        public float lastFiredTime = -999f;
        public Dictionary<string, float> voiceLastFiredTimes;
        public bool hasBeenFired = false; // For one-time triggers

        public TriggerRuntimeState(string id)
        {
            triggerId = id;
            voiceLastFiredTimes = new Dictionary<string, float>();
        }

        public bool CanFire(float currentTime, float globalCooldown, float perVoiceCooldown, string voiceId = null)
        {
            // Check global cooldown
            if (currentTime - lastFiredTime < globalCooldown)
                return false;

            // Check per-voice cooldown
            if (!string.IsNullOrEmpty(voiceId))
            {
                if (voiceLastFiredTimes.TryGetValue(voiceId, out float lastVoiceTime))
                {
                    if (currentTime - lastVoiceTime < perVoiceCooldown)
                        return false;
                }
            }

            return true;
        }

        public void MarkFired(float currentTime, string voiceId = null)
        {
            lastFiredTime = currentTime;
            hasBeenFired = true;

            if (!string.IsNullOrEmpty(voiceId))
            {
                voiceLastFiredTimes[voiceId] = currentTime;
            }
        }

        public float? GetNextThreshold(float[] thresholds, float currentValue)
        {
            if (thresholds == null || currentThresholdIndex >= thresholds.Length)
                return null;

            var threshold = thresholds[currentThresholdIndex];
            return currentValue >= threshold ? (float?)threshold : null;
        }

        public void AdvanceThreshold()
        {
            currentThresholdIndex++;
        }
    }
}