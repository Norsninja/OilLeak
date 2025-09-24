using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using OilLeak.Toast.Data;
using Random = System.Random;

namespace OilLeak.Toast.Services
{
    public class ToastManager : IToastService
    {
        // Configuration
        private ToastSystemConfig config;
        private List<ToastVoiceProfile> voiceProfiles;
        private List<ToastTrigger> triggers;

        // Runtime state
        private Dictionary<string, TriggerRuntimeState> triggerStates;
        private Queue<TriggerEvaluation> pendingEvaluations;
        private HashSet<string> pendingTriggerIds;  // Track which triggers are already queued
        private ToastQueue toastQueue;
        private ToastSessionLog sessionLog;
        private Random seededRandom;

        // Service state
        private bool isInitialized = false;
        private bool isReady = false;
        private bool isActive = false;
        private bool isPaused = false;
        private List<string> loadErrors = new List<string>();

        // Timing
        private float lastBatchProcessTime = 0f;
        private float currentTime = 0f;

        // Game state provider (injected)
        private IGameStateProvider gameStateProvider;

        // Debug tracking
        private ToastDebugInfo debugInfo;
        private Queue<string> recentTriggers;
        private int triggersEvaluatedThisSecond = 0;
        private float debugSecondTimer = 0f;

        // Events
        public event Action<ToastPayload> OnToastQueued;
        public event Action<ToastPayload> OnToastDisplayed;
        public event Action<ToastPayload> OnToastDismissed;

        // Properties
        public bool IsReady => isReady;

        public ToastManager(IGameStateProvider stateProvider = null)
        {
            pendingEvaluations = new Queue<TriggerEvaluation>();
            pendingTriggerIds = new HashSet<string>();
            triggerStates = new Dictionary<string, TriggerRuntimeState>();
            sessionLog = new ToastSessionLog();
            debugInfo = new ToastDebugInfo();
            recentTriggers = new Queue<string>();
            gameStateProvider = stateProvider;
        }

        public void Initialize()
        {
            if (isInitialized) return;

            Debug.Log("[ToastManager] Initializing...");

            // Load configuration
            LoadConfiguration();

            // Initialize voice profiles
            InitializeVoiceProfiles();

            // Load triggers
            LoadTriggers();

            // Initialize queue
            if (config != null)
            {
                toastQueue = new ToastQueue(config);
                toastQueue.OnToastReady += HandleToastReady;
                toastQueue.OnToastExpired += HandleToastExpired;
            }
            else
            {
                // Use defaults if no config
                var defaultConfig = ScriptableObject.CreateInstance<ToastSystemConfig>();
                toastQueue = new ToastQueue(defaultConfig);
                toastQueue.OnToastReady += HandleToastReady;
                toastQueue.OnToastExpired += HandleToastExpired;
                loadErrors.Add("No ToastSystemConfig found, using defaults");
            }

            // Initialize trigger states
            InitializeTriggerStates();

            // Set ready flag based on content availability
            CheckContentReady();

            isInitialized = true;
            Debug.Log($"[ToastManager] Initialization complete. Ready: {isReady}, Errors: {loadErrors.Count}");
        }

        private void LoadConfiguration()
        {
            // Load from Resources or find in project
            config = Resources.Load<ToastSystemConfig>("ToastConfig");
            if (config == null)
            {
                // Try to find any ToastSystemConfig in the project
                var configs = Resources.LoadAll<ToastSystemConfig>("");
                if (configs.Length > 0)
                {
                    config = configs[0];
                    Debug.Log($"[ToastManager] Loaded config: {config.name}");
                }
            }
        }

