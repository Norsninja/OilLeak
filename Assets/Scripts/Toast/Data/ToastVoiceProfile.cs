using UnityEngine;
using System.Collections.Generic;
using System.IO;

namespace OilLeak.Toast.Data
{
    [CreateAssetMenu(fileName = "ToastVoiceProfile", menuName = "OilLeak/Toast/Voice Profile")]
    public class ToastVoiceProfile : ScriptableObject
    {
        [Header("Voice Identity")]
        [SerializeField] private string voiceId;
        [SerializeField] private string defaultHandle;
        [SerializeField] private Sprite defaultAvatar;
        [SerializeField] private Color borderColor = Color.white;

        [Header("Audio")]
        [SerializeField] private AudioClip defaultNotificationSound;
        [SerializeField] private List<AudioAssetMapping> audioOverrides = new List<AudioAssetMapping>();

        [Header("Icons")]
        [SerializeField] private List<IconAssetMapping> iconOverrides = new List<IconAssetMapping>();

        [Header("Content Source")]
        [SerializeField] private TextAsset messagesJsonFile;
        [SerializeField] private bool useStreamingAssets = true;
        [SerializeField] private string streamingAssetFileName = "corporate_voice.json";

        // Runtime data
        private ToastVoiceData parsedData;
        private Dictionary<string, AudioClip> audioLookup;
        private Dictionary<string, Sprite> iconLookup;
        private bool isInitialized = false;
        private bool contentReady = false;
        private ValidationResult validationResult;

        public string VoiceId => voiceId;
        public string DefaultHandle => defaultHandle;
        public Sprite DefaultAvatar => defaultAvatar;
        public Color BorderColor => borderColor;
        public AudioClip DefaultSound => defaultNotificationSound;
        public bool ContentReady => contentReady;
        public ValidationResult ValidationResult => validationResult;

        public void Initialize()
        {
            if (isInitialized) return;

            // Build lookup dictionaries
            audioLookup = new Dictionary<string, AudioClip>();
            foreach (var mapping in audioOverrides)
            {
                if (!string.IsNullOrEmpty(mapping.key) && mapping.audioClip != null)
                {
                    audioLookup[mapping.key] = mapping.audioClip;
                }
            }

            iconLookup = new Dictionary<string, Sprite>();
            foreach (var mapping in iconOverrides)
            {
                if (!string.IsNullOrEmpty(mapping.key) && mapping.sprite != null)
                {
                    iconLookup[mapping.key] = mapping.sprite;
                }
            }

            // Load and parse JSON
            string jsonContent = LoadJsonContent();
            if (!string.IsNullOrEmpty(jsonContent))
            {
                var loader = new ToastContentLoader();
                var (data, result) = loader.Parse(jsonContent);

                validationResult = result;

                if (result.isValid)
                {
                    parsedData = data;

                    // Override with ScriptableObject values if they exist
                    if (!string.IsNullOrEmpty(voiceId))
                        parsedData.voiceId = voiceId;
                    if (!string.IsNullOrEmpty(defaultHandle))
                        parsedData.defaultHandle = defaultHandle;

                    contentReady = true;
                    isInitialized = true;
                    Debug.Log($"[ToastVoiceProfile] Successfully loaded {name} with {CountTotalMessages()} messages");
                }
                else
                {
                    contentReady = false;
                    Debug.LogError($"[ToastVoiceProfile] Failed to parse JSON for {name}:\n" +
                                 string.Join("\n", result.errors));
                }
            }
            else
            {
                validationResult = new ValidationResult();
                validationResult.AddError("No JSON content found");
                contentReady = false;
                Debug.LogError($"[ToastVoiceProfile] No JSON content found for {name}");
            }
        }

        private string LoadJsonContent()
        {
            // Try TextAsset first (for testing/overrides)
            if (messagesJsonFile != null)
            {
                return messagesJsonFile.text;
            }

            // Load from StreamingAssets
            if (useStreamingAssets && !string.IsNullOrEmpty(streamingAssetFileName))
            {
                string path = Path.Combine(Application.streamingAssetsPath, "ToastContent", streamingAssetFileName);

                if (File.Exists(path))
                {
                    return File.ReadAllText(path);
                }
                else
                {
                    Debug.LogError($"[ToastVoiceProfile] File not found: {path}");
                }
            }

            return null;
        }

        public List<ToastMessage> GetTemplates(string triggerId, int actNumber)
        {
            if (!isInitialized)
            {
                Initialize();
            }

            if (parsedData == null || parsedData.acts == null)
            {
                Debug.LogWarning($"[ToastVoiceProfile] No parsed data available for {name}");
                return new List<ToastMessage>();
            }

            string actKey = $"act{actNumber}";
            if (!parsedData.acts.ContainsKey(actKey))
            {
                Debug.LogWarning($"[ToastVoiceProfile] No act '{actKey}' found in {name}");
                return new List<ToastMessage>();
            }

            var act = parsedData.acts[actKey];
            var matchingMessages = new List<ToastMessage>();

            foreach (var message in act.messages)
            {
                if (message.triggerId == triggerId)
                {
                    matchingMessages.Add(message);
                }
            }

            return matchingMessages;
        }

        public List<ToastMessage> GetAllTemplatesForAct(int actNumber)
        {
            if (!isInitialized)
            {
                Initialize();
            }

            if (parsedData == null || parsedData.acts == null)
            {
                return new List<ToastMessage>();
            }

            string actKey = $"act{actNumber}";
            if (!parsedData.acts.ContainsKey(actKey))
            {
                return new List<ToastMessage>();
            }

            return new List<ToastMessage>(parsedData.acts[actKey].messages);
        }

        public AudioClip GetAudioForKey(string key)
        {
            if (string.IsNullOrEmpty(key))
                return defaultNotificationSound;

            if (audioLookup != null && audioLookup.TryGetValue(key, out AudioClip clip))
                return clip;

            return defaultNotificationSound;
        }

        public Sprite GetIconForKey(string key)
        {
            if (string.IsNullOrEmpty(key))
                return defaultAvatar;

            if (iconLookup != null && iconLookup.TryGetValue(key, out Sprite sprite))
                return sprite;

            return defaultAvatar;
        }

        public string GetHandleForMessage(ToastMessage message)
        {
            if (!string.IsNullOrEmpty(message.handleOverride))
                return message.handleOverride;

            if (parsedData != null && !string.IsNullOrEmpty(parsedData.defaultHandle))
                return parsedData.defaultHandle;

            return defaultHandle;
        }

        public ToastVoiceData GetParsedData()
        {
            if (!isInitialized)
            {
                Initialize();
            }
            return parsedData;
        }

        private void OnValidate()
        {
            // Reset initialization when values change in inspector
            isInitialized = false;
            contentReady = false;
        }

        private int CountTotalMessages()
        {
            if (parsedData?.acts == null) return 0;

            int count = 0;
            foreach (var act in parsedData.acts.Values)
            {
                if (act.messages != null)
                    count += act.messages.Count;
            }
            return count;
        }

        [System.Serializable]
        public class AudioAssetMapping
        {
            public string key;
            public AudioClip audioClip;
        }

        [System.Serializable]
        public class IconAssetMapping
        {
            public string key;
            public Sprite sprite;
        }
    }
}