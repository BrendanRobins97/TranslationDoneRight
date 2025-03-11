using System;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using TMPro;
using System.Linq;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
#endif
using UnityEngine.AddressableAssets;

namespace Translations
{
    [CreateAssetMenu(fileName = "TranslationData", menuName = "Localization/TranslationData")]
    public class TranslationData : ScriptableObject
    {
        // Default language setting
        [SerializeField]
        public string defaultLanguage = "English";

        // Add list of supported languages
        public List<string> supportedLanguages = new List<string>();

        public AssetReference[] languageDataDictionary = new AssetReference[0];
        public List<string> allKeys = new List<string>();

        [SerializeField]
        public SerializableDictionary<TMP_FontAsset, SerializableDictionary<string, TMP_FontAsset>> fonts = 
            new SerializableDictionary<TMP_FontAsset, SerializableDictionary<string, TMP_FontAsset>>();

        public SerializableDictionary<string, string> keyContexts = new SerializableDictionary<string, string>();

        // Cache for quick text lookup
        [SerializeField]
        private SerializableDictionary<string, string> textToGroupKeyCache = new SerializableDictionary<string, string>();
        [SerializeField]
        private SerializableDictionary<string, string> canonicalTextCache = new SerializableDictionary<string, string>();

        // Store similarity group selections and acceptance status
        [SerializeField]
        private SerializableDictionary<string, string> similarityGroupSelections = new SerializableDictionary<string, string>();


        private void RebuildCaches()
        {
            textToGroupKeyCache.Clear();
            canonicalTextCache.Clear();

            foreach (var kvp in similarityGroupSelections)
            {
                string groupKey = kvp.Key;
                string selectedText = kvp.Value;
                
                // Split the group key back into individual texts
                var texts = groupKey.Split('|');
                
                // Add each text to the cache
                foreach (var text in texts)
                {
                    textToGroupKeyCache[text] = groupKey;
                    canonicalTextCache[text] = selectedText;
                }
            }
        }

        /// <summary>
        /// Gets the canonical (selected) text for a given input text if it's part of a similarity group.
        /// </summary>
        /// <param name="text">The input text to check</param>
        /// <returns>The canonical text if the input is part of a similarity group, or the input text if not</returns>
        public string GetCanonicalText(string text)
        {
            // Rebuild caches if they're empty (first use or after domain reload)
            if (textToGroupKeyCache.Count == 0 && similarityGroupSelections.Count > 0)
            {
                RebuildCaches();
            }

            // Check if this text is in our cache
            if (canonicalTextCache.TryGetValue(text, out string canonicalText))
            {
                return canonicalText;
            }

            return text; // Return original text if not in any group
        }

        /// <summary>
        /// Checks if a text is part of a similarity group and has a different canonical version.
        /// </summary>
        /// <param name="text">The text to check</param>
        /// <returns>True if the text is part of a group and has a different canonical version</returns>
        public bool HasDifferentCanonicalVersion(string text)
        {
            string canonicalText = GetCanonicalText(text);
            return canonicalText != text;
        }

        public void AddKey(string key, string context = null)
        {
            if (!allKeys.Contains(key))
            {
                allKeys.Add(key);
                if (!string.IsNullOrEmpty(context))
                {
                    keyContexts[key] = context;
                }
            }
        }

        public (string selectedText, bool isAccepted) GetGroupStatus(IEnumerable<string> groupTexts)
        {
            string groupKey = string.Join("|", groupTexts.OrderBy(t => t));
            string selectedText = similarityGroupSelections.TryGetValue(groupKey, out var text) ? text : null;
            return (selectedText, selectedText == null);
        }

        public void SetGroupStatus(IEnumerable<string> groupTexts, string selectedText)
        {
            string groupKey = string.Join("|", groupTexts.OrderBy(t => t));
            
            if (selectedText != null && groupTexts.Contains(selectedText))
            {
                similarityGroupSelections[groupKey] = selectedText;
            }
            else
            {
                similarityGroupSelections.Remove(groupKey);
            }

            // Clear caches to force rebuild
            textToGroupKeyCache.Clear();
            canonicalTextCache.Clear();
        }

        public void ClearGroupStatus(IEnumerable<string> groupTexts)
        {
            string groupKey = string.Join("|", groupTexts.OrderBy(t => t));
            similarityGroupSelections.Remove(groupKey);

            // Clear caches to force rebuild
            textToGroupKeyCache.Clear();
            canonicalTextCache.Clear();
        }