        private void InitializeVoiceProfiles()
        {
            voiceProfiles = new List<ToastVoiceProfile>();

            // Load all voice profiles from Resources
            var profiles = Resources.LoadAll<ToastVoiceProfile>("ToastVoices");
            if (profiles.Length == 0)
            {
                // Try to find them anywhere in Resources
                profiles = Resources.LoadAll<ToastVoiceProfile>("");
            }

            foreach (var profile in profiles)
            {
                profile.Initialize();

                if (profile.ContentReady)
                {
                    voiceProfiles.Add(profile);
                    Debug.Log($"[ToastManager] Loaded voice profile: {profile.VoiceId}");
                }
                else
                {
                    loadErrors.Add($"Voice profile '{profile.name}' failed to load content");
                    if (profile.ValidationResult != null)
                    {
                        foreach (var error in profile.ValidationResult.errors)
                        {
                            loadErrors.Add($"  - {error}");
                        }
                    }
                }
            }

            if (voiceProfiles.Count == 0)
            {
                loadErrors.Add("No voice profiles loaded successfully");
            }
        }

        private void LoadTriggers()
        {
            triggers = new List<ToastTrigger>();

            // Load all triggers from Resources
            var loadedTriggers = Resources.LoadAll<ToastTrigger>("ToastTriggers");
            if (loadedTriggers.Length == 0)
            {
                // Try to find them anywhere
                loadedTriggers = Resources.LoadAll<ToastTrigger>("");
            }

            triggers.AddRange(loadedTriggers);

            if (triggers.Count == 0)
            {
                Debug.LogWarning("[ToastManager] No ToastTrigger assets found. Creating default triggers.");
                // Could create default triggers programmatically here if needed
            }
            else
            {
                Debug.Log($"[ToastManager] Loaded {triggers.Count} triggers");
            }
        }

        private void InitializeTriggerStates()
        {
            foreach (var trigger in triggers)
            {
                if (!string.IsNullOrEmpty(trigger.Id))
                {
                    triggerStates[trigger.Id] = new TriggerRuntimeState(trigger.Id);
                }
            }

            // Also initialize states for any triggers referenced in messages but not defined
            foreach (var profile in voiceProfiles)
            {
                var data = profile.GetParsedData();
                if (data?.acts != null)
                {
                    foreach (var act in data.acts.Values)
                    {
                        if (act.messages != null)
                        {
                            foreach (var msg in act.messages)
                            {
                                if (!string.IsNullOrEmpty(msg.triggerId) && !triggerStates.ContainsKey(msg.triggerId))
                                {
                                    triggerStates[msg.triggerId] = new TriggerRuntimeState(msg.triggerId);
                                    Debug.LogWarning($"[ToastManager] Created runtime state for undefined trigger: {msg.triggerId}");
                                }
                            }
                        }
                    }
                }
            }
        }

        private void CheckContentReady()
        {
            isReady = voiceProfiles.Count > 0 && voiceProfiles.Any(p => p.ContentReady);

            if (!isReady)
            {
                loadErrors.Add("No voice profiles have valid content loaded");
            }

            // Update debug info
            debugInfo.contentReady = isReady;
            debugInfo.totalMessagesLoaded = voiceProfiles.Sum(p =>
            {
                var data = p.GetParsedData();
                return data?.acts?.Values.Sum(a => a.messages?.Count ?? 0) ?? 0;
            });
            debugInfo.loadErrors = new List<string>(loadErrors);
        }

        public void StartToasting()
        {
            if (!isInitialized)
            {
                Initialize();
            }

            if (!isReady)
            {
                Debug.LogError("[ToastManager] Cannot start - content not ready. Errors:\n" + string.Join("\n", loadErrors));
                return;
            }

            // Subscribe to game events
            SubscribeToGameEvents();

            // Initialize random with session seed
            int seed = gameStateProvider?.RunSeed ?? UnityEngine.Random.Range(0, int.MaxValue);
            seededRandom = new Random(seed);

            isActive = true;
            isPaused = false;

            Debug.Log($"[ToastManager] Started toasting with seed {seed}");
        }

        private void SubscribeToGameEvents()
        {
            if (gameStateProvider != null)
            {
                gameStateProvider.OnTimeUpdated += HandleTimeUpdate;
                gameStateProvider.OnGallonsBlockedChanged += HandleGallonsChanged;
                gameStateProvider.OnIntegrityChanged += HandleIntegrityChanged;
                gameStateProvider.OnResupplyEvent += HandleResupplyEvent;
            }
            else
            {
                Debug.LogWarning("[ToastManager] No game state provider - toasts won't trigger from game events");
            }
        }

