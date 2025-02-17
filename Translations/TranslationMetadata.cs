using UnityEngine;
using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using System.Linq;

namespace PSS
{
    public enum TextSourceType
    {
        Scene,
        Prefab,
        Script,
        ScriptableObject
    }

    [Serializable]
    public class TextSourceInfo
    {
        public TextSourceType sourceType;
        public string sourcePath;        // Path to the source asset
        public string objectPath;        // Full path to the object in hierarchy (for scenes/prefabs)
        public string componentName;     // Name of the component containing the text
        public string fieldName;         // Name of the field containing the text
        public bool wasInactive;         // Whether the GameObject was inactive when extracted
    }

    [Serializable]
    public class SimilarityGroupMetadata
    {
        public string reason;
        public float similarityScore;
        public string sourceInfo;
        public DateTime createdTime;
        public DateTime lastModifiedTime;
    }

    public class TranslationMetadata : SerializedScriptableObject
    {
        [SerializeField]
        private Dictionary<string, List<TextSourceInfo>> textSources = new Dictionary<string, List<TextSourceInfo>>();

        // Store metadata for similarity groups
        [SerializeField]
        private Dictionary<string, SimilarityGroupMetadata> similarityGroupMetadata = new Dictionary<string, SimilarityGroupMetadata>();

        // Add list of text categories
        [SerializeField]
        private Dictionary<string, List<string>> textCategories = new Dictionary<string, List<string>>() {
            {
                "UI",
                new List<string>
                {
                    "Button",
                }
            }
        };
        public Dictionary<string, List<string>> TextCategories => textCategories;

        // New centralized context storage
        [SerializeField]
        private Dictionary<string, Dictionary<string, string>> textContexts = new Dictionary<string, Dictionary<string, string>>();

        public void UpdateTextCategory(string key, string oldCategory, string newCategory)
        {
            if (textCategories.ContainsKey(key))
            {
                textCategories[key].Add(newCategory);
                // Update all existing contexts using this category
                foreach (var textKey in textContexts.Keys)
                {
                    var context = textContexts[textKey];
                    if (context.ContainsKey(key) && context[key] == oldCategory)
                    {
                        context[key] = newCategory;
                    }
                }
            }
        }

        public void AddSource(string text, TextSourceInfo sourceInfo)
        {
            if (!textSources.ContainsKey(text))
            {
                textSources[text] = new List<TextSourceInfo>();
            }
            textSources[text].Add(sourceInfo);

            // Initialize context if it doesn't exist
            if (!textContexts.ContainsKey(text))
            {
                textContexts[text] = new Dictionary<string, string>();
                foreach (var category in textCategories.Keys)
                {
                    textContexts[text][category] = "";
                }
            }
        }

        // Get combined context for translation
        public string GetTranslationContext(string text)
        {
            if (!textContexts.TryGetValue(text, out var context))
                return string.Empty;

            var contextParts = new List<string>();
            foreach (var key in context.Keys)
            {
                if (!string.IsNullOrEmpty(context[key]))
                    contextParts.Add($"{key}: {context[key]}");
            }

            return string.Join(". ", contextParts);
        }

        public Dictionary<string, string> GetContext(string text)
        {
            if (!textContexts.ContainsKey(text))
            {
                textContexts[text] = new Dictionary<string, string>();
                foreach (var category in textCategories.Keys)
                {
                    textContexts[text][category] = "";
                }
            }
            return textContexts[text];
        }

        public void SetContext(string text, Dictionary<string, string> context)
        {
            textContexts[text] = context;
        }

        public void UpdateContext(string text, string key, string value)
        {
            if (!textContexts.ContainsKey(text))
            {
                textContexts[text] = new Dictionary<string, string>();
            }
            textContexts[text][key] = value;
        }

        public void ClearSources(string text)
        {
            if (textSources.ContainsKey(text))
            {
                textSources[text].Clear();
            }
        }

        public void ClearAllSources()
        {
            textSources.Clear();
        }

        public List<TextSourceInfo> GetSources(string text)
        {
            return textSources.TryGetValue(text, out var sources) ? sources : new List<TextSourceInfo>();
        }

        public bool HasSources(string text)
        {
            return textSources.ContainsKey(text) && textSources[text].Count > 0;
        }

        public Dictionary<string, List<TextSourceInfo>> GetAllSources()
        {
            return textSources;
        }

        public void SetGroupMetadata(IEnumerable<string> groupTexts, string reason, float similarityScore, string sourceInfo = null)
        {
            string groupKey = string.Join("|", groupTexts.OrderBy(t => t));
            
            if (!similarityGroupMetadata.TryGetValue(groupKey, out var metadata))
            {
                metadata = new SimilarityGroupMetadata
                {
                    createdTime = DateTime.Now
                };
                similarityGroupMetadata[groupKey] = metadata;
            }

            metadata.reason = reason;
            metadata.similarityScore = similarityScore;
            metadata.sourceInfo = sourceInfo;
            metadata.lastModifiedTime = DateTime.Now;
        }

        public SimilarityGroupMetadata GetGroupMetadata(IEnumerable<string> groupTexts)
        {
            string groupKey = string.Join("|", groupTexts.OrderBy(t => t));
            return similarityGroupMetadata.TryGetValue(groupKey, out var metadata) ? metadata : null;
        }

        public void ClearGroupMetadata(IEnumerable<string> groupTexts)
        {
            string groupKey = string.Join("|", groupTexts.OrderBy(t => t));
            similarityGroupMetadata.Remove(groupKey);
        }
    }
} 