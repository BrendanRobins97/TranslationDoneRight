#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using System.IO;
using System.Collections.Generic;
using TMPro;
using UnityEditor.SceneManagement;
using System.Text.RegularExpressions;
using System.Reflection;
using UnityEngine.UI;
using System.Linq;
using System;

namespace PSS
{
    public enum KeyUpdateMode
    {
        Replace,  // Clear existing and add new keys
        Merge     // Keep existing and add new keys
    }

    public class TextExtractor
    {
        public static HashSet<string> ExtractAllText(
            bool fromScenes,
            bool fromPrefabs,
            bool fromScripts,
            bool fromScriptableObjects,
            bool includeInactive)
        {
            HashSet<string> extractedText = new HashSet<string>();

            if (fromScenes)
                ExtractTextFromScenes(extractedText, includeInactive);
            
            if (fromPrefabs)
                ExtractTaggedStringFieldsFromPrefabs(extractedText, includeInactive);
            
            if (fromScripts)
                ExtractTranslateFunctionTexts(extractedText);
            
            if (fromScriptableObjects)
                ExtractScriptableObjectFields(extractedText);

            return extractedText;
        }

        public static void ExtractToCSV(
            string filePath,
            TranslationData translationData,
            bool fromScenes,
            bool fromPrefabs,
            bool fromScripts,
            bool fromScriptableObjects,
            bool includeInactive)
        {
            var extractedText = ExtractAllText(fromScenes, fromPrefabs, fromScripts, fromScriptableObjects, includeInactive);
            WriteNewCSVWithExistingTranslations(filePath, extractedText, translationData);
        }

        public static void UpdateExistingCSV(
            string filePath,
            TranslationData translationData,
            bool fromScenes,
            bool fromPrefabs,
            bool fromScripts,
            bool fromScriptableObjects,
            bool includeInactive)
        {
            var extractedText = ExtractAllText(fromScenes, fromPrefabs, fromScripts, fromScriptableObjects, includeInactive);
            List<string[]> existingRows = new List<string[]>();
            Dictionary<string, string[]> existingTranslations = new Dictionary<string, string[]>();

            // Read existing CSV and store its translations
            if (File.Exists(filePath))
            {
                using (var reader = new StreamReader(filePath))
                {
                    string[] headers = reader.ReadLine()?.Split(',');
                    if (headers != null)
                    {
                        existingRows.Add(headers);
                        while (!reader.EndOfStream)
                        {
                            string[] row = reader.ReadLine()?.Split(',');
                            if (row != null && row.Length > 0)
                            {
                                existingTranslations[row[0]] = row;
                            }
                        }
                    }
                }
            }
            else
            {
                // If file doesn't exist, create headers
                existingRows.Add(translationData.supportedLanguages.ToArray());
            }

            // Create a dictionary of current translations from TranslationData
            Dictionary<string, Dictionary<string, string>> currentTranslations = new Dictionary<string, Dictionary<string, string>>();
            
            for (int i = 0; i < translationData.allKeys.Count; i++)
            {
                string key = translationData.allKeys[i];
                currentTranslations[key] = new Dictionary<string, string>();

                for (int j = 0; j < translationData.languageDataDictionary.Length; j++)
                {
                    string language = translationData.supportedLanguages[j + 1];
                    string assetPath = AssetDatabase.GUIDToAssetPath(translationData.languageDataDictionary[j].AssetGUID);
                    LanguageData languageData = AssetDatabase.LoadAssetAtPath<LanguageData>(assetPath);
                    
                    if (languageData != null && i < languageData.allText.Count)
                    {
                        currentTranslations[key][language] = languageData.allText[i];
                    }
                }
            }

            // Clear existing rows except header
            existingRows.RemoveRange(1, existingRows.Count - 1);

            // Add rows for each extracted text
            foreach (string text in extractedText)
            {
                string[] row = new string[existingRows[0].Length];
                row[0] = text;

                // First try to get translations from existing CSV
                if (existingTranslations.TryGetValue(text, out string[] existingRow))
                {
                    for (int i = 1; i < Math.Min(existingRow.Length, row.Length); i++)
                    {
                        row[i] = existingRow[i];
                    }
                }
                
                // Then fill in any missing translations from current TranslationData
                if (currentTranslations.TryGetValue(text, out var translations))
                {
                    for (int i = 1; i < translationData.supportedLanguages.Count; i++)
                    {
                        string language = translationData.supportedLanguages[i];
                        if (string.IsNullOrEmpty(row[i]) && translations.TryGetValue(language, out string translation))
                        {
                            row[i] = translation;
                        }
                    }
                }

                existingRows.Add(row);
            }

            // Write updated CSV
            WriteToCSV(filePath, existingRows);
        }