        private void HandleResupplyEvent()
        {
            if (!isActive || isPaused) return;

            var resupplyCount = gameStateProvider?.ResupplyCount ?? 0;
            foreach (var trigger in triggers)
            {
                if (trigger.Type == TriggerType.Resupply)
                {
                    if (ShouldQueueTrigger(trigger, resupplyCount))
                    {
                        QueueTriggerEvaluation(trigger, resupplyCount);
                    }
                }
            }
        }

        private void HandleTimeUpdate(float time)
        {
            if (!isActive || isPaused) return;

            currentTime = time;

            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Log every 10 seconds to see if we're getting time updates
            if (Mathf.FloorToInt(time) % 10 == 0 && Mathf.Approximately(time % 1f, 0f))
            {
                // Removed per-frame time update log - was spamming console
            }
            #endif

            // Queue time-based trigger evaluations
            foreach (var trigger in triggers)
            {
                if (trigger.Type == TriggerType.Time)
                {
                    if (ShouldQueueTrigger(trigger, time))
                    {
                        QueueTriggerEvaluation(trigger, time);
                    }
                }
            }

            // Process batch if enough time has passed
            if (time - lastBatchProcessTime >= (config?.batchProcessInterval ?? 0.25f))
            {
                ProcessTriggerBatch();
                lastBatchProcessTime = time;
            }

            // Update debug tracking
            debugSecondTimer += Time.deltaTime;
            if (debugSecondTimer >= 1f)
            {
                debugInfo.triggersEvaluatedLastSecond = triggersEvaluatedThisSecond;
                triggersEvaluatedThisSecond = 0;
                debugSecondTimer = 0f;
            }
        }

        private void HandleGallonsChanged(float gallonsBlocked)
        {
            if (!isActive || isPaused) return;

            foreach (var trigger in triggers)
            {
                if (trigger.Type == TriggerType.Gallons)
                {
                    if (ShouldQueueTrigger(trigger, gallonsBlocked))
                    {
                        QueueTriggerEvaluation(trigger, gallonsBlocked);
                    }
                }
            }
        }

        private void HandleIntegrityChanged(float integrity)
        {
            if (!isActive || isPaused) return;

            // Update current act for voice weighting
            debugInfo.currentAct = GetActForIntegrity(integrity).ToString();

            foreach (var trigger in triggers)
            {
                if (trigger.Type == TriggerType.Integrity)
                {
                    if (ShouldQueueTrigger(trigger, integrity))
                    {
                        QueueTriggerEvaluation(trigger, integrity);
                    }
                }
            }
        }

        private bool ShouldQueueTrigger(ToastTrigger trigger, float value)
        {
            // Check if already in pending queue
            if (pendingTriggerIds.Contains(trigger.Id))
                return false;

            // Check trigger state
            if (!triggerStates.TryGetValue(trigger.Id, out var state))
                return false;

            // Check one-time triggers
            if (trigger.OneTimeOnly && state.hasBeenFired)
                return false;

            // Check thresholds
            var thresholds = trigger.Thresholds;
            if (thresholds != null && thresholds.Length > 0)
            {
                // No more thresholds left
                if (state.currentThresholdIndex >= thresholds.Length)
                    return false;

                // Only queue when value is close enough to potentially trigger
                // For threshold-based triggers, only queue if we've reached the threshold
                var currentThreshold = thresholds[state.currentThresholdIndex];
                if (value < currentThreshold)
                    return false;
            }

            // For pure condition-based triggers (no thresholds), allow through
            return true;
        }

        private void QueueTriggerEvaluation(ToastTrigger trigger, float value)
        {
            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Log first few evaluations to see what's happening
            if (pendingEvaluations.Count < 5)
            {
                #if UNITY_EDITOR && TOAST_DEBUG
                Debug.Log($"[ToastManager] Queuing trigger '{trigger.Id}' for evaluation with value {value:F1}");
                #endif
            }
            #endif

            // Add to pending set to prevent duplicates
            pendingTriggerIds.Add(trigger.Id);

            pendingEvaluations.Enqueue(new TriggerEvaluation
            {
                trigger = trigger,
                value = value,
                context = BuildTriggerContext()
            });
        }

