using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using OilLeak.Toast.Data;
using System.IO;

namespace OilLeak.Editor
{
    /// <summary>
    /// Editor tool to generate ToastTrigger assets from voice profile trigger IDs
    /// </summary>
    public class ToastTriggerGenerator : EditorWindow
    {
        private List<string> discoveredTriggerIds = new List<string>();
        private Dictionary<string, TriggerInfo> triggerInfoMap = new Dictionary<string, TriggerInfo>();
        private Vector2 scrollPosition;

        private class TriggerInfo
        {
            public string id;
            public TriggerType type;
            public float[] thresholds;
            public string conditionType;
            public string conditionOp;
            public float conditionValue;
            public bool isValid;
            public string warning;
        }

        [MenuItem("OilLeak/Toast System/Generate Triggers from Voice Profiles")]
        public static void ShowWindow()
        {
            var window = GetWindow<ToastTriggerGenerator>("Toast Trigger Generator");
            window.minSize = new Vector2(600, 400);
            window.ScanVoiceProfiles();
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("Toast Trigger Generator", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            if (GUILayout.Button("Scan Voice Profiles", GUILayout.Height(30)))
            {
                ScanVoiceProfiles();
            }

            EditorGUILayout.Space();

            if (discoveredTriggerIds.Count > 0)
            {
                EditorGUILayout.LabelField($"Found {discoveredTriggerIds.Count} unique trigger IDs", EditorStyles.helpBox);
                EditorGUILayout.Space();

                scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

                foreach (var kvp in triggerInfoMap.OrderBy(x => x.Key))
                {
                    var info = kvp.Value;

                    EditorGUILayout.BeginHorizontal();

                    // Show trigger ID
                    EditorGUILayout.LabelField(info.id, GUILayout.Width(150));

                    // Show interpreted type
                    EditorGUILayout.LabelField(info.type.ToString(), GUILayout.Width(100));

                    // Show thresholds/conditions
                    if (info.thresholds != null && info.thresholds.Length > 0)
                    {
                        EditorGUILayout.LabelField($"Value: {string.Join(", ", info.thresholds)}", GUILayout.Width(150));
                    }

                    // Show warnings
                    if (!info.isValid)
                    {
                        EditorGUILayout.LabelField(info.warning, EditorStyles.miniLabel);
                    }

                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.EndScrollView();

                EditorGUILayout.Space();

                if (GUILayout.Button("Generate All Trigger Assets", GUILayout.Height(40)))
                {
                    GenerateTriggerAssets();
                }
            }
            else
            {
                EditorGUILayout.HelpBox("Click 'Scan Voice Profiles' to discover trigger IDs", MessageType.Info);
            }
        }

        private void ScanVoiceProfiles()
        {
            discoveredTriggerIds.Clear();
            triggerInfoMap.Clear();

            // Find all ToastVoiceProfile assets
            var guids = AssetDatabase.FindAssets("t:ToastVoiceProfile");
            Debug.Log($"[TriggerGenerator] Found {guids.Length} voice profiles to scan");

            HashSet<string> uniqueTriggerIds = new HashSet<string>();

            // Add essential early-game triggers that should always exist
            uniqueTriggerIds.Add("game_start");
            uniqueTriggerIds.Add("time_0_5");  // 30 seconds
            uniqueTriggerIds.Add("time_1");    // 60 seconds

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var profile = AssetDatabase.LoadAssetAtPath<ToastVoiceProfile>(path);

                if (profile != null)
                {
                    // Initialize the profile to load its data
                    profile.Initialize();

                    // Get messages from all acts (1-5)
                    for (int actNumber = 1; actNumber <= 5; actNumber++)
                    {
                        var messages = profile.GetAllTemplatesForAct(actNumber);
                        if (messages != null)
                        {
                            foreach (var msg in messages)
                            {
                                if (!string.IsNullOrEmpty(msg.triggerId))
                                {
                                    uniqueTriggerIds.Add(msg.triggerId);
                                }
                            }
                        }
                    }
                }
            }

            discoveredTriggerIds = uniqueTriggerIds.OrderBy(x => x).ToList();
            Debug.Log($"[TriggerGenerator] Discovered {discoveredTriggerIds.Count} unique trigger IDs");

            // Parse each trigger ID
            foreach (var triggerId in discoveredTriggerIds)
            {
                triggerInfoMap[triggerId] = ParseTriggerId(triggerId);
            }
        }

        private TriggerInfo ParseTriggerId(string triggerId)
        {
            var info = new TriggerInfo { id = triggerId, isValid = true };

            // Time patterns: time_3, time_5, time_8, etc.
            var timeMatch = Regex.Match(triggerId, @"^time_(\d+)$");
            if (timeMatch.Success)
            {
                info.type = TriggerType.Time;
                float minutes = float.Parse(timeMatch.Groups[1].Value);
                info.thresholds = new float[] { minutes * 60f }; // Convert to seconds
                info.conditionType = "time_elapsed";
                info.conditionOp = ">=";
                info.conditionValue = minutes * 60f;
                return info;
            }

            // Gallons patterns: gallons_10k, gallons_25k, etc.
            var gallonsMatch = Regex.Match(triggerId, @"^gallons_(\d+)k?$");
            if (gallonsMatch.Success)
            {
                info.type = TriggerType.Gallons;
                float gallons = float.Parse(gallonsMatch.Groups[1].Value);
                if (triggerId.Contains("k"))
                {
                    gallons *= 1000f;
                }
                info.thresholds = new float[] { gallons };
                info.conditionType = "gallons_blocked";
                info.conditionOp = ">=";
                info.conditionValue = gallons;
                return info;
            }

            // Integrity patterns: integrity_75, integrity_50, integrity_critical
            var integrityMatch = Regex.Match(triggerId, @"^integrity_(\d+)$");
            if (integrityMatch.Success)
            {
                info.type = TriggerType.Integrity;
                float integrity = float.Parse(integrityMatch.Groups[1].Value);
                info.thresholds = new float[] { integrity };
                info.conditionType = "integrity";
                info.conditionOp = "<=";
                info.conditionValue = integrity;
                return info;
            }

            // Special integrity cases
            if (triggerId == "integrity_critical")
            {
                info.type = TriggerType.Integrity;
                info.thresholds = new float[] { 25f };
                info.conditionType = "integrity";
                info.conditionOp = "<=";
                info.conditionValue = 25f;
                return info;
            }

            // Resupply patterns
            if (triggerId.StartsWith("resupply_"))
            {
                info.type = TriggerType.Resupply;
                info.thresholds = new float[] { 0f };

                if (triggerId == "resupply_first")
                {
                    info.conditionType = "resupply_count";
                    info.conditionOp = "==";
                    info.conditionValue = 1f;
                }
                else
                {
                    info.conditionType = "resupply_count";
                    info.conditionOp = ">";
                    info.conditionValue = 0f;
                }
                return info;
            }

            // Game start
            if (triggerId == "game_start")
            {
                info.type = TriggerType.Special;
                info.thresholds = new float[] { 5f };
                info.conditionType = "time_elapsed";
                info.conditionOp = ">=";
                info.conditionValue = 5f;
                return info;
            }

            // Early time triggers (fractional minutes)
            if (triggerId == "time_0_5")
            {
                info.type = TriggerType.Time;
                info.thresholds = new float[] { 30f };
                info.conditionType = "time_elapsed";
                info.conditionOp = ">=";
                info.conditionValue = 30f;
                return info;
            }

            if (triggerId == "time_1")
            {
                info.type = TriggerType.Time;
                info.thresholds = new float[] { 60f };
                info.conditionType = "time_elapsed";
                info.conditionOp = ">=";
                info.conditionValue = 60f;
                return info;
            }

            // Items degraded patterns
            var itemsMatch = Regex.Match(triggerId, @"^items_(\d+)$");
            if (itemsMatch.Success)
            {
                info.type = TriggerType.Milestone;
                float items = float.Parse(itemsMatch.Groups[1].Value);
                info.thresholds = new float[] { items };
                info.conditionType = "items_degraded";
                info.conditionOp = ">=";
                info.conditionValue = items;
                return info;
            }

            // Default fallback
            info.type = TriggerType.Special;
            info.thresholds = new float[] { 0f };
            info.isValid = false;
            info.warning = "Unknown pattern - needs manual configuration";
            Debug.LogWarning($"[TriggerGenerator] Unknown trigger pattern: {triggerId}");

            return info;
        }

        private void GenerateTriggerAssets()
        {
            // Create directory if it doesn't exist
            string basePath = "Assets/ScriptableObjects/ToastSystem/Triggers";
            if (!AssetDatabase.IsValidFolder("Assets/ScriptableObjects"))
            {
                AssetDatabase.CreateFolder("Assets", "ScriptableObjects");
            }
            if (!AssetDatabase.IsValidFolder("Assets/ScriptableObjects/ToastSystem"))
            {
                AssetDatabase.CreateFolder("Assets/ScriptableObjects", "ToastSystem");
            }
            if (!AssetDatabase.IsValidFolder(basePath))
            {
                AssetDatabase.CreateFolder("Assets/ScriptableObjects/ToastSystem", "Triggers");
            }

            int created = 0;
            int updated = 0;

            foreach (var kvp in triggerInfoMap)
            {
                var info = kvp.Value;
                string assetPath = $"{basePath}/{info.id}.asset";

                // Check if asset already exists
                var existingAsset = AssetDatabase.LoadAssetAtPath<ToastTrigger>(assetPath);

                if (existingAsset != null)
                {
                    // Update existing asset
                    UpdateTriggerAsset(existingAsset, info);
                    updated++;
                    EditorUtility.SetDirty(existingAsset);
                }
                else
                {
                    // Create new asset
                    var trigger = CreateTriggerAsset(info);
                    AssetDatabase.CreateAsset(trigger, assetPath);
                    created++;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Now move them to Resources so ToastManager can find them
            string resourcesPath = "Assets/Resources/ToastTriggers";
            if (!AssetDatabase.IsValidFolder("Assets/Resources/ToastTriggers"))
            {
                if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                {
                    AssetDatabase.CreateFolder("Assets", "Resources");
                }
                AssetDatabase.CreateFolder("Assets/Resources", "ToastTriggers");
            }

            // Copy all triggers to Resources
            foreach (var kvp in triggerInfoMap)
            {
                var info = kvp.Value;
                string sourcePath = $"{basePath}/{info.id}.asset";
                string destPath = $"{resourcesPath}/{info.id}.asset";

                if (!File.Exists(Path.Combine(Application.dataPath, "..", destPath)))
                {
                    AssetDatabase.CopyAsset(sourcePath, destPath);
                }
            }

            AssetDatabase.Refresh();

            Debug.Log($"[TriggerGenerator] Created {created} new triggers, updated {updated} existing triggers");
            EditorUtility.DisplayDialog("Success",
                $"Generated {created} new trigger assets\nUpdated {updated} existing triggers\n\nTriggers saved to:\n{basePath}\nand copied to:\n{resourcesPath}",
                "OK");
        }

        private ToastTrigger CreateTriggerAsset(TriggerInfo info)
        {
            var trigger = ScriptableObject.CreateInstance<ToastTrigger>();
            UpdateTriggerAsset(trigger, info);
            return trigger;
        }

        private void UpdateTriggerAsset(ToastTrigger trigger, TriggerInfo info)
        {
            // Use reflection to set private fields since they're serialized
            var type = trigger.GetType();

            var idField = type.GetField("id", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            idField?.SetValue(trigger, info.id);

            var typeField = type.GetField("type", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            typeField?.SetValue(trigger, info.type);

            var descField = type.GetField("description", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            descField?.SetValue(trigger, $"Auto-generated trigger for {info.id}");

            var thresholdField = type.GetField("thresholds", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            thresholdField?.SetValue(trigger, info.thresholds);

            // Set up conditions if we have them
            if (!string.IsNullOrEmpty(info.conditionType))
            {
                var conditions = new List<TriggerCondition>
                {
                    new TriggerCondition
                    {
                        type = info.conditionType,
                        op = info.conditionOp,
                        value = info.conditionValue
                    }
                };

                var conditionsField = type.GetField("conditions", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                conditionsField?.SetValue(trigger, conditions);
            }

            // Set reasonable defaults for cooldowns based on type
            var globalCooldownField = type.GetField("globalCooldown", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var perVoiceCooldownField = type.GetField("perVoiceCooldown", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            switch (info.type)
            {
                case TriggerType.Time:
                    globalCooldownField?.SetValue(trigger, 30f);
                    perVoiceCooldownField?.SetValue(trigger, 60f);
                    break;
                case TriggerType.Integrity:
                    globalCooldownField?.SetValue(trigger, 45f);
                    perVoiceCooldownField?.SetValue(trigger, 90f);
                    break;
                case TriggerType.Resupply:
                    globalCooldownField?.SetValue(trigger, 20f);
                    perVoiceCooldownField?.SetValue(trigger, 40f);
                    break;
                default:
                    globalCooldownField?.SetValue(trigger, 30f);
                    perVoiceCooldownField?.SetValue(trigger, 60f);
                    break;
            }

            // Set priority based on type
            var priorityField = type.GetField("priority", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            switch (info.type)
            {
                case TriggerType.Integrity:
                    priorityField?.SetValue(trigger, 70); // High priority for integrity warnings
                    break;
                case TriggerType.Special:
                    priorityField?.SetValue(trigger, 80); // Highest for special events
                    break;
                default:
                    priorityField?.SetValue(trigger, 50); // Normal priority
                    break;
            }
        }
    }
}