        public static void ExportCurrentKeys(string filePath, TranslationData translationData)
        {
            List<string[]> rows = new List<string[]>();
            
            // Add header
            rows.Add(translationData.supportedLanguages.ToArray());

            // Add translations
            for (int i = 0; i < translationData.allKeys.Count; i++)
            {
                string[] row = new string[translationData.supportedLanguages.Count];
                row[0] = translationData.allKeys[i]; // English key

                // Add translations for other languages
                for (int j = 0; j < translationData.languageDataDictionary.Length; j++)
                {
                    string assetPath = AssetDatabase.GUIDToAssetPath(translationData.languageDataDictionary[j].AssetGUID);
                    LanguageData languageData = AssetDatabase.LoadAssetAtPath<LanguageData>(assetPath);
                    if (languageData != null && i < languageData.allText.Count)
                    {
                        row[j + 1] = languageData.allText[i];
                    }
                }

                rows.Add(row);
            }

            WriteToCSV(filePath, rows);
        }

        public static void GenerateReport(
            string filePath,
            TranslationData translationData,
            bool fromScenes,
            bool fromPrefabs,
            bool fromScripts,
            bool fromScriptableObjects,
            bool includeInactive)
        {
            var extractedText = ExtractAllText(fromScenes, fromPrefabs, fromScripts, fromScriptableObjects, includeInactive);
            var unusedKeys = translationData.allKeys.Where(k => !extractedText.Contains(k)).ToList();
            var missingKeys = extractedText.Where(k => !translationData.allKeys.Contains(k)).ToList();

            using (StreamWriter writer = new StreamWriter(filePath))
            {
                writer.WriteLine("Translation System Report");
                writer.WriteLine("========================");
                writer.WriteLine($"Generated: {System.DateTime.Now}\n");

                writer.WriteLine("Statistics:");
                writer.WriteLine($"Total Keys: {translationData.allKeys.Count}");
                writer.WriteLine($"Total Languages: {translationData.supportedLanguages.Count}");
                writer.WriteLine($"Unused Keys: {unusedKeys.Count}");
                writer.WriteLine($"Missing Keys: {missingKeys.Count}\n");

                writer.WriteLine("Language Coverage:");
                foreach (var language in translationData.supportedLanguages.Skip(1))
                {
                    int index = translationData.supportedLanguages.IndexOf(language) - 1;
                    string assetPath = AssetDatabase.GUIDToAssetPath(translationData.languageDataDictionary[index].AssetGUID);
                    LanguageData languageData = AssetDatabase.LoadAssetAtPath<LanguageData>(assetPath);
                    
                    if (languageData != null)
                    {
                        int nonEmptyCount = languageData.allText.Count(t => !string.IsNullOrWhiteSpace(t));
                        float coverage = translationData.allKeys.Count > 0 
                            ? (nonEmptyCount * 100f) / translationData.allKeys.Count 
                            : 100f;
                        writer.WriteLine($"{language}: {coverage:F1}% ({nonEmptyCount}/{translationData.allKeys.Count})");
                    }
                }

                if (unusedKeys.Count > 0)
                {
                    writer.WriteLine("\nUnused Keys:");
                    foreach (var key in unusedKeys)
                    {
                        writer.WriteLine($"- {key}");
                    }
                }

                if (missingKeys.Count > 0)
                {
                    writer.WriteLine("\nMissing Keys:");
                    foreach (var key in missingKeys)
                    {
                        writer.WriteLine($"- {key}");
                    }
                }
            }
        }