        private TriggerContext BuildTriggerContext()
        {
            var context = new TriggerContext();

            if (gameStateProvider != null)
            {
                context.timeElapsed = gameStateProvider.Timer;
                context.integrity = gameStateProvider.Integrity;
                context.gallonsBlocked = gameStateProvider.GallonsBlocked;
                context.gallonsEscaped = gameStateProvider.GallonsEscaped;
                context.itemsDegraded = gameStateProvider.ItemsDegraded;
                context.resupplyCount = gameStateProvider.ResupplyCount;
            }

            return context;
        }

        private void ProcessTriggerBatch()
        {
            if (pendingEvaluations.Count == 0) return;

            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            #if UNITY_EDITOR && TOAST_DEBUG
            Debug.Log($"[ToastManager] Processing batch with {pendingEvaluations.Count} pending evaluations");
            #endif
            #endif

            int maxToProcess = config?.maxTriggersPerBatch ?? 5;
            var toProcess = new List<TriggerEvaluation>();

            // Take up to max items
            for (int i = 0; i < maxToProcess && pendingEvaluations.Count > 0; i++)
            {
                var eval = pendingEvaluations.Dequeue();
                toProcess.Add(eval);
                // Remove from pending set since we're processing it
                pendingTriggerIds.Remove(eval.trigger.Id);
            }

            // Sort by priority with seeded random for ties
            toProcess = toProcess
                .Where(e => CanFireTrigger(e))
                .OrderByDescending(e => e.trigger.Priority)
                .ThenBy(e => seededRandom.Next())
                .ToList();

            // Process selected triggers
            foreach (var eval in toProcess)
            {
                ProcessTrigger(eval);
                triggersEvaluatedThisSecond++;
            }
        }

        private bool CanFireTrigger(TriggerEvaluation eval)
        {
            if (!triggerStates.TryGetValue(eval.trigger.Id, out var state))
                return false;

            // Check one-time flag
            if (eval.trigger.OneTimeOnly && state.hasBeenFired)
                return false;

            // Check cooldowns
            if (!state.CanFire(currentTime, eval.trigger.GlobalCooldown, eval.trigger.PerVoiceCooldown))
                return false;

            // Check conditions
            if (!eval.trigger.EvaluateConditions(eval.context))
                return false;

            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Temporary logging to diagnose trigger evaluation
            #if UNITY_EDITOR && TOAST_DEBUG
            Debug.Log($"[ToastManager] Trigger '{eval.trigger.Id}' passed conditions. Value: {eval.value:F1}");
            #endif
            #endif

            // Check thresholds
            if (eval.trigger.Thresholds != null && eval.trigger.Thresholds.Length > 0)
            {
                var nextThreshold = state.GetNextThreshold(eval.trigger.Thresholds, eval.value);

                #if UNITY_EDITOR || DEVELOPMENT_BUILD
                #if UNITY_EDITOR && TOAST_DEBUG
                Debug.Log($"[ToastManager] Trigger '{eval.trigger.Id}' threshold check - Next: {nextThreshold}, Current: {eval.value:F1}");
                #endif
                #endif

                if (!nextThreshold.HasValue || eval.value < nextThreshold.Value)
                    return false;
            }

            return true;
        }

