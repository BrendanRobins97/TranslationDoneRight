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

        // Enhanced context fields
        [TextArea(3, 10)]
        public string manualContext;     // Additional manual context from the user

        // Specialized context fields
        public string textCategory;    // Category of text (UI, Dialog, Tutorial, Item, etc.)
        public string speakerInfo;           // For dialog: Information about who is speaking
        public string locationContext;       // Where this text appears in the game
        public string mechanicContext;       // Related game mechanics
        public string targetAudience;        // Intended audience/tone
        public string culturalNotes;         // Cultural or lore-specific notes
        public string visualContext;         // Description of visual elements
        
        /// <summary>
        /// Generates optimized context for DeepL translation following best practices:
        /// 1. Most important context first (category, location, audience)
        /// 2. Clear and concise descriptions
        /// 3. Specific details that affect meaning or tone
        /// 4. Technical details last (if relevant)
        /// </summary>
        public string Context
        {
            get
            {
                var contextParts = new List<string>();

                // Start with text category and basic context
                contextParts.Add($"Type: {textCategory} text");
                if (!string.IsNullOrEmpty(locationContext))
                    contextParts.Add($"Location: {locationContext}");
                if (!string.IsNullOrEmpty(targetAudience))
                    contextParts.Add($"Audience: {targetAudience}");

                if (!string.IsNullOrEmpty(speakerInfo))
                    contextParts.Add($"Speaker: {speakerInfo}");

                if (!string.IsNullOrEmpty(visualContext))
                        contextParts.Add($"Visual: {visualContext}");
                    if (!string.IsNullOrEmpty(mechanicContext))
                        contextParts.Add($"Action: {mechanicContext}");

                if (!string.IsNullOrEmpty(culturalNotes))
                    contextParts.Add($"Lore: {culturalNotes}");

                if (!string.IsNullOrEmpty(mechanicContext))
                    contextParts.Add($"Action: {mechanicContext}");

                if (!string.IsNullOrEmpty(manualContext))
                    contextParts.Add($"Additional info: {manualContext}");

                // Add manual context if provided
                if (!string.IsNullOrEmpty(manualContext))
                    contextParts.Add($"Additional info: {manualContext}");

                // Add technical context last
                var technicalContext = new List<string>();
                if (!string.IsNullOrEmpty(componentName))
                    technicalContext.Add($"Component: {componentName}");
                if (!string.IsNullOrEmpty(fieldName))
                    technicalContext.Add($"Field: {fieldName}");

                if (technicalContext.Count > 0)
                    contextParts.Add($"Technical details: {string.Join(", ", technicalContext)}");

                // Join all parts with periods for clear separation
                return string.Join(". ", contextParts.Where(p => !string.IsNullOrEmpty(p)));
            }
        }
    }

    public class TranslationMetadata : SerializedScriptableObject
    {
        [SerializeField]
        private Dictionary<string, List<TextSourceInfo>> textSources = new Dictionary<string, List<TextSourceInfo>>();

        // Add list of text categories
        [SerializeField]
        private List<string> textCategories = new List<string>
        {
            "UI",
            "Dialog",
            "Tutorial",
            "Item",
            "System",
            "Lore",
            "Achievement",
            "Menu",
            "Error",
            "Notification"
        };

        public IReadOnlyList<string> TextCategories => textCategories;

        public void AddTextCategory(string category)
        {
            if (!textCategories.Contains(category))
            {
                textCategories.Add(category);
            }
        }

        public void RemoveTextCategory(string category)
        {
            textCategories.Remove(category);
        }

        public void UpdateTextCategory(string oldCategory, string newCategory)
        {
            int index = textCategories.IndexOf(oldCategory);
            if (index >= 0)
            {
                textCategories[index] = newCategory;

                // Update all existing sources using this category
                foreach (var sources in textSources.Values)
                {
                    foreach (var source in sources)
                    {
                        if (source.textCategory == oldCategory)
                        {
                            source.textCategory = newCategory;
                        }
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
        }

        // Get combined context for translation
        public string GetTranslationContext(string text)
        {
            if (!textSources.TryGetValue(text, out var sources) || sources.Count == 0)
                return string.Empty;

            // Combine contexts from all sources, removing duplicates
            var contexts = new HashSet<string>(
                sources.Select(s => s.Context)
                       .Where(c => !string.IsNullOrEmpty(c))
            );

            return string.Join("\n\n", contexts);
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
    }
} 