        private static void WriteNewCSVWithExistingTranslations(string filePath, HashSet<string> extractedText, TranslationData translationData)
        {
            List<string[]> rows = new List<string[]>();
            
            // Add header with all supported languages
            rows.Add(translationData.supportedLanguages.ToArray());

            // Create a dictionary to store existing translations
            Dictionary<string, string[]> translations = new Dictionary<string, string[]>();

            // Load existing translations for all keys
            for (int keyIndex = 0; keyIndex < translationData.allKeys.Count; keyIndex++)
            {
                string key = translationData.allKeys[keyIndex];
                string[] translationRow = new string[translationData.supportedLanguages.Count];
                translationRow[0] = key; // English (key)

                // Get translations for each language
                for (int langIndex = 0; langIndex < translationData.languageDataDictionary.Length; langIndex++)
                {
                    string assetPath = AssetDatabase.GUIDToAssetPath(translationData.languageDataDictionary[langIndex].AssetGUID);
                    LanguageData languageData = AssetDatabase.LoadAssetAtPath<LanguageData>(assetPath);
                    
                    if (languageData != null && keyIndex < languageData.allText.Count)
                    {
                        // langIndex + 1 because first column is English
                        translationRow[langIndex + 1] = languageData.allText[keyIndex];
                    }
                }

                translations[key] = translationRow;
            }

            // Add rows for each extracted text
            foreach (string text in extractedText)
            {
                if (translations.TryGetValue(text, out string[] existingTranslations))
                {
                    // Use existing translations if available
                    rows.Add(existingTranslations);
                }
                else
                {
                    // Create new row with just the English text
                    string[] newRow = new string[translationData.supportedLanguages.Count];
                    newRow[0] = text;
                    rows.Add(newRow);
                }
            }

            WriteToCSV(filePath, rows);
        }

        public static void ExtractTextFromScenes(HashSet<string> extractedText, bool includeInactive)
        {
            for (int i = 0; i < EditorBuildSettings.scenes.Length; i++)
            {
                string scenePath = EditorBuildSettings.scenes[i].path;
                Scene scene = SceneManager.GetSceneByPath(scenePath);

                if (!scene.isLoaded)
                {
                    scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                }

                // Extract TextMeshPro texts
                TextMeshProUGUI[] textMeshProObjects = GameObject.FindObjectsOfType<TextMeshProUGUI>(includeInactive);
                foreach (TextMeshProUGUI textObject in textMeshProObjects)
                {
                    if (!string.IsNullOrWhiteSpace(textObject.text))
                    {
                        extractedText.Add(textObject.text);
                    }
                }

                // Extract UI Text texts
                Text[] uiTextObjects = GameObject.FindObjectsOfType<Text>(includeInactive);
                foreach (Text uiText in uiTextObjects)
                {
                    if (!string.IsNullOrWhiteSpace(uiText.text))
                    {
                        extractedText.Add(uiText.text);
                    }
                }

                // Extract fields marked with TranslatedAttribute
                foreach (GameObject rootObj in scene.GetRootGameObjects())
                {
                    ExtractFromGameObject(rootObj, extractedText, includeInactive);
                }
            }
        }

        private static void ExtractFromGameObject(GameObject obj, HashSet<string> extractedText, bool includeInactive)
        {
            if (!includeInactive && !obj.activeInHierarchy) return;

            Component[] components = obj.GetComponents<Component>();
            foreach (Component component in components)
            {
                if (component == null) continue;
                ExtractFieldsRecursive(component, extractedText);
            }

            foreach (Transform child in obj.transform)
            {
                ExtractFromGameObject(child.gameObject, extractedText, includeInactive);
            }
        }

        public static void ExtractTranslateFunctionTexts(HashSet<string> extractedText)
        {
            string[] scriptPaths = Directory.GetFiles("Assets", "*.cs", SearchOption.AllDirectories);
            Regex translateRegex = new Regex(@"Translations\.Translate\(\s*""([^""]+)""\s*\)");

            foreach (string scriptPath in scriptPaths)
            {
                string scriptContent = File.ReadAllText(scriptPath);
                MatchCollection matches = translateRegex.Matches(scriptContent);

                foreach (Match match in matches)
                {
                    if (match.Groups.Count > 1)
                    {
                        string matchedText = match.Groups[1].Value;
                        extractedText.Add(matchedText);
                    }
                }
            }
        }

        public static void ExtractScriptableObjectFields(HashSet<string> extractedText)
        {
            string[] guids = AssetDatabase.FindAssets("t:ScriptableObject");

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                ScriptableObject scriptableObject = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);