        private void ProcessTrigger(TriggerEvaluation eval)
        {
            // Select voice based on current act and weights
            var selectedVoice = SelectVoiceForAct(GetCurrentAct());
            if (selectedVoice == null)
            {
                Debug.LogWarning($"[ToastManager] No voice selected for trigger '{eval.trigger.Id}'");
                return;
            }

            // Get templates for this trigger/act
            var templates = selectedVoice.GetTemplates(eval.trigger.Id, GetCurrentAct());
            if (templates.Count == 0)
            {
                // Silently skip - many triggers don't have templates in all voices/acts
                return;
            }

            // Select template by weight
            var selectedTemplate = SelectByWeight(templates);
            if (selectedTemplate == null) return;

            // Create payload
            var payload = CreatePayload(selectedTemplate, selectedVoice, eval.trigger.Id);

            // Queue the toast
            toastQueue.Enqueue(payload);
            OnToastQueued?.Invoke(payload);

            // Update state
            var state = triggerStates[eval.trigger.Id];
            state.MarkFired(currentTime, selectedVoice.VoiceId);
            if (eval.trigger.Thresholds != null && eval.trigger.Thresholds.Length > 0)
            {
                state.AdvanceThreshold();
            }

            // Log to session
            sessionLog.LogToast(payload);

            // Track for debug
            if (recentTriggers.Count >= 10)
                recentTriggers.Dequeue();
            recentTriggers.Enqueue(eval.trigger.Id);
            debugInfo.recentTriggerIds = recentTriggers.ToList();
        }

        private int GetCurrentAct()
        {
            float integrity = gameStateProvider?.Integrity ?? 100f;
            return GetActForIntegrity(integrity);
        }

        private int GetActForIntegrity(float integrity)
        {
            if (integrity >= 80f) return 1;
            if (integrity >= 60f) return 2;
            if (integrity >= 30f) return 3;
            return 4;
        }

        private ToastVoiceProfile SelectVoiceForAct(int act)
        {
            if (voiceProfiles.Count == 0) return null;

            // Get weights for current act
            var weights = GetVoiceWeightsForAct(act);

            // Build weighted selection
            float totalWeight = weights.Sum(kvp => kvp.Value);
            float roll = (float)seededRandom.NextDouble() * totalWeight;

            float current = 0f;
            foreach (var kvp in weights)
            {
                current += kvp.Value;
                if (roll <= current)
                {
                    return voiceProfiles.FirstOrDefault(p => p.VoiceId == kvp.Key);
                }
            }

            // Fallback
            return voiceProfiles[seededRandom.Next(voiceProfiles.Count)];
        }

        private Dictionary<string, float> GetVoiceWeightsForAct(int act)
        {
            // Default weights by act
            var weights = new Dictionary<string, float>();

            switch (act)
            {
                case 1: // 80-100%
                    weights["corporate"] = 60f;
                    weights["reality"] = 30f;
                    weights["gallows"] = 10f;
                    break;
                case 2: // 60-80%
                    weights["corporate"] = 33f;
                    weights["reality"] = 33f;
                    weights["gallows"] = 34f;
                    break;
                case 3: // 30-60%
                    weights["corporate"] = 20f;
                    weights["reality"] = 40f;
                    weights["gallows"] = 40f;
                    break;
                case 4: // 0-30%
                    weights["corporate"] = 10f;
                    weights["reality"] = 50f;
                    weights["gallows"] = 40f;
                    break;
            }

            // TODO: Override with config if available

            return weights;
        }

        private ToastMessage SelectByWeight(List<ToastMessage> templates)
        {
            if (templates.Count == 0) return null;
            if (templates.Count == 1) return templates[0];

            float totalWeight = templates.Sum(t => t.weight);
            float roll = (float)seededRandom.NextDouble() * totalWeight;

            float current = 0f;
            foreach (var template in templates)
            {
                current += template.weight;
                if (roll <= current)
                    return template;
            }

            return templates[templates.Count - 1];
        }

        private ToastPayload CreatePayload(ToastMessage template, ToastVoiceProfile voice, string triggerId)
        {
            var payload = new ToastPayload
            {
                id = template.id,
                triggerId = triggerId,
                voiceId = voice.VoiceId,
                handle = voice.GetHandleForMessage(template),
                interpolatedText = InterpolateTemplate(template.template),
                avatar = voice.GetIconForKey(template.iconKey),
                borderColor = voice.BorderColor,
                notificationSound = voice.GetAudioForKey(template.soundKey),
                timestamp = currentTime,
                actNumber = GetCurrentAct()
            };

            return payload;
        }

