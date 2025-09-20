using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace OilLeak.Toast.Services
{
    public class ToastQueue
    {
        private readonly ToastSystemConfig config;
        private readonly Queue<ToastPayload> pending;
        private readonly List<ActiveToast> visible;
        private bool isPaused;

        public event Action<ToastPayload> OnToastReady;
        public event Action<ToastPayload> OnToastExpired;

        public ToastQueue(ToastSystemConfig config)
        {
            this.config = config;
            pending = new Queue<ToastPayload>();
            visible = new List<ActiveToast>();
            isPaused = false;
        }

        public void Enqueue(ToastPayload payload)
        {
            if (payload == null) return;

            // Handle overflow based on policy
            if (config.overflowPolicy == OverflowPolicy.MergeIdentical)
            {
                // Check if identical toast is already queued
                var existing = pending.FirstOrDefault(p =>
                    p.triggerId == payload.triggerId &&
                    p.interpolatedText == payload.interpolatedText &&
                    p.voiceId == payload.voiceId);

                if (existing != null)
                {
                    existing.duplicateCount++;
                    Debug.Log($"[ToastQueue] Merged identical toast: {payload.triggerId} (count: {existing.duplicateCount})");
                    return;
                }
            }

            // Check queue capacity
            if (pending.Count >= config.maxQueueLength)
            {
                switch (config.overflowPolicy)
                {
                    case OverflowPolicy.DropOldest:
                        var dropped = pending.Dequeue();
                        Debug.Log($"[ToastQueue] Dropped oldest toast: {dropped.triggerId}");
                        break;

                    case OverflowPolicy.DropNewest:
                        Debug.Log($"[ToastQueue] Dropped newest toast (queue full): {payload.triggerId}");
                        return;

                    case OverflowPolicy.MergeIdentical:
                        // Already handled above
                        break;
                }
            }

            pending.Enqueue(payload);
            ProcessQueue();
        }

        public void Update(float deltaTime)
        {
            if (isPaused) return;

            // Update visible toasts
            for (int i = visible.Count - 1; i >= 0; i--)
            {
                var toast = visible[i];
                toast.timeRemaining -= deltaTime;

                if (toast.timeRemaining <= 0)
                {
                    visible.RemoveAt(i);
                    OnToastExpired?.Invoke(toast.payload);
                }
            }

            // Try to display more toasts if we have room
            ProcessQueue();
        }

        private void ProcessQueue()
        {
            if (isPaused) return;

            while (pending.Count > 0 && visible.Count < config.maxVisibleToasts)
            {
                var payload = pending.Dequeue();

                var activeToast = new ActiveToast
                {
                    payload = payload,
                    timeRemaining = config.defaultDisplayDuration
                };

                visible.Add(activeToast);
                OnToastReady?.Invoke(payload);
            }
        }

        public void Pause()
        {
            isPaused = true;
        }

        public void Resume()
        {
            isPaused = false;
            ProcessQueue();
        }

        public void Clear()
        {
            pending.Clear();
            visible.Clear();
        }

        public int GetQueuedCount()
        {
            return pending.Count;
        }

        public int GetVisibleCount()
        {
            return visible.Count;
        }

        public List<ToastPayload> GetVisibleToasts()
        {
            return visible.Select(v => v.payload).ToList();
        }

        private class ActiveToast
        {
            public ToastPayload payload;
            public float timeRemaining;
        }
    }

    [CreateAssetMenu(fileName = "ToastSystemConfig", menuName = "OilLeak/Toast/System Config")]
    public class ToastSystemConfig : ScriptableObject
    {
        [Header("Display Settings")]
        public int maxVisibleToasts = 3;
        public float defaultDisplayDuration = 2.5f;

        [Header("Queue Settings")]
        public int maxQueueLength = 10;
        public OverflowPolicy overflowPolicy = OverflowPolicy.DropOldest;

        [Header("Processing")]
        public float batchProcessInterval = 0.25f;
        public int maxTriggersPerBatch = 5;

        [Header("Voice Weights by Act")]
        public ActVoiceWeight[] voiceWeightsByAct = new ActVoiceWeight[]
        {
            new ActVoiceWeight { act = 1, corporateWeight = 60f, realityWeight = 30f, gallowsWeight = 10f },
            new ActVoiceWeight { act = 2, corporateWeight = 33f, realityWeight = 33f, gallowsWeight = 34f },
            new ActVoiceWeight { act = 3, corporateWeight = 20f, realityWeight = 40f, gallowsWeight = 40f },
            new ActVoiceWeight { act = 4, corporateWeight = 10f, realityWeight = 50f, gallowsWeight = 40f }
        };
    }

    public enum OverflowPolicy
    {
        DropOldest,
        DropNewest,
        MergeIdentical
    }

    [System.Serializable]
    public class ActVoiceWeight
    {
        public int act;
        public float corporateWeight;
        public float realityWeight;
        public float gallowsWeight;
    }
}