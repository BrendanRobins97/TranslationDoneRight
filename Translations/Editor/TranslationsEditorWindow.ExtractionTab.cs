using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace PSS
{
    public partial class TranslationsEditorWindow
    {
        private Vector2 extractionTabScrollPosition;
        private bool showExtractionSources = true;
        private bool showExtractionTools = true;
        private bool showCSVTools = true;

        private float GetHalfCardWidth()
        {
            // Account for scrollbar width, padding, and spacing
            float scrollbarWidth = 20f;
            float horizontalSpacing = 10f;
            float availableWidth = position.width - scrollbarWidth - (horizontalSpacing * 3); // Account for left, middle, and right spacing
            return availableWidth / 2;
        }

        private void DrawTextExtractionTab()
        {
            float cardWidth = GetHalfCardWidth();
            extractionTabScrollPosition = EditorGUILayout.BeginScrollView(extractionTabScrollPosition);

            // Header
            EditorGUILayout.Space(10);
            GUILayout.Label("Text Extraction", EditorGUIStyleUtility.HeaderLabel);
            EditorGUILayout.Space(5);
            EditorGUILayout.HelpBox("Configure and manage text extraction from your game assets.", MessageType.None);
            EditorGUILayout.Space(10);

            // Update Mode Selection
            using (new EditorGUILayout.VerticalScope(EditorGUIStyleUtility.CardStyle))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PrefixLabel(new GUIContent("Update Mode", "Choose how new keys are handled"));
                    updateMode = (KeyUpdateMode)EditorGUILayout.EnumPopup(updateMode);
                    
                    string modeDescription = updateMode switch
                    {
                        KeyUpdateMode.Replace => "Replaces existing keys while preserving translations",
                        KeyUpdateMode.Merge => "Adds new keys while keeping existing ones",
                        _ => string.Empty
                    };
                    
                    if (!string.IsNullOrEmpty(modeDescription))
                    {
                        EditorGUILayout.LabelField(new GUIContent("ⓘ", modeDescription), GUILayout.Width(20));
                    }
                }
            }
            EditorGUILayout.Space(10);

            // Extraction Sources Section
            showExtractionSources = EditorGUILayout.Foldout(showExtractionSources, "Extraction Sources", true, EditorGUIStyleUtility.FoldoutHeader);
            if (showExtractionSources)
            {
                EditorGUI.indentLevel++;

                var extractors = TextExtractor.GetExtractors();
                foreach (var extractor in extractors)
                {
                    var extractorType = extractor.GetType();
                    bool isEnabled = TextExtractor.IsExtractorEnabled(extractorType);
                    
                    using (new EditorGUILayout.VerticalScope(EditorGUIStyleUtility.CardStyle))
                    {
                        // Extractor Header
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            var newEnabled = EditorGUILayout.ToggleLeft(
                                new GUIContent(
                                    $"{extractor.SourceType} Extractor",
                                    extractor.Description
                                ),
                                isEnabled,
                                EditorStyles.boldLabel
                            );
                            
                            GUILayout.FlexibleSpace();
                            EditorGUILayout.LabelField($"Priority: {extractor.Priority}", EditorStyles.miniLabel);
                            
                            if (newEnabled != isEnabled)
                            {
                                TextExtractor.SetExtractorEnabled(extractorType, newEnabled);
                            }
                        }

                        // Description
                        if (isEnabled)
                        {
                            EditorGUILayout.LabelField(extractor.Description, EditorStyles.miniLabel);
                        }
                    }
                    EditorGUILayout.Space(2);
                }

                EditorGUI.indentLevel--;
            }
            EditorGUILayout.Space(10);

            // Extraction Tools Section
            showExtractionTools = EditorGUILayout.Foldout(showExtractionTools, "Extraction Tools", true, EditorGUIStyleUtility.FoldoutHeader);
            if (showExtractionTools)
            {
                // Direct Update and Text Similarity Cards (Side by Side)
                using (new EditorGUILayout.HorizontalScope())
                {
                    // Direct Update Card
                    using (new EditorGUILayout.VerticalScope(EditorGUIStyleUtility.CardStyle, GUILayout.Width(cardWidth)))
                    {
                        EditorGUILayout.LabelField("Direct Update", EditorStyles.boldLabel);
                        EditorGUILayout.LabelField("Updates translation keys while preserving existing translations", EditorStyles.miniLabel);
                        EditorGUILayout.Space(5);
                        
                        if (GUILayout.Button(new GUIContent("Extract and Update Keys", "Extract text and update TranslationData asset")))
                        {
                            if (EditorUtility.DisplayDialog("Extract Text", 
                                $"This will {(updateMode == KeyUpdateMode.Replace ? "replace" : "merge")} translation keys. Existing translations will be preserved. Continue?", 
                                "Extract", "Cancel"))
                            {
                                var extractedText = TextExtractor.ExtractAllText();
                                TextExtractor.UpdateTranslationData(translationData, extractedText, updateMode);
                                needsCoverageUpdate = true;
                            }
                        }
                    }

                    GUILayout.Space(10);

                    // Text Similarity Tools Card
                    using (new EditorGUILayout.VerticalScope(EditorGUIStyleUtility.CardStyle, GUILayout.Width(cardWidth)))
                    {
                        EditorGUILayout.LabelField("Text Similarity Tools", EditorStyles.boldLabel);
                        EditorGUILayout.LabelField("Find and manage similar text entries", EditorStyles.miniLabel);
                        EditorGUILayout.Space(5);

                        using (new EditorGUILayout.HorizontalScope())
                        {
                            if (GUILayout.Button(new GUIContent("View Current Groups", "View existing similar text groups")))
                            {
                                SimilarityReviewWindow.ShowWindowWithExistingGroups(translationData);
                            }
                            if (GUILayout.Button(new GUIContent("Find Similar Texts", "Analyze and find new similar text groups")))
                            {
                                SimilarityReviewWindow.ShowWindow(translationData);
                            }
                        }
                    }
                }

                EditorGUILayout.Space(5);

                // Similarity Settings Card
                if (showSimilaritySettings)
                {
                    using (new EditorGUILayout.VerticalScope(EditorGUIStyleUtility.CardStyle))
                    {
                        showSimilaritySettings = EditorGUILayout.Foldout(showSimilaritySettings, "Similarity Settings", true);
                        if (showSimilaritySettings)
                        {
                            EditorGUI.indentLevel++;
                            EditorGUI.BeginChangeCheck();
                            
                            float newLevenshtein = EditorGUILayoutSliderWithReset(
                                "General Similarity",
                                TextSimilarityChecker.LevenshteinThreshold,
                                0.5f, 1f, 0.85f,
                                "Minimum similarity threshold for general text comparison"
                            );

                            float newCaseInsensitive = EditorGUILayoutSliderWithReset(
                                "Case Differences",
                                TextSimilarityChecker.CaseInsensitiveThreshold,
                                0.5f, 1f, 0.95f,
                                "Threshold for texts that differ only in letter case"
                            );

                            float newPunctuation = EditorGUILayoutSliderWithReset(
                                "Punctuation Differences",
                                TextSimilarityChecker.PunctuationThreshold,
                                0.5f, 1f, 0.90f,
                                "Threshold for texts that differ only in punctuation"
                            );

                            if (EditorGUI.EndChangeCheck())
                            {
                                TextSimilarityChecker.LevenshteinThreshold = newLevenshtein;
                                TextSimilarityChecker.CaseInsensitiveThreshold = newCaseInsensitive;
                                TextSimilarityChecker.PunctuationThreshold = newPunctuation;
                            }
                            EditorGUI.indentLevel--;
                        }
                    }
                }
            }
            EditorGUILayout.Space(10);

            // CSV Management Section
            showCSVTools = EditorGUILayout.Foldout(showCSVTools, "CSV Management", true, EditorGUIStyleUtility.FoldoutHeader);
            if (showCSVTools)
            {
                // CSV Import/Export Cards (Side by Side)
                using (new EditorGUILayout.HorizontalScope())
                {
                    // CSV Export Card
                    using (new EditorGUILayout.VerticalScope(EditorGUIStyleUtility.CardStyle, GUILayout.Width(cardWidth)))
                    {
                        EditorGUILayout.LabelField("CSV Export", EditorStyles.boldLabel);
                        EditorGUILayout.LabelField("Export translations to CSV format for external editing", EditorStyles.miniLabel);
                        EditorGUILayout.Space(5);
                        
                        if (GUILayout.Button(new GUIContent("New CSV", "Create a new CSV file with extracted text")))
                        {
                            ExtractToNewCSV();
                        }
                        if (GUILayout.Button(new GUIContent("Update CSV", "Update an existing CSV file")))
                        {
                            ExtractToExistingCSV();
                        }
                        if (GUILayout.Button(new GUIContent("Export Current", "Export current translation keys to CSV")))
                        {
                            ExportCurrentKeysToCSV();
                        }
                    }

                    GUILayout.Space(10);

                    // CSV Import and Reports Card
                    using (new EditorGUILayout.VerticalScope(EditorGUIStyleUtility.CardStyle, GUILayout.Width(cardWidth)))
                    {
                        EditorGUILayout.LabelField("CSV Import", EditorStyles.boldLabel);
                        EditorGUILayout.LabelField("Import translations and generate reports", EditorStyles.miniLabel);
                        EditorGUILayout.Space(5);
                        
                        if (GUILayout.Button(new GUIContent("Import CSV", "Import translations from a CSV file")))
                        {
                            translationData.ImportCSV();
                        }
                        
                        EditorGUILayout.Space(5);
                        if (GUILayout.Button(new GUIContent("Generate Report", "Create a detailed translation status report")))
                        {
                            GenerateReport();
                        }
                    }
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private float EditorGUILayoutSliderWithReset(string label, float value, float min, float max, float defaultValue, string tooltip)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                var newValue = EditorGUILayout.Slider(new GUIContent(label, tooltip), value, min, max);
                if (GUILayout.Button(new GUIContent("↺", $"Reset to default ({defaultValue})"), GUILayout.Width(25)))
                {
                    newValue = defaultValue;
                }
                return newValue;
            }
        }

        private void ExtractToNewCSV()
        {
            string path = EditorUtility.SaveFilePanel(
                "Save CSV File",
                "",
                "translations.csv",
                "csv");
                
            if (!string.IsNullOrEmpty(path))
            {
                TranslationCSVHandler.ExtractToCSV(
                    path,
                    translationData
                );
            }
        }

        private void ExtractToExistingCSV()
        {
            string path = EditorUtility.OpenFilePanel("Select Existing CSV", "", "csv");
            if (!string.IsNullOrEmpty(path))
            {
                TranslationCSVHandler.UpdateExistingCSV(
                    path,
                    translationData
                );
            }
        }

        private void ExportCurrentKeysToCSV()
        {
            string path = EditorUtility.SaveFilePanel(
                "Export Current Keys",
                "",
                "current_translations.csv",
                "csv");
                
            if (!string.IsNullOrEmpty(path))
            {
                TranslationCSVHandler.ExportCurrentKeys(path, translationData);
            }
        }

        private void GenerateReport()
        {
            string path = EditorUtility.SaveFilePanel(
                "Save Translation Report",
                "",
                "translation_report.txt",
                "txt");
                
            if (!string.IsNullOrEmpty(path))
            {
                TranslationCSVHandler.GenerateReport(
                    path,
                    translationData
                );
            }
        }
    }
} 