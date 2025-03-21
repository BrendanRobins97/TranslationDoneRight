#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System;

namespace Translations
{
    /// <summary>
    /// Handles all CSV import/export operations for the translation system.
    /// </summary>
    public class TranslationCSVHandler
    {
        public static void ExtractToCSV(string filePath, TranslationData translationData)
        {
            var extractedText = TextExtractor.ExtractAllText();
            WriteNewCSVWithExistingTranslations(filePath, extractedText, translationData);
        }

        public static void UpdateExistingCSV(string filePath, TranslationData translationData)
        {
            var extractedText = TextExtractor.ExtractAllText();
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
                row[0] = translationData.allKeys[i]; // default language key

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

        public static void GenerateReport(string filePath, TranslationData translationData)
        {
            var extractedText = TextExtractor.ExtractAllText();
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

                writer.WriteLine("Active Extractors:");
                foreach (var extractor in TextExtractor.GetExtractors())
                {
                    bool isEnabled = TextExtractor.IsExtractorEnabled(extractor.GetType());
                    writer.WriteLine($"- [{(isEnabled ? "X" : " ")}] {extractor.GetType().Name}");
                    writer.WriteLine($"    Priority: {extractor.Priority}");
                    writer.WriteLine($"    Description: {extractor.Description}\n");
                }

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
                        var sources = TranslationMetaDataProvider.Metadata.GetSources(key);
                        if (sources.Count > 0)
                        {
                            writer.WriteLine("  Last known locations:");
                            foreach (var source in sources)
                            {
                                writer.WriteLine($"    - {source.sourceType} in {source.sourcePath}");
                            }
                        }
                    }
                }

                if (missingKeys.Count > 0)
                {
                    writer.WriteLine("\nMissing Keys:");
                    foreach (var key in missingKeys)
                    {
                        writer.WriteLine($"- {key}");
                        var sources = TranslationMetaDataProvider.Metadata.GetSources(key);
                        if (sources.Count > 0)
                        {
                            writer.WriteLine("  Found in:");
                            foreach (var source in sources)
                            {
                                writer.WriteLine($"    - {source.sourceType} in {source.sourcePath}");
                            }
                        }
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
                translationRow[0] = key; // default language (key)

                // Get translations for each language
                for (int langIndex = 0; langIndex < translationData.languageDataDictionary.Length; langIndex++)
                {
                    string assetPath = AssetDatabase.GUIDToAssetPath(translationData.languageDataDictionary[langIndex].AssetGUID);
                    LanguageData languageData = AssetDatabase.LoadAssetAtPath<LanguageData>(assetPath);
                    
                    if (languageData != null && keyIndex < languageData.allText.Count)
                    {
                        // langIndex + 1 because first column is default language
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
                    // Create new row with just the default language text
                    string[] newRow = new string[translationData.supportedLanguages.Count];
                    newRow[0] = text;
                    rows.Add(newRow);
                }
            }

            WriteToCSV(filePath, rows);
        }

        private static void WriteToCSV(string filePath, List<string[]> rowData)
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
    }
}
#endif 