        public List<List<string>> GetAllGroupedTexts()
        {
            if (Application.isPlaying)
            {
                if (textToGroupKeyCache.Count == 0 && similarityGroupSelections.Count > 0)
                {
                    RebuildCaches();
                }
            } else {
                RebuildCaches();
            }
            

            // Group texts by their group keys
            var groups = new Dictionary<string, List<string>>();
            foreach (var kvp in textToGroupKeyCache)
            {
                string text = kvp.Key;
                string groupKey = kvp.Value;

                if (!groups.ContainsKey(groupKey))
                {
                    groups[groupKey] = new List<string>();
                }
                groups[groupKey].Add(text);
            }

            return groups.Values.ToList();
        }


#if UNITY_EDITOR
        public void SetupLanguageDataAssets()
        {
            // Get or create Addressables settings
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            
            // Create a group for language data if it doesn't exist
            var languageGroup = settings.FindGroup("LanguageData");
            if (languageGroup == null)
            {
                languageGroup = settings.CreateGroup("LanguageData", false, false, true, null);
            }

            // Resize array to match number of non-default languages
            int nonDefaultCount = supportedLanguages.Count - 1; // Exclude default language
            if (languageDataDictionary.Length != nonDefaultCount)
            {
                Array.Resize(ref languageDataDictionary, nonDefaultCount);
            }

            // Create language data assets for each supported language (except default language)
            for (int i = 1; i < supportedLanguages.Count; i++) // Start from 1 to skip default language
            {
                string language = supportedLanguages[i];
                string sanitizedName = SanitizeFileName(language);
                string assetPath = $"Assets/Resources/LanguageData_{sanitizedName}.asset";

                // Check if asset already exists
                LanguageData languageData = AssetDatabase.LoadAssetAtPath<LanguageData>(assetPath);
                
                if (languageData == null)
                {
                    // Create new language data asset
                    languageData = CreateInstance<LanguageData>();
                    AssetDatabase.CreateAsset(languageData, assetPath);
                }

                // Make the asset addressable
                var guid = AssetDatabase.AssetPathToGUID(assetPath);
                var entry = settings.CreateOrMoveEntry(guid, languageGroup);
                entry.address = $"LanguageData_{sanitizedName}";

                // Update the asset reference in the dictionary
                languageDataDictionary[i - 1] = new AssetReference(guid);
            }

            AssetDatabase.SaveAssets();
            EditorUtility.SetDirty(this);
            AssetDatabase.Refresh();

            Debug.Log("Language data assets setup complete!");
        }

        private string SanitizeFileName(string fileName)
        {
            // Replace spaces with underscores and remove invalid characters
            fileName = fileName.Replace(' ', '_');
            string invalid = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars()) + "()";
            foreach (char c in invalid)
            {
                fileName = fileName.Replace(c.ToString(), string.Empty);
            }
            return fileName;
        }

        public void ImportCSV()
        {
            string csvPath = EditorUtility.OpenFilePanel("Select CSV File", "", "csv");
            if (!string.IsNullOrEmpty(csvPath))
            {
                ReadCSV(csvPath);
            }
        }

        public void ClearData()
        {
            allKeys.Clear();
            foreach (var data in languageDataDictionary)
            {
                LanguageData asset = AssetDatabase.LoadAssetAtPath<LanguageData>(AssetDatabase.GUIDToAssetPath(data.AssetGUID));
                asset.allText.Clear();
                EditorUtility.SetDirty(asset);
            }
        }

