using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Random = System.Random;

namespace OilLeak.News
{
    /// <summary>
    /// Manages news ticker content loading, selection, and interpolation.
    /// Follows patterns from Toast system but simpler - no triggers, single feed.
    /// </summary>
    public class NewsTickerManager : INewsTickerService
    {
        // Configuration
        private NewsTickerContent content;
        private NewsTickerConfig config;

        // Runtime State
        private Queue<string> regularQueue;
        private Queue<BreakingHeadline> breakingQueue;
        private RingBuffer<string> recentIds;
        private Dictionary<string, float> cooldowns;
        private StringBuilder interpolationBuffer;

        // Current state
        private int currentTier = 5; // Start at pristine
        private SessionStats lastStats;
        private Random seededRandom;
        private bool isInitialized = false;
        private bool isActive = false;
        private bool isPaused = false;

        // Constants
        private const int MAX_BREAKING_QUEUE = 5;
        private const int RECENT_IDS_BUFFER_SIZE = 10;
        private const float GLOBAL_BREAKING_MIN_INTERVAL = 10f; // seconds between any breaking news
        private float lastBreakingTime = -100f;

        // Debug
        private List<string> loadErrors = new List<string>();

        #region INewsTickerService Implementation

        public bool IsReady => isInitialized && content != null;

        public void Initialize()
        {
            if (isInitialized) return;

            Debug.Log("[NewsTickerManager] Initializing...");

            // Initialize collections
            regularQueue = new Queue<string>();
            breakingQueue = new Queue<BreakingHeadline>(MAX_BREAKING_QUEUE);
            recentIds = new RingBuffer<string>(RECENT_IDS_BUFFER_SIZE);
            cooldowns = new Dictionary<string, float>();
            interpolationBuffer = new StringBuilder(256);

            // Load configuration
            LoadConfiguration();

            // Load content
            LoadContent();

            // Seed random for this session
            int seed = (int)(Time.realtimeSinceStartup * 1000) ^ (SystemInfo.deviceUniqueIdentifier?.GetHashCode() ?? 0);
            seededRandom = new Random(seed);
            Debug.Log($"[NewsTickerManager] Seeded RNG with {seed}");

            isInitialized = true;

            if (loadErrors.Count > 0)
            {
                Debug.LogWarning($"[NewsTickerManager] Initialized with {loadErrors.Count} errors");
                foreach (var error in loadErrors)
                {
                    Debug.LogWarning($"  - {error}");
                }
            }
            else
            {
                Debug.Log("[NewsTickerManager] Initialized successfully");
            }
        }

        public void Start()
        {
            if (!isInitialized)
            {
                Debug.LogError("[NewsTickerManager] Cannot start - not initialized");
                return;
            }

            isActive = true;
            isPaused = false;
            Debug.Log("[NewsTickerManager] Started");
        }

        public void Pause()
        {
            isPaused = true;
            Debug.Log("[NewsTickerManager] Paused");
        }

        public void Resume()
        {
            isPaused = false;
            Debug.Log("[NewsTickerManager] Resumed");
        }

        public void Stop()
        {
            isActive = false;
            isPaused = false;

            // Clear queues
            regularQueue.Clear();
            breakingQueue.Clear();

            Debug.Log("[NewsTickerManager] Stopped");
        }

        public string GetNextHeadline()
        {
            // Return null/empty during reset or when paused
            if (!IsReady || !isActive || isPaused)
                return null; // UI should handle null and retry after Resume/Start

            // Update cooldowns
            UpdateCooldowns();

            // Check breaking queue first (with global interval check)
            if (breakingQueue.Count > 0 && Time.time - lastBreakingTime >= GLOBAL_BREAKING_MIN_INTERVAL)
            {
                var breaking = breakingQueue.Dequeue();

                // Check if it's expired (TTL)
                if (Time.time - breaking.queuedTime <= breaking.ttl)
                {
                    lastBreakingTime = Time.time;
                    cooldowns[breaking.id] = Time.time + breaking.cooldown;
                    return InterpolateTemplate(breaking.template);
                }
                // If expired, continue to regular selection
            }

            // Regular selection from current tier
            return GetRegularHeadline();
        }