        private string InterpolateTemplate(string template)
        {
            if (string.IsNullOrEmpty(template)) return template;

            var result = template;

            // Replace placeholders
            if (gameStateProvider != null)
            {
                result = result.Replace("{time}", FormatTime(gameStateProvider.Timer));
                result = result.Replace("{blocked}", FormatGallons(gameStateProvider.GallonsBlocked));
                result = result.Replace("{escaped}", FormatGallons(gameStateProvider.GallonsEscaped));
                result = result.Replace("{integrity}", gameStateProvider.Integrity.ToString("F0"));
            }

            return result;
        }

        private string FormatTime(float seconds)
        {
            if (seconds < 60) return $"{seconds:F0}";
            if (seconds < 3600) return $"{seconds / 60:F0}";
            return $"{seconds / 3600:F1}";
        }

        private string FormatGallons(float gallons)
        {
            if (gallons < 1000) return gallons.ToString("F0");
            if (gallons < 1000000) return $"{gallons / 1000:F0}k";
            return $"{gallons / 1000000:F1}M";
        }

        private void HandleToastReady(ToastPayload payload)
        {
            OnToastDisplayed?.Invoke(payload);

            // Update debug info
            debugInfo.toastsVisibleCount++;

            // Update voice counts
            string key = $"{payload.voiceId}_act{payload.actNumber}";
            if (!debugInfo.voiceCountsByAct.ContainsKey(key))
                debugInfo.voiceCountsByAct[key] = 0;
            debugInfo.voiceCountsByAct[key]++;
        }

        private void HandleToastExpired(ToastPayload payload)
        {
            OnToastDismissed?.Invoke(payload);

            // Update debug info
            debugInfo.toastsVisibleCount = Mathf.Max(0, debugInfo.toastsVisibleCount - 1);
        }

        public void PauseToasting()
        {
            isPaused = true;
            toastQueue?.Pause();
        }

        public void ResumeToasting()
        {
            isPaused = false;
            toastQueue?.Resume();
        }

        public void StopToasting()
        {
            isActive = false;
            isPaused = false;

            // Unsubscribe from events
            if (gameStateProvider != null)
            {
                gameStateProvider.OnTimeUpdated -= HandleTimeUpdate;
                gameStateProvider.OnGallonsBlockedChanged -= HandleGallonsChanged;
                gameStateProvider.OnIntegrityChanged -= HandleIntegrityChanged;
                gameStateProvider.OnResupplyEvent -= HandleResupplyEvent;
            }

            // Clear queues
            pendingEvaluations.Clear();
            pendingTriggerIds.Clear();
            toastQueue?.Clear();
        }

        public string[] GetLoadErrors()
        {
            return loadErrors.ToArray();
        }

        public void ForceToast(string triggerId, string voiceId = null, int? actOverride = null)
        {
            int act = actOverride ?? GetCurrentAct();

            ToastVoiceProfile voice = null;
            if (!string.IsNullOrEmpty(voiceId))
            {
                voice = voiceProfiles.FirstOrDefault(p => p.VoiceId == voiceId);
            }

            if (voice == null)
            {
                // Respect act-based voice weights when no explicit voice is provided
                voice = SelectVoiceForAct(act);
            }

            if (voice == null)
            {
                Debug.LogError("[ToastManager] No voice profiles available for forced toast");
                return;
            }

            var templates = voice.GetTemplates(triggerId, act);
            if (templates.Count == 0)
            {
                Debug.LogWarning($"[ToastManager] No templates found for trigger {triggerId} in act {act}");
                return;
            }

            var template = SelectByWeight(templates);
            var payload = CreatePayload(template, voice, triggerId);

            toastQueue.Enqueue(payload);
            OnToastQueued?.Invoke(payload);
            sessionLog.LogToast(payload);
        }

        public ToastSessionLog GetSessionLog()
        {
            return sessionLog;
        }

        public ToastDebugInfo GetDebugInfo()
        {
            debugInfo.toastsQueuedCount = toastQueue?.GetQueuedCount() ?? 0;
            return debugInfo;
        }

        private class TriggerEvaluation
        {
            public ToastTrigger trigger;
            public float value;
            public TriggerContext context;
            public string voiceId;
        }
    }
}