        private void ReadCSV(string filePath)
        {
            List<string[]> rows = ParseCSV(filePath);

            if (rows.Count < 2)
            {
                Debug.LogError("CSV file is empty or not properly formatted.");
                return;
            }

            string[] headers = rows[0];
            
            // Find the default language column index
            int defaultLanguageIndex = Array.IndexOf(headers, defaultLanguage);
            if (defaultLanguageIndex == -1)
            {
                Debug.LogError("CSV must contain a '" + defaultLanguage + "' column.");
                return;
            }

            // Create a mapping of language names to their column indices
            Dictionary<string, int> languageColumns = new Dictionary<string, int>();
            Dictionary<string, LanguageData> languageDataAssets = new Dictionary<string, LanguageData>();
            
            // Initialize language data assets
            for (int i = 0; i < languageDataDictionary.Length; i++)
            {
                string language = supportedLanguages[i + 1]; // +1 to skip default language
                string assetPath = AssetDatabase.GUIDToAssetPath(languageDataDictionary[i].AssetGUID);
                LanguageData asset = AssetDatabase.LoadAssetAtPath<LanguageData>(assetPath);
                
                if (asset != null)
                {
                    languageDataAssets[language] = asset;
                    asset.allText.Clear(); // Clear existing translations
                }
                else
                {
                    Debug.LogError($"Could not load language data asset for {language}");
                    return;
                }
            }

            // Find column indices for each supported language
            for (int i = 0; i < headers.Length; i++)
            {
                if (supportedLanguages.Contains(headers[i]) && headers[i] != defaultLanguage)
                {
                    languageColumns[headers[i]] = i;
                }
            }

            // Clear existing keys
            allKeys.Clear();

            // Process each row
            for (int rowIndex = 1; rowIndex < rows.Count; rowIndex++)
            {
                string[] fields = rows[rowIndex];
                
                if (fields.Length <= defaultLanguageIndex)
                {
                    Debug.LogWarning($"Skipping row {rowIndex + 1}: insufficient columns");
                    continue;
                }

                string defaultLanguageText = fields[defaultLanguageIndex];
                if (string.IsNullOrWhiteSpace(defaultLanguageText))
                {
                    continue;
                }

                // Add the default language key
                allKeys.Add(defaultLanguageText);

                // Add translations for each language
                foreach (var language in supportedLanguages.Skip(1)) // Skip default language
                {
                    if (languageColumns.TryGetValue(language, out int columnIndex) && 
                        languageDataAssets.TryGetValue(language, out LanguageData asset))
                    {
                        string translation = columnIndex < fields.Length ? fields[columnIndex] : "";
                        asset.allText.Add(translation);
                    }
                    else
                    {
                        // If language column not found in CSV, add empty translation
                        if (languageDataAssets.TryGetValue(language, out LanguageData asset2))
                        {
                            asset2.allText.Add("");
                        }
                    }
                }
            }

            // Save all changes
            foreach (var asset in languageDataAssets.Values)
            {
                EditorUtility.SetDirty(asset);
            }
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();

            Debug.Log("CSV import complete. Translations updated.");
        }

        private List<string[]> ParseCSV(string filePath)
        {
            List<string[]> rows = new List<string[]>();
            using (var reader = new StreamReader(filePath))
            {
                bool inQuotes = false;
                string line;
                string currentField = "";
                List<string> currentRow = new List<string>();

                while ((line = reader.ReadLine()) != null)
                {
                    for (int i = 0; i < line.Length; i++)
                    {
                        char c = line[i];
                        if (c == '\"')
                        {
                            inQuotes = !inQuotes;
                        }
                        else if (c == ',' && !inQuotes)
                        {
                            currentRow.Add(currentField);
                            currentField = "";
                        }
                        else
                        {
                            currentField += c;
                        }
                    }
                    if (inQuotes)
                    {
                        currentField += "\n";
                    }
                    else
                    {
                        currentRow.Add(currentField);
                        rows.Add(currentRow.ToArray());
                        currentRow.Clear();
                        currentField = "";
                    }
                }
                // Add last row if file doesn't end with a newline
                if (currentRow.Count > 0)
                {
                    currentRow.Add(currentField);
                    rows.Add(currentRow.ToArray());
                }
            }
            return rows;
        }

        public void AddFont(TMP_FontAsset font)
        {
            fonts ??= new SerializableDictionary<TMP_FontAsset, SerializableDictionary<string, TMP_FontAsset>>();

            if (!fonts.ContainsKey(font))
            {
                fonts[font] = new SerializableDictionary<string, TMP_FontAsset>();
                foreach (var language in supportedLanguages)
                {
                    if (language != defaultLanguage)
                    {
                        fonts[font][language] = null;
                    }
                }
            }
        }
#endif

        // Font management methods
        public void AddLanguageToFonts(string language)
        {
            if (language == defaultLanguage) return; // Default language doesn't need font mappings
            
            // Add the new language to all existing font mappings
            foreach (var fontPair in fonts)
            {
                SerializableDictionary<string, TMP_FontAsset> languageFonts = fontPair.Value;
                if (!languageFonts.ContainsKey(language))
                {
                    languageFonts[language] = null;
                }
            }
        }
        
        public void RemoveLanguageFromFonts(string language)
        {
            if (language == defaultLanguage) return; // Default language doesn't have font mappings
            
            // Remove the language from all font mappings
            foreach (var fontPair in fonts)
            {
                SerializableDictionary<string, TMP_FontAsset> languageFonts = fontPair.Value;
                languageFonts.Remove(language);
            }
        }
        
        public void UpdateDefaultLanguage(string oldDefault, string newDefault)
        {
            // If we're changing default language, we need to update the font mappings
            if (oldDefault != newDefault)
            {
                // Add mappings for old default language (which was previously skipped)
                AddLanguageToFonts(oldDefault);
                
                // Remove mappings for new default language (which will now use default font)
                RemoveLanguageFromFonts(newDefault);
            }
        }
    }
}