        public void NotifyBreaking(BreakingEventType type, object context)
        {
            if (!IsReady || !isActive || isPaused) return;

            // Find appropriate breaking template for this event type
            var template = GetBreakingTemplate(type, context);
            if (template == null) return;

            // Check cooldown
            if (cooldowns.ContainsKey(template.id) && Time.time < cooldowns[template.id])
            {
                #if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[NewsTickerManager] Breaking news {template.id} still on cooldown");
                #endif
                return;
            }

            // Add to breaking queue (drop oldest if full)
            if (breakingQueue.Count >= MAX_BREAKING_QUEUE)
            {
                breakingQueue.Dequeue();
            }

            breakingQueue.Enqueue(new BreakingHeadline
            {
                id = template.id,
                template = template.template,
                cooldown = template.cooldownSec,
                ttl = template.ttlSec,
                queuedTime = Time.time
            });

            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[NewsTickerManager] Queued breaking news: {template.id}");
            #endif
        }

        public void UpdateState(SessionStats stats, int tier)
        {
            lastStats = stats;

            if (tier != currentTier)
            {
                var oldTier = currentTier;
                currentTier = tier;

                #if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[NewsTickerManager] Tier changed from {oldTier} to {tier}");
                #endif
            }
        }

        #endregion

        #region State Accessors

        /// <summary>
        /// Expose initialization state for IsClean checks
        /// </summary>
        public bool IsInitialized => isInitialized;

        /// <summary>
        /// Expose active state for IsClean checks
        /// </summary>
        public bool IsActive => isActive;

        /// <summary>
        /// Expose paused state for IsClean checks
        /// </summary>
        public bool IsPaused => isPaused;

        /// <summary>
        /// Get count of regular queue items
        /// </summary>
        public int RegularQueueCount => regularQueue?.Count ?? 0;

        /// <summary>
        /// Get count of breaking queue items
        /// </summary>
        public int BreakingQueueCount => breakingQueue?.Count ?? 0;

        /// <summary>
        /// Get count of active cooldowns
        /// </summary>
        public int CooldownCount => cooldowns?.Count ?? 0;

        /// <summary>
        /// Get count of recent IDs tracked
        /// </summary>
        public int RecentCount => recentIds?.Count ?? 0;

        /// <summary>
        /// Check if we have any event subscriptions
        /// Note: The adapter manages subscriptions, so this is always false for the manager
        /// </summary>
        public bool HasSubscriptions => false;

        #endregion

        #region Reset Management

        /// <summary>
        /// Internal reset method that clears all state
        /// Called by the adapter during reset lifecycle
        /// </summary>
        public void ResetInternal()
        {
            Debug.Log("[NewsTickerManager] ResetInternal called");

            // Stop gate - prevent GetNextHeadline from returning content
            isActive = false;
            isPaused = false;

            // Clear queues
            if (regularQueue != null)
            {
                regularQueue.Clear();
            }

            if (breakingQueue != null)
            {
                breakingQueue.Clear();
            }

            // Clear cooldowns
            if (cooldowns != null)
            {
                cooldowns.Clear();
            }

            // Clear recent IDs
            if (recentIds != null)
            {
                recentIds.Clear();
            }

            // Clear current state
            currentTier = 5; // Reset to pristine
            lastStats = new SessionStats();
            lastBreakingTime = -100f;

            // Reset flags
            isInitialized = false;
            // Note: isReady is derived from isInitialized && content != null
            // We keep content loaded for efficiency

            // Clear RNG - will be re-seeded on next Initialize
            seededRandom = null;

            Debug.Log("[NewsTickerManager] Reset complete");
        }

        #endregion

        #region Content Loading

        private void LoadConfiguration()
        {
            // Try to load NewsTickerConfig from Resources
            config = Resources.Load<NewsTickerConfig>("NewsTickerConfig");

            if (config == null)
            {
                // Create default config
                config = ScriptableObject.CreateInstance<NewsTickerConfig>();
                config.scrollSpeed = 120f;
                config.globalBreakingMinInterval = 10f;
                config.recentIdsBufferSize = 10;
                loadErrors.Add("No NewsTickerConfig found in Resources, using defaults");
            }
        }