                if (scriptableObject != null)
                {
                    ExtractFieldsRecursive(scriptableObject, extractedText);
                }
            }
        }

        public static void ExtractTaggedStringFieldsFromPrefabs(HashSet<string> extractedText, bool includeInactive)
        {
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");

            foreach (string guid in prefabGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                if (prefab != null)
                {
                    Component[] allComponents = prefab.GetComponentsInChildren<Component>(true);
                    foreach (Component component in allComponents)
                    {
                        if (component == null)
                        {
                            Debug.LogWarning("Null component found in prefab: " + path);
                            continue;
                        }

                        ExtractFieldsRecursive(component, extractedText);
                    }
                }
            }
        }

        private static void ExtractFieldsRecursive(object obj, HashSet<string> extractedText)
        {
            if (obj == null) return;

            FieldInfo[] fields = obj.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (FieldInfo field in fields)
            {
                if (field.IsDefined(typeof(TranslatedAttribute), false))
                {
                    if (field.FieldType == typeof(string))
                    {
                        string fieldValue = field.GetValue(obj) as string;
                        extractedText.Add(fieldValue);
                    }
                    else if (!field.FieldType.IsPrimitive && !field.FieldType.IsEnum && field.FieldType.IsClass)
                    {
                        object nestedObj = field.GetValue(obj);
                        ExtractFieldsRecursive(nestedObj, extractedText);
                    }
                }
            }
        }

        public static void WriteToCSV(string filePath, List<string[]> rowData)
        {
            using (StreamWriter outStream = new StreamWriter(filePath))
            {
                foreach (string[] row in rowData)
                {
                    string[] escapedFields = row.Select(field => 
                    {
                        if (string.IsNullOrEmpty(field))
                            return "";
                            
                        bool needsQuotes = field.Contains(",") || field.Contains("\"") || field.Contains("\n");
                        if (needsQuotes)
                        {
                            return $"\"{field.Replace("\"", "\"\"")}\"";
                        }
                        return field;
                    }).ToArray();
                    
                    outStream.WriteLine(string.Join(",", escapedFields));
                }
            }
        }

        public static void UpdateTranslationData(
            TranslationData translationData,
            HashSet<string> newKeys,
            KeyUpdateMode updateMode)
        {
            if (updateMode == KeyUpdateMode.Replace)
            {
                // Store existing translations before clearing
                Dictionary<string, Dictionary<string, string>> existingTranslations = new Dictionary<string, Dictionary<string, string>>();
                
                // Load existing translations
                for (int i = 0; i < translationData.allKeys.Count; i++)
                {
                    string key = translationData.allKeys[i];
                    existingTranslations[key] = new Dictionary<string, string>();

                    for (int j = 0; j < translationData.languageDataDictionary.Length; j++)
                    {
                        string language = translationData.supportedLanguages[j + 1];
                        string assetPath = AssetDatabase.GUIDToAssetPath(translationData.languageDataDictionary[j].AssetGUID);
                        LanguageData languageData = AssetDatabase.LoadAssetAtPath<LanguageData>(assetPath);
                        
                        if (languageData != null && i < languageData.allText.Count)
                        {
                            existingTranslations[key][language] = languageData.allText[i];
                        }
                    }
                }

                // Clear existing data
                translationData.allKeys.Clear();
                foreach (var assetRef in translationData.languageDataDictionary)
                {
                    string assetPath = AssetDatabase.GUIDToAssetPath(assetRef.AssetGUID);
                    LanguageData languageData = AssetDatabase.LoadAssetAtPath<LanguageData>(assetPath);
                    if (languageData != null)
                    {
                        languageData.allText.Clear();
                    }
                }

                // Add new keys
                foreach (string key in newKeys)
                {
                    translationData.allKeys.Add(key);
                    
                    // Add translations for each language
                    foreach (var assetRef in translationData.languageDataDictionary)
                    {
                        string assetPath = AssetDatabase.GUIDToAssetPath(assetRef.AssetGUID);
                        LanguageData languageData = AssetDatabase.LoadAssetAtPath<LanguageData>(assetPath);
                        if (languageData != null)
                        {
                            string translation = "";
                            if (existingTranslations.TryGetValue(key, out var translations))
                            {
                                string language = translationData.supportedLanguages[translationData.languageDataDictionary.ToList().IndexOf(assetRef) + 1];
                                translation = translations.TryGetValue(language, out string existingTranslation) ? existingTranslation : "";
                            }
                            languageData.allText.Add(translation);
                            EditorUtility.SetDirty(languageData);
                        }
                    }
                }
            }
            else // Merge mode
            {
                foreach (string key in newKeys)
                {
                    if (!translationData.allKeys.Contains(key))
                    {
                        translationData.allKeys.Add(key);
                        
                        // Add empty translation for each language
                        foreach (var assetRef in translationData.languageDataDictionary)
                        {
                            string assetPath = AssetDatabase.GUIDToAssetPath(assetRef.AssetGUID);
                            LanguageData languageData = AssetDatabase.LoadAssetAtPath<LanguageData>(assetPath);
                            if (languageData != null)
                            {
                                languageData.allText.Add("");
                                EditorUtility.SetDirty(languageData);
                            }
                        }
                    }
                }
            }

            EditorUtility.SetDirty(translationData);
            AssetDatabase.SaveAssets();
        }
    }
}
#endif
