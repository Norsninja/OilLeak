using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace OilLeak.News
{
    /// <summary>
    /// Loads and parses news ticker content from JSON.
    /// Handles WebGL vs standalone file loading.
    /// </summary>
    public class NewsTickerContentLoader
    {
        public (NewsTickerContent content, List<string> errors) LoadFromStreamingAssets(string fileName)
        {
            var errors = new List<string>();
            string jsonContent = null;

            // Build path to file
            string filePath = Path.Combine(Application.streamingAssetsPath, fileName);

            try
            {
                // WebGL requires UnityWebRequest
                #if UNITY_WEBGL && !UNITY_EDITOR
                    jsonContent = LoadWebGL(filePath);
                #else
                    jsonContent = LoadStandalone(filePath);
                #endif

                if (string.IsNullOrEmpty(jsonContent))
                {
                    errors.Add($"Failed to load {fileName} - file empty or not found");
                    return (null, errors);
                }

                // Parse JSON
                var content = ParseContent(jsonContent, errors);

                // Validate content
                ValidateContent(content, errors);

                return (content, errors);
            }
            catch (Exception ex)
            {
                errors.Add($"Error loading {fileName}: {ex.Message}");
                return (null, errors);
            }
        }

        private string LoadStandalone(string path)
        {
            if (File.Exists(path))
            {
                return File.ReadAllText(path);
            }
            return null;
        }

        private string LoadWebGL(string path)
        {
            // For WebGL, we need to use UnityWebRequest synchronously
            // This is not ideal but acceptable for initialization
            using (var request = UnityWebRequest.Get(path))
            {
                request.SendWebRequest();

                // Wait for completion (blocking - only use during init!)
                while (!request.isDone)
                {
                    // Spin wait - not ideal but necessary for synchronous load
                }

                if (request.result == UnityWebRequest.Result.Success)
                {
                    return request.downloadHandler.text;
                }
                else
                {
                    Debug.LogError($"[NewsTickerContentLoader] WebGL load failed: {request.error}");
                    return null;
                }
            }
        }

        private NewsTickerContent ParseContent(string json, List<string> errors)
        {
            try
            {
                // Unity's JsonUtility doesn't handle Dictionary well
                // We need to manually parse the tier structure
                var content = new NewsTickerContent();

                // Parse the base structure
                var tempContent = JsonUtility.FromJson<TempNewsContent>(json);
                content.version = tempContent.version;
                content.breaking = tempContent.breaking;

                // Parse tiers manually
                content.tiers = new Dictionary<string, TierContent>();

                // Extract tier objects from JSON
                var tierParser = new SimpleTierParser();
                content.tiers = tierParser.ParseTiers(json);

                return content;
            }
            catch (Exception ex)
            {
                errors.Add($"JSON parse error: {ex.Message}");
                return null;
            }
        }

        private void ValidateContent(NewsTickerContent content, List<string> errors)
        {
            if (content == null)
            {
                errors.Add("Content is null after parsing");
                return;
            }

            // Check version
            if (string.IsNullOrEmpty(content.version))
            {
                errors.Add("No version specified in content");
            }

            // Check tiers
            if (content.tiers == null || content.tiers.Count == 0)
            {
                errors.Add("No tiers found in content");
            }
            else
            {
                // Validate each tier has content
                for (int tier = 1; tier <= 5; tier++)
                {
                    var tierKey = $"tier{tier}";
                    if (!content.tiers.ContainsKey(tierKey))
                    {
                        errors.Add($"Missing {tierKey} in content");
                    }
                    else if (content.tiers[tierKey].messages == null || content.tiers[tierKey].messages.Count == 0)
                    {
                        errors.Add($"{tierKey} has no messages");
                    }
                }
            }

            // Check breaking news (optional but recommended)
            if (content.breaking == null || content.breaking.Count == 0)
            {
                errors.Add("No breaking news templates found (optional but recommended)");
            }
        }

        /// <summary>
        /// Temporary structure for initial parsing since JsonUtility can't handle Dictionary
        /// </summary>
        [System.Serializable]
        private class TempNewsContent
        {
            public string version;
            public List<BreakingNewsTemplate> breaking;
        }

        /// <summary>
        /// Simple manual parser for tier structure
        /// </summary>
        private class SimpleTierParser
        {
            public Dictionary<string, TierContent> ParseTiers(string json)
            {
                var tiers = new Dictionary<string, TierContent>();

                // This is a simplified parser - in production we'd use a proper JSON library
                // For MVP, we'll parse the tier structure manually

                for (int tierNum = 1; tierNum <= 5; tierNum++)
                {
                    var tierKey = $"tier{tierNum}";
                    var tierContent = ExtractTier(json, tierKey);

                    if (tierContent != null)
                    {
                        tiers[tierKey] = tierContent;
                    }
                }

                return tiers;
            }

            private TierContent ExtractTier(string json, string tierKey)
            {
                try
                {
                    // Find the tier object in the JSON
                    string searchPattern = $"\"{tierKey}\"";
                    int tierStart = json.IndexOf(searchPattern);

                    if (tierStart < 0) return null;

                    // Find the start of the tier object
                    int objectStart = json.IndexOf('{', tierStart);
                    if (objectStart < 0) return null;

                    // Find the matching closing brace
                    int braceCount = 1;
                    int pos = objectStart + 1;
                    int objectEnd = -1;

                    while (pos < json.Length && braceCount > 0)
                    {
                        if (json[pos] == '{') braceCount++;
                        else if (json[pos] == '}') braceCount--;

                        if (braceCount == 0)
                        {
                            objectEnd = pos;
                            break;
                        }
                        pos++;
                    }

                    if (objectEnd < 0) return null;

                    // Extract the tier JSON
                    string tierJson = json.Substring(objectStart, objectEnd - objectStart + 1);

                    // Parse it
                    return JsonUtility.FromJson<TierContent>(tierJson);
                }
                catch
                {
                    return null;
                }
            }
        }
    }
}