        private void LoadContent()
        {
            var loader = new NewsTickerContentLoader();
            var (loadedContent, errors) = loader.LoadFromStreamingAssets("NewsTickerContent.json");

            if (loadedContent != null)
            {
                content = loadedContent;
                Debug.Log($"[NewsTickerManager] Loaded {GetContentStats()} messages");
            }
            else
            {
                loadErrors.Add("Failed to load NewsTickerContent.json");
                content = CreateFallbackContent();
            }

            if (errors != null && errors.Count > 0)
            {
                loadErrors.AddRange(errors);
            }
        }

        private NewsTickerContent CreateFallbackContent()
        {
            // Create minimal fallback content so game doesn't break
            var fallback = new NewsTickerContent
            {
                version = "1.0-fallback",
                tiers = new Dictionary<string, TierContent>()
            };

            // Add basic messages for each tier
            for (int tier = 1; tier <= 5; tier++)
            {
                fallback.tiers[$"tier{tier}"] = new TierContent
                {
                    messages = new List<NewsMessage>
                    {
                        new NewsMessage
                        {
                            id = $"fallback_t{tier}",
                            template = $"Day {{time}}: Situation continues",
                            weight = 10
                        }
                    }
                };
            }

            fallback.breaking = new List<BreakingNewsTemplate>();

            return fallback;
        }

        private string GetContentStats()
        {
            if (content == null) return "0";

            int total = 0;
            foreach (var tier in content.tiers.Values)
            {
                total += tier.messages?.Count ?? 0;
            }
            total += content.breaking?.Count ?? 0;

            return total.ToString();
        }

        #endregion

        #region Headline Selection

        private string GetRegularHeadline()
        {
            // Get messages for current tier
            var tierKey = $"tier{currentTier}";
            if (!content.tiers.ContainsKey(tierKey))
            {
                // Fallback to adjacent tier
                tierKey = FindFallbackTier();
            }

            if (!content.tiers.ContainsKey(tierKey))
                return GetFallbackHeadline();

            var tierContent = content.tiers[tierKey];
            if (tierContent.messages == null || tierContent.messages.Count == 0)
                return GetFallbackHeadline();

            // Weighted selection avoiding recent IDs
            var candidates = tierContent.messages
                .Where(m => !recentIds.Contains(m.id))
                .ToList();

            if (candidates.Count == 0)
            {
                // All recent, pick least recent
                candidates = tierContent.messages;
            }

            var selected = SelectWeighted(candidates);
            if (selected != null)
            {
                recentIds.Add(selected.id);
                return InterpolateTemplate(selected.template);
            }

            return GetFallbackHeadline();
        }

        private NewsMessage SelectWeighted(List<NewsMessage> messages)
        {
            if (messages == null || messages.Count == 0) return null;
            if (messages.Count == 1) return messages[0];

            // Calculate total weight
            int totalWeight = 0;
            foreach (var msg in messages)
            {
                totalWeight += msg.weight;
            }

            if (totalWeight <= 0) return messages[0];

            // Pick random point in weight space
            int randomPoint = seededRandom.Next(totalWeight);

            // Find which message this lands on
            int currentWeight = 0;
            foreach (var msg in messages)
            {
                currentWeight += msg.weight;
                if (randomPoint < currentWeight)
                    return msg;
            }

            return messages[messages.Count - 1];
        }

        private BreakingNewsTemplate GetBreakingTemplate(BreakingEventType type, object context)
        {
            if (content.breaking == null || content.breaking.Count == 0)
                return null;

            // Filter templates by type (for MVP, just return random)
            // TODO: Add type filtering when we have more content
            var candidates = content.breaking.ToList();

            if (candidates.Count == 0) return null;
            if (candidates.Count == 1) return candidates[0];

            // Weighted selection
            return SelectWeightedBreaking(candidates);
        }

        private BreakingNewsTemplate SelectWeightedBreaking(List<BreakingNewsTemplate> templates)
        {
            int totalWeight = templates.Sum(t => t.weight);
            if (totalWeight <= 0) return templates[0];

            int randomPoint = seededRandom.Next(totalWeight);
            int currentWeight = 0;

            foreach (var template in templates)
            {
                currentWeight += template.weight;
                if (randomPoint < currentWeight)
                    return template;
            }

            return templates[templates.Count - 1];
        }

