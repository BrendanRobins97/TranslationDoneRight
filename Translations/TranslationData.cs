using System;
using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;
using System.IO;
using Sirenix.Serialization;
using TMPro;
using System.Linq;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
#endif
using UnityEngine.AddressableAssets;

namespace PSS
{
    [CreateAssetMenu(fileName = "TranslationData", menuName = "Localization/TranslationData")]
    public class TranslationData : SerializedScriptableObject
    {
        // Add list of supported languages
        public List<string> supportedLanguages = new List<string>
        {
            "English",
            "French", 
            "Italian", 
            "German", 
            "Danish", 
            "Dutch", 
            "Japanese",
            "Korean", 
            "Portuguese", 
            "Portuguese (Brazil)", 
            "Russian", 
            "Chinese (Simplified)",
            "Spanish", 
            "Swedish", 
            "Chinese (Traditional)", 
            "Ukrainian"
        };

        public AssetReference[] languageDataDictionary = new AssetReference[0];
        public List<string> allKeys = new List<string>();

        [OdinSerialize]
        public Dictionary<TMP_FontAsset, Dictionary<string, TMP_FontAsset>> fonts = 
            new Dictionary<TMP_FontAsset, Dictionary<string, TMP_FontAsset>>();

        // New fields for parameter metadata
        [OdinSerialize]
        public Dictionary<string, List<string>> keyParameters = new Dictionary<string, List<string>>();

        // New field for translation context
        [OdinSerialize]
        public Dictionary<string, string> keyContexts = new Dictionary<string, string>();

        // Reference to the metadata asset
        [SerializeField]
        private TranslationMetadata metadata;

        public TranslationMetadata Metadata
        {
            get
            {
                if (metadata == null)
                {
                    metadata = Resources.Load<TranslationMetadata>("TranslationMetadata");
#if UNITY_EDITOR
                    if (metadata == null)
                    {
                        metadata = ScriptableObject.CreateInstance<TranslationMetadata>();
                        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                        {
                            AssetDatabase.CreateFolder("Assets", "Resources");
                        }
                        AssetDatabase.CreateAsset(metadata, "Assets/Resources/TranslationMetadata.asset");
                        AssetDatabase.SaveAssets();
                    }
#endif
                }
                return metadata;
            }
        }

        public void AddKey(string key, List<string> parameters = null, string context = null)
        {
            if (!allKeys.Contains(key))
            {
                allKeys.Add(key);
                if (parameters != null && parameters.Count > 0)
                {
                    keyParameters[key] = new List<string>(parameters);
                }
                if (!string.IsNullOrEmpty(context))
                {
                    keyContexts[key] = context;
                }
            }
        }

        public List<string> GetKeyParameters(string key)
        {
            return keyParameters.TryGetValue(key, out var parameters) ? parameters : new List<string>();
        }

        public bool ValidateParameters(string key, IEnumerable<string> providedParameters)
        {
            if (!keyParameters.TryGetValue(key, out var requiredParameters))
                return true; // No parameters defined for this key

            var required = new HashSet<string>(requiredParameters);
            var provided = new HashSet<string>(providedParameters);

            return required.SetEquals(provided);
        }

        public string GetKeyContext(string key)
        {
            return keyContexts.TryGetValue(key, out var context) ? context : string.Empty;
        }

        public void SetKeyContext(string key, string context)
        {
            if (allKeys.Contains(key))
            {
                if (string.IsNullOrEmpty(context))
                {
                    keyContexts.Remove(key);
                }
                else
                {
                    keyContexts[key] = context;
                }
            }
        }

#if UNITY_EDITOR
        [Button("Setup Language Data Assets")]
        public void SetupLanguageDataAssets()
        {
            // Get or create Addressables settings
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            
            // Create a group for language data if it doesn't exist
            AddressableAssetGroup languageGroup = settings.FindGroup("LanguageData");
            if (languageGroup == null)
            {
                languageGroup = settings.CreateGroup("LanguageData", false, false, true, null);
            }

            // Resize array to match number of non-English languages
            int nonEnglishCount = supportedLanguages.Count - 1; // Exclude English
            if (languageDataDictionary.Length != nonEnglishCount)
            {
                Array.Resize(ref languageDataDictionary, nonEnglishCount);
            }

            // Create language data assets for each supported language (except English)
            for (int i = 1; i < supportedLanguages.Count; i++) // Start from 1 to skip English
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

        [Button("Import CSV")]
        public void ImportCSV()
        {
            string csvPath = EditorUtility.OpenFilePanel("Select CSV File", "", "csv");
            if (!string.IsNullOrEmpty(csvPath))
            {
                ReadCSV(csvPath);
            }
        }

        [Button("Clear Data")]
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
            
            // Find the English column index
            int englishIndex = Array.IndexOf(headers, "English");
            if (englishIndex == -1)
            {
                Debug.LogError("CSV must contain an 'English' column.");
                return;
            }

            // Create a mapping of language names to their column indices
            Dictionary<string, int> languageColumns = new Dictionary<string, int>();
            Dictionary<string, LanguageData> languageDataAssets = new Dictionary<string, LanguageData>();
            
            // Initialize language data assets
            for (int i = 0; i < languageDataDictionary.Length; i++)
            {
                string language = supportedLanguages[i + 1]; // +1 to skip English
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
                if (supportedLanguages.Contains(headers[i]) && headers[i] != "English")
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
                
                if (fields.Length <= englishIndex)
                {
                    Debug.LogWarning($"Skipping row {rowIndex + 1}: insufficient columns");
                    continue;
                }

                string englishText = fields[englishIndex];
                if (string.IsNullOrWhiteSpace(englishText))
                {
                    continue;
                }

                // Add the English key
                allKeys.Add(englishText);

                // Add translations for each language
                foreach (var language in supportedLanguages.Skip(1)) // Skip English
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

        [Button]
        public void AddFont(TMP_FontAsset font)
        {
            fonts ??= new Dictionary<TMP_FontAsset, Dictionary<string, TMP_FontAsset>>();

            if (!fonts.ContainsKey(font))
            {
                fonts[font] = new Dictionary<string, TMP_FontAsset>();
                foreach (var language in supportedLanguages)
                {
                    fonts[font][language] = null;
                }
            }
        }
#endif

    }
}
