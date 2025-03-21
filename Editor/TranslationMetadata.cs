using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Translations
{
    public enum TextSourceType
    {
        Scene,
        Prefab,
        Script,
        ScriptableObject,
        ExternalFile
    }

    [Serializable]
    public class TextSourceInfo
    {
        public TextSourceType sourceType;
        public string sourcePath;        // Path to the source asset
        // Simplified source info - removed objectPath, componentName, fieldName
    }

    [Serializable]
    public class CategoryTemplate
    {
        public string format = "This text appears in {value}"; // Template with {value} placeholder

        public string FormatContext(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            string formatted = format.Replace("{value}", value);
            return formatted + ".";
        }
    }

    [Serializable]
    public class TextSourceInfoList
    {
        public List<TextSourceInfo> Items = new List<TextSourceInfo>();
        private const int MAX_SOURCES = 4; // Limit number of sources to 4
        
        public void AddSource(TextSourceInfo source)
        {
            // Check if this exact source already exists to avoid duplicates
            bool sourceExists = Items.Any(existingSource => 
                existingSource.sourceType == source.sourceType && 
                existingSource.sourcePath == source.sourcePath);
            
            // Only add if it doesn't already exist and we haven't reached the cap
            if (!sourceExists && Items.Count < MAX_SOURCES)
            {
                Items.Add(source);
            }
        }
        
        // Implicit conversion operators for easier usage
        public static implicit operator List<TextSourceInfo>(TextSourceInfoList wrapper) => wrapper.Items;
        public static implicit operator TextSourceInfoList(List<TextSourceInfo> list) => new TextSourceInfoList { Items = list };
    }

    [Serializable]
    public class StringList
    {
        public List<string> Items = new List<string>();
        
        // Implicit conversion operators for easier usage
        public static implicit operator List<string>(StringList wrapper) => wrapper.Items;
        public static implicit operator StringList(List<string> list) => new StringList { Items = list };
    }

    [Serializable]
    public class ExtractionSourcesList
    {
        public List<ExtractionSource> Items = new List<ExtractionSource>();

        // Implicit conversion operators for easier usage
        public static implicit operator List<ExtractionSource>(ExtractionSourcesList wrapper) => wrapper.Items;
        public static implicit operator ExtractionSourcesList(List<ExtractionSource> list) => new ExtractionSourcesList { Items = list };
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

    public enum TextState
    {
        None,
        New,
        Recent,
        Missing,
    }

    [System.Serializable]
    public class TranslationMetadata : ScriptableObject
    {
        [SerializeField]
        private SerializableDictionary<string, TextSourceInfoList> textSources = new SerializableDictionary<string, TextSourceInfoList>();

        // Store metadata for similarity groups
        [SerializeField]
        private SerializableDictionary<string, SimilarityGroupMetadata> similarityGroupMetadata = new SerializableDictionary<string, SimilarityGroupMetadata>();

        // Track text states (new/missing/both)
        [SerializeField]
        private SerializableDictionary<string, TextState> textStates = new SerializableDictionary<string, TextState>();

        // Add list of text categories
        [SerializeField]
        private SerializableDictionary<string, StringList> textCategories = new SerializableDictionary<string, StringList>();

        // Add category templates
        [SerializeField]
        private SerializableDictionary<string, CategoryTemplate> categoryTemplates = new SerializableDictionary<string, CategoryTemplate>();

        public SerializableDictionary<string, StringList> TextCategories => textCategories;
        public SerializableDictionary<string, CategoryTemplate> CategoryTemplates => categoryTemplates;

        // New centralized context storage
        [SerializeField]
        private SerializableDictionary<string, SerializableDictionary<string, string>> textContexts = new SerializableDictionary<string, SerializableDictionary<string, string>>();

        [SerializeField]
        private SerializableDictionary<string, string> customLanguageMappings = new SerializableDictionary<string, string>();
        public SerializableDictionary<string, string> CustomLanguageMappings => customLanguageMappings;
        public List<ExtractionSource> extractionSources = new List<ExtractionSource>();

        [SerializeField]
        public SerializableDictionary<string, ExtractionSourcesList> extractorSources = new SerializableDictionary<string, ExtractionSourcesList>();
        
        // List of manually specified scene paths for the Scene extractor when using Manual mode
        [SerializeField]
        public List<string> manualScenePaths = new List<string>();
        
        public void UpdateTextCategory(string key, string oldCategory, string newCategory)
        {
            if (textCategories.ContainsKey(key))
            {
                if (!textCategories[key].Items.Contains(newCategory))
                {
                    textCategories[key].Items.Add(newCategory);
                }
                if (!string.IsNullOrEmpty(oldCategory))
                {
                    // Update all existing contexts using this category
                    foreach (var textKey in textContexts.Keys.ToList())
                    {
                        foreach (var context in textContexts[textKey].ToList())
                        {
                            if (context.Value == oldCategory)
                            {
                                textContexts[textKey][key] = newCategory;
                            }
                        }
                    }
                }
                textCategories[key].Items.Remove(oldCategory);
            }
        }

        public void AddSource(string text, TextSourceInfo sourceInfo)
        {
            if (!textSources.ContainsKey(text))
            {
                textSources[text] = new TextSourceInfoList();
            }
            
            // Use the new AddSource method in TextSourceInfoList to handle duplication and capping
            textSources[text].AddSource(sourceInfo);

            // Initialize context if it doesn't exist
            if (!textContexts.ContainsKey(text))
            {
                textContexts[text] = new SerializableDictionary<string, string>();
                foreach (var category in textCategories.Keys)
                {
                    textContexts[text][category] = "";
                }
            }
        }

        // Get combined context for translation with natural language
        public string GetTranslationContext(string text)
        {
            if (!textContexts.TryGetValue(text, out var context))
                return string.Empty;

            var contextParts = new List<string>();
            foreach (var kvp in context)
            {
                string categoryKey = kvp.Key;
                string value = kvp.Value;

                // Skip if category doesn't exist anymore or value is empty
                if (!categoryKey.Equals("Manual") && (!textCategories.ContainsKey(categoryKey) || string.IsNullOrEmpty(value)))
                    continue;

                if (categoryTemplates.TryGetValue(categoryKey, out var template))
                {
                    string formattedContext = template.FormatContext(value);
                    if (!string.IsNullOrEmpty(formattedContext))
                    {
                        contextParts.Add(formattedContext);
                    }
                }
                else
                {
                    // Fallback for categories without templates
                    contextParts.Add($"{categoryKey}: {value}");
                }
            }

            return string.Join(" ", contextParts);
        }

        public SerializableDictionary<string, string> GetContext(string text)
        {
            if (!textContexts.ContainsKey(text))
            {
                textContexts[text] = new SerializableDictionary<string, string>();
                foreach (var category in textCategories.Keys)
                {
                    textContexts[text][category] = "";
                }
            }
            return textContexts[text];
        }

        public void SetContext(string text, SerializableDictionary<string, string> context)
        {
            textContexts[text] = context;
        }

        public void UpdateContext(string text, string key, string value)
        {
            if (!textContexts.ContainsKey(text))
            {
                textContexts[text] = new SerializableDictionary<string, string>();
            }
            textContexts[text][key] = value;
        }

        public void ClearSources(string text)
        {
            if (textSources.ContainsKey(text))
            {
                textSources[text].Items.Clear();
            }
        }

        public void ClearAllSources()
        {
            textSources.Clear();
        }

        public List<TextSourceInfo> GetSources(string text)
        {
            return textSources.TryGetValue(text, out var sources) ? sources.Items : new List<TextSourceInfo>();
        }

        public bool HasSources(string text)
        {
            return textSources.ContainsKey(text) && textSources[text].Items.Count > 0;
        }

        public Dictionary<string, List<TextSourceInfo>> GetAllSources()
        {
            return textSources.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Items);
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

        // Add a new category with template
        public void AddCategory(string categoryName, CategoryTemplate template)
        {
            if (!textCategories.ContainsKey(categoryName))
            {
                textCategories[categoryName] = new List<string>();
                categoryTemplates[categoryName] = template;

                // Initialize this category for all existing texts
                foreach (var textKey in textContexts.Keys)
                {
                    textContexts[textKey][categoryName] = "";
                }
            }
        }

        // Update or add a category template
        public void UpdateCategoryTemplate(string categoryName, CategoryTemplate template)
        {
            categoryTemplates[categoryName] = template;
        }

        public bool IsNewText(string text)
        {
            return textStates.TryGetValue(text, out var state) && (state == TextState.New);
        }

        public bool IsRecentText(string text)
        {
            return textStates.TryGetValue(text, out var state) && (state == TextState.Recent);
        }

        public bool IsMissingText(string text)
        {
            return textStates.TryGetValue(text, out var state) && (state == TextState.Missing);
        }

        public TextState GetTextState(string text)
        {
            return textStates.TryGetValue(text, out var state) ? state : TextState.None;
        }

        public void SetTextState(string text, TextState state)
        {
            textStates[text] = state;
        }

        public void ClearTextStates()
        {
            textStates.Clear();
        }
    }
} 