        private string FindFallbackTier()
        {
            // Try adjacent tiers
            if (currentTier > 1 && content.tiers.ContainsKey($"tier{currentTier - 1}"))
                return $"tier{currentTier - 1}";

            if (currentTier < 5 && content.tiers.ContainsKey($"tier{currentTier + 1}"))
                return $"tier{currentTier + 1}";

            // Try any tier
            foreach (var key in content.tiers.Keys)
            {
                if (content.tiers[key].messages?.Count > 0)
                    return key;
            }

            return "";
        }

        private string GetFallbackHeadline()
        {
            return $"Day {GetFormattedTime()}: Crisis continues";
        }

        #endregion

        #region Interpolation

        private string InterpolateTemplate(string template)
        {
            if (string.IsNullOrEmpty(template))
                return GetFallbackHeadline();

            // Use StringBuilder for efficiency
            interpolationBuffer.Clear();
            interpolationBuffer.Append(template);

            // Replace variables
            ReplaceVariable(interpolationBuffer, "{time}", GetFormattedTime());
            ReplaceVariable(interpolationBuffer, "{blocked}", FormatNumber(lastStats.ParticlesBlocked));
            ReplaceVariable(interpolationBuffer, "{escaped}", FormatNumber(lastStats.ParticlesEscaped));
            ReplaceVariable(interpolationBuffer, "{integrity}", lastStats.Integrity.ToString("F0"));
            ReplaceVariable(interpolationBuffer, "{tierName}", GetTierName(currentTier));
            ReplaceVariable(interpolationBuffer, "{items}", lastStats.ItemsThrown.ToString());
            ReplaceVariable(interpolationBuffer, "{score}", FormatNumber(lastStats.Score));

            return interpolationBuffer.ToString();
        }

        private void ReplaceVariable(StringBuilder sb, string variable, string value)
        {
            sb.Replace(variable, value);
        }

        private string GetFormattedTime()
        {
            if (lastStats.TimeElapsed < 60)
                return "1"; // Day 1 for first minute

            int days = Mathf.FloorToInt(lastStats.TimeElapsed / 60f); // 1 day per minute for pacing
            return days.ToString();
        }

        private string FormatNumber(int number)
        {
            // Format with thousands separator
            return number.ToString("N0");
        }

        private string GetTierName(int tier)
        {
            switch (tier)
            {
                case 5: return "Pristine";
                case 4: return "Stable";
                case 3: return "Damaged";
                case 2: return "Critical";
                case 1: return "Failing";
                default: return "Unknown";
            }
        }

        #endregion

        #region Cooldown Management

        private void UpdateCooldowns()
        {
            // Remove expired cooldowns
            var expiredKeys = new List<string>();
            foreach (var kvp in cooldowns)
            {
                if (Time.time >= kvp.Value)
                    expiredKeys.Add(kvp.Key);
            }

            foreach (var key in expiredKeys)
            {
                cooldowns.Remove(key);
            }
        }

        #endregion

        #region Helper Classes

        private class BreakingHeadline
        {
            public string id;
            public string template;
            public float cooldown;
            public float ttl;
            public float queuedTime;
        }

        private class RingBuffer<T>
        {
            private T[] buffer;
            private int writeIndex = 0;
            private int count = 0;

            public int Count => count;

            public RingBuffer(int capacity)
            {
                buffer = new T[capacity];
            }

            public void Add(T item)
            {
                buffer[writeIndex] = item;
                writeIndex = (writeIndex + 1) % buffer.Length;
                if (count < buffer.Length) count++;
            }

            public bool Contains(T item)
            {
                for (int i = 0; i < count; i++)
                {
                    if (EqualityComparer<T>.Default.Equals(buffer[i], item))
                        return true;
                }
                return false;
            }

            public void Clear()
            {
                count = 0;
                writeIndex = 0;
                // Clear references for GC
                for (int i = 0; i < buffer.Length; i++)
                {
                    buffer[i] = default(T);
                }
            }
        }

        #endregion
    }

    /// <summary>
    /// Types of breaking news events
    /// </summary>
    public enum BreakingEventType
    {
        IntegrityTierChange,
        PressureBurst,
        ResupplyArrival,
        RoundOver,
        MajorMilestone
    }
}