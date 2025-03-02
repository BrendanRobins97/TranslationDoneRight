using UnityEngine;
using System;
using System.Collections.Generic;
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
    public class SimilarityGroupMetadata
    {
        public string reason;
        public float similarityScore;
        public string sourceInfo;
        public DateTime createdTime;
        public DateTime lastModifiedTime;
    }

    [System.Serializable]
    public class TranslationMetadata : ScriptableObject
    {
        [SerializeField]
        private SerializableDictionary<string, TextSourceInfoList> textSources = new SerializableDictionary<string, TextSourceInfoList>();

        // Store metadata for similarity groups
        [SerializeField]
        private SerializableDictionary<string, SimilarityGroupMetadata> similarityGroupMetadata = new SerializableDictionary<string, SimilarityGroupMetadata>();

        // Add list of text categories
        [SerializeField]
        private SerializableDictionary<string, StringList> textCategories = new SerializableDictionary<string, StringList>() {
            {
                "UI",
                new StringList
                {
                    Items = new List<string> { "Button" }
                }
            }
        };

        // Add category templates
        [SerializeField]
        private SerializableDictionary<string, CategoryTemplate> categoryTemplates = new SerializableDictionary<string, CategoryTemplate>() {
            {
                "UI", new CategoryTemplate {
                    format = "This text appears in {value}"
                }
            },
            {
                "Manual", new CategoryTemplate {
                    format = "{value}" // Manual entries are free-form
                }
            }
        };

        public SerializableDictionary<string, StringList> TextCategories => textCategories;
        public SerializableDictionary<string, CategoryTemplate> CategoryTemplates => categoryTemplates;

        // New centralized context storage
        [SerializeField]
        private SerializableDictionary<string, SerializableDictionary<string, string>> textContexts = new SerializableDictionary<string, SerializableDictionary<string, string>>();

        [SerializeField]
        private SerializableDictionary<string, string> customLanguageMappings = new SerializableDictionary<string, string>();
        public SerializableDictionary<string, string> CustomLanguageMappings => customLanguageMappings;
        public List<ExtractionSource> extractionSources = new List<ExtractionSource>();
        public void UpdateTextCategory(string key, string oldCategory, string newCategory)
        {
            if (textCategories.ContainsKey(key))
            {
                textCategories[key].Items.Add(newCategory);
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
                textSources[text] = new TextSourceInfoList();
            }
            textSources[text].Items.Add(sourceInfo);

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
    }
} 