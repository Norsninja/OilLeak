using System;
using System.Collections.Generic;
using OilLeak.Toast.Data;

namespace OilLeak.Toast.Services
{
    public interface IToastService
    {
        // Service state
        bool IsReady { get; }
        string[] GetLoadErrors();

        // Toast events
        event Action<ToastPayload> OnToastQueued;
        event Action<ToastPayload> OnToastDisplayed;
        event Action<ToastPayload> OnToastDismissed;

        // Control
        void Initialize();
        void StartToasting();
        void PauseToasting();
        void ResumeToasting();
        void StopToasting();

        // Debug/Testing
        void ForceToast(string triggerId, string voiceId = null, int? actOverride = null);
        ToastSessionLog GetSessionLog();
        ToastDebugInfo GetDebugInfo();
    }

    // Payload sent to UI
    [System.Serializable]
    public class ToastPayload
    {
        public string id;
        public string triggerId;
        public string voiceId;
        public string handle;
        public string interpolatedText;
        public UnityEngine.Sprite avatar;
        public UnityEngine.Color borderColor;
        public UnityEngine.AudioClip notificationSound;
        public float timestamp;
        public int actNumber;
        public int duplicateCount = 1;

        public ToastPayload Clone()
        {
            return new ToastPayload
            {
                id = id,
                triggerId = triggerId,
                voiceId = voiceId,
                handle = handle,
                interpolatedText = interpolatedText,
                avatar = avatar,
                borderColor = borderColor,
                notificationSound = notificationSound,
                timestamp = timestamp,
                actNumber = actNumber,
                duplicateCount = duplicateCount
            };
        }
    }

    // Session logging for end screen
    [System.Serializable]
    public class ToastSessionLog
    {
        public List<ToastLogEntry> entries = new List<ToastLogEntry>();

        public void LogToast(ToastPayload payload)
        {
            entries.Add(new ToastLogEntry
            {
                timestamp = payload.timestamp,
                triggerId = payload.triggerId,
                voiceId = payload.voiceId,
                handle = payload.handle,
                displayedText = payload.interpolatedText,
                actNumber = payload.actNumber
            });
        }

        public TimelineData GenerateTimeline()
        {
            var timeline = new TimelineData();

            // Group by act
            var actGroups = new Dictionary<int, List<ToastLogEntry>>();
            foreach (var entry in entries)
            {
                if (!actGroups.ContainsKey(entry.actNumber))
                    actGroups[entry.actNumber] = new List<ToastLogEntry>();
                actGroups[entry.actNumber].Add(entry);
            }

            timeline.actGroups = actGroups;
            timeline.totalToasts = entries.Count;
            timeline.finalTimestamp = entries.Count > 0 ? entries[entries.Count - 1].timestamp : 0;

            return timeline;
        }
    }

    [System.Serializable]
    public class ToastLogEntry
    {
        public float timestamp;
        public string triggerId;
        public string voiceId;
        public string handle;
        public string displayedText;
        public int actNumber;
    }

    [System.Serializable]
    public class TimelineData
    {
        public Dictionary<int, List<ToastLogEntry>> actGroups;
        public int totalToasts;
        public float finalTimestamp;
    }

    // Debug information for DevHUD
    [System.Serializable]
    public class ToastDebugInfo
    {
        public int triggersEvaluatedLastSecond;
        public int toastsQueuedCount;
        public int toastsVisibleCount;
        public List<string> recentTriggerIds = new List<string>();
        public string currentAct;
        public Dictionary<string, int> voiceCountsByAct = new Dictionary<string, int>();
        public bool contentReady;
        public int totalMessagesLoaded;
        public List<string> loadErrors = new List<string>();
    }
}