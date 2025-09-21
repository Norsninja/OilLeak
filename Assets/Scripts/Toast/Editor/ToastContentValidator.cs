using UnityEngine;
using UnityEditor;
using OilLeak.Toast.Data;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace OilLeak.Toast.Editor
{
    public class ToastContentValidator : AssetPostprocessor
    {
        private const string TOAST_CONTENT_PATH = "Assets/StreamingAssets/ToastContent";
        private static readonly HashSet<string> validTriggerIds = new HashSet<string>();

        // Auto-validate on import
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            bool shouldValidate = false;

            foreach (string path in importedAssets)
            {
                if (path.Contains("StreamingAssets/ToastContent") && path.EndsWith(".json"))
                {
                    shouldValidate = true;
                    break;
                }
            }

            if (shouldValidate)
            {
                ValidateAllToastContent(false);
            }
        }

        [MenuItem("OilLeak/Toast/Validate All Content")]
        public static void ValidateAllContentMenuItem()
        {
            ValidateAllToastContent(true);
        }

        [MenuItem("OilLeak/Toast/Validate and Fix Common Issues")]
        public static void ValidateAndFixMenuItem()
        {
            ValidateAllToastContent(true, true);
        }

        public static bool ValidateAllToastContent(bool showSuccessDialog = false, bool attemptFixes = false)
        {
            LoadValidTriggerIds();

            var loader = new ToastContentLoader();
            var allErrors = new List<string>();
            var validatedFiles = new List<string>();

            // Check if directory exists
            string fullPath = Path.Combine(Application.dataPath, "StreamingAssets/ToastContent");
            if (!Directory.Exists(fullPath))
            {
                EditorUtility.DisplayDialog("Toast Validation Error",
                    "ToastContent directory not found at:\n" + fullPath, "OK");
                return false;
            }

            // Validate each JSON file
            string[] jsonFiles = Directory.GetFiles(fullPath, "*.json");

            if (jsonFiles.Length == 0)
            {
                EditorUtility.DisplayDialog("Toast Validation Warning",
                    "No JSON files found in ToastContent directory", "OK");
                return false;
            }

            foreach (string filePath in jsonFiles)
            {
                string fileName = Path.GetFileName(filePath);
                string content = File.ReadAllText(filePath);

                var (data, result) = loader.Parse(content);

                if (!result.isValid)
                {
                    allErrors.Add($"\n[{fileName}]");
                    foreach (var error in result.errors)
                    {
                        allErrors.Add($"  • {error}");
                    }
                }
                else
                {
                    // Additional validation against trigger definitions
                    var triggerErrors = ValidateAgainstTriggerDefinitions(data, fileName);
                    if (triggerErrors.Count > 0)
                    {
                        allErrors.Add($"\n[{fileName}] - Trigger validation:");
                        allErrors.AddRange(triggerErrors.Select(e => $"  • {e}"));
                    }
                    else
                    {
                        validatedFiles.Add(fileName);
                    }

                    // Validate act coverage
                    var coverageErrors = ValidateActCoverage(data);
                    if (coverageErrors.Count > 0)
                    {
                        allErrors.Add($"\n[{fileName}] - Coverage warnings:");
                        allErrors.AddRange(coverageErrors.Select(e => $"  • {e}"));
                    }
                }
            }

            // Report results
            if (allErrors.Count > 0)
            {
                string errorMessage = "Toast Content Validation Failed!\n\n" +
                                    string.Join("\n", allErrors);

                if (attemptFixes)
                {
                    errorMessage += "\n\nNote: Auto-fix not implemented yet. Please fix manually.";
                }

                EditorUtility.DisplayDialog("Validation Failed", errorMessage, "OK");

                // Also log to console for easier reading
                Debug.LogError($"[ToastValidator] Validation failed with {allErrors.Count} errors:\n" +
                             string.Join("\n", allErrors));

                return false;
            }

            // Success
            if (showSuccessDialog)
            {
                string successMessage = $"All toast content validated successfully!\n\n" +
                                      $"Files validated: {validatedFiles.Count}\n" +
                                      string.Join("\n", validatedFiles.Select(f => $"  ✓ {f}"));

                EditorUtility.DisplayDialog("Validation Success", successMessage, "OK");
            }

            Debug.Log($"[ToastValidator] All {validatedFiles.Count} toast files validated successfully");
            return true;
        }

        private static void LoadValidTriggerIds()
        {
            validTriggerIds.Clear();

            // Dynamically load trigger IDs from actual ToastTrigger ScriptableObjects
            // This ensures we always have the current list of valid triggers
            var triggers = Resources.LoadAll<ToastTrigger>("ToastTriggers");

            if (triggers != null && triggers.Length > 0)
            {
                foreach (var trigger in triggers)
                {
                    if (trigger != null && !string.IsNullOrEmpty(trigger.Id))
                    {
                        validTriggerIds.Add(trigger.Id);
                    }
                }

                Debug.Log($"[ToastValidator] Loaded {validTriggerIds.Count} trigger IDs from ToastTrigger assets");
            }
            else
            {
                Debug.LogWarning("[ToastValidator] No ToastTrigger assets found in Resources/ToastTriggers. Using fallback list.");

                // Fallback to essential triggers if assets aren't generated yet
                // This includes our new time_0_5 and time_1 triggers
                validTriggerIds.Add("time_0_5");
                validTriggerIds.Add("time_1");
                validTriggerIds.Add("time_3");
                validTriggerIds.Add("time_5");
                validTriggerIds.Add("time_8");
                validTriggerIds.Add("time_10");
                validTriggerIds.Add("time_15");
                validTriggerIds.Add("time_20");
                validTriggerIds.Add("time_25");
                validTriggerIds.Add("time_30");
                validTriggerIds.Add("time_45");
                validTriggerIds.Add("time_60");
                validTriggerIds.Add("time_90");
                validTriggerIds.Add("time_120");

                // Gallon triggers
                validTriggerIds.Add("gallons_10k");
                validTriggerIds.Add("gallons_25k");
                validTriggerIds.Add("gallons_50k");
                validTriggerIds.Add("gallons_100k");
                validTriggerIds.Add("gallons_250k");
                validTriggerIds.Add("gallons_500k");
                validTriggerIds.Add("gallons_1m");
                validTriggerIds.Add("gallons_2m");
                validTriggerIds.Add("gallons_5m");
                validTriggerIds.Add("gallons_10m");
                validTriggerIds.Add("gallons_20m");
                validTriggerIds.Add("gallons_50m");

                // Integrity triggers
                validTriggerIds.Add("integrity_80");
                validTriggerIds.Add("integrity_70");
                validTriggerIds.Add("integrity_60");
                validTriggerIds.Add("integrity_40");
                validTriggerIds.Add("integrity_30");
                validTriggerIds.Add("integrity_20");
                validTriggerIds.Add("integrity_10");

                // Resupply triggers
                validTriggerIds.Add("resupply_first_drop");
                validTriggerIds.Add("resupply_barge");
                validTriggerIds.Add("resupply_emergency");
                validTriggerIds.Add("resupply_delayed");
                validTriggerIds.Add("resupply_failed");

                // Special triggers that were in the original list
                validTriggerIds.Add("special_junk_shot");
                validTriggerIds.Add("special_top_kill");
                validTriggerIds.Add("special_prayer_circle");
                validTriggerIds.Add("special_corexit");
                validTriggerIds.Add("special_mutation");
                validTriggerIds.Add("special_final_science");
                validTriggerIds.Add("special_epitaph");
                validTriggerIds.Add("game_start");
                validTriggerIds.Add("resupply_first");
            }

            // Always add special triggers (they might not be in Resources yet)
            validTriggerIds.Add("special_junk_shot");
            validTriggerIds.Add("special_kevin_costner");
            validTriggerIds.Add("special_thoughts_prayers");
            validTriggerIds.Add("special_item_sludge_50");
            validTriggerIds.Add("special_item_sludge_100");
            validTriggerIds.Add("special_mike_2010");
            validTriggerIds.Add("special_corexit");
            validTriggerIds.Add("special_mutation");
            validTriggerIds.Add("special_end_times");
            validTriggerIds.Add("special_final_denial");
            validTriggerIds.Add("special_final_science");
            validTriggerIds.Add("special_epitaph");
            validTriggerIds.Add("special_final_joke");

            // TODO: Load from actual ToastTrigger ScriptableObjects when they exist
            // var triggers = Resources.LoadAll<ToastTrigger>("Triggers");
            // foreach (var trigger in triggers)
            // {
            //     validTriggerIds.Add(trigger.id);
            // }
        }

        private static List<string> ValidateAgainstTriggerDefinitions(ToastVoiceData data, string fileName)
        {
            var errors = new List<string>();

            foreach (var act in data.acts.Values)
            {
                foreach (var message in act.messages)
                {
                    if (!validTriggerIds.Contains(message.triggerId))
                    {
                        errors.Add($"Unknown trigger ID '{message.triggerId}' in message '{message.id}'");
                    }
                }
            }

            return errors;
        }

        private static List<string> ValidateActCoverage(ToastVoiceData data)
        {
            var warnings = new List<string>();
            var triggersByAct = new Dictionary<string, HashSet<string>>();

            // Collect triggers per act
            foreach (var kvp in data.acts)
            {
                var actTriggers = new HashSet<string>();
                foreach (var msg in kvp.Value.messages)
                {
                    actTriggers.Add(msg.triggerId);
                }
                triggersByAct[kvp.Key] = actTriggers;
            }

            // Check for common triggers missing from acts
            var commonTriggers = new[] { "time_3", "time_5", "gallons_10k", "integrity_80" };

            foreach (var kvp in triggersByAct)
            {
                if (kvp.Key == "act1")
                {
                    foreach (var trigger in commonTriggers)
                    {
                        if (!kvp.Value.Contains(trigger) && trigger != "integrity_80")
                        {
                            warnings.Add($"Act 1 missing common early-game trigger: {trigger}");
                        }
                    }
                }

                // Warn if act has very few messages
                if (kvp.Value.Count < 5)
                {
                    warnings.Add($"{kvp.Key} only has {kvp.Value.Count} unique triggers (recommend 5+)");
                }
            }

            // Check weight distribution
            foreach (var act in data.acts)
            {
                float totalWeight = act.Value.messages.Sum(m => m.weight);
                if (totalWeight < 20)
                {
                    warnings.Add($"{act.Key} has low total weight ({totalWeight}), may not trigger often");
                }
            }

            return warnings;
        }

        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            // Run validation on Unity startup in editor
            EditorApplication.delayCall += () =>
            {
                if (!EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    ValidateAllToastContent(false);
                }
            };
        }
    }
}