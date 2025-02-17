#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace PSS
{
    public class SimilarityReviewWindow : EditorWindow
    {
        private TranslationData translationData;
        private List<TextSimilarityGroup> similarityGroups;
        private Vector2 scrollPosition;
        private Dictionary<string, (string selectedText, bool isAccepted)> tempSelections = 
            new Dictionary<string, (string selectedText, bool isAccepted)>();
        private bool isViewingExistingGroups;

        public static void ShowWindow(TranslationData data)
        {
            var window = GetWindow<SimilarityReviewWindow>("Similar Text Review");
            window.translationData = data;
            window.isViewingExistingGroups = false;
            window.GenerateReport();
            window.Show();
        }

        public static void ShowWindowWithExistingGroups(TranslationData data)
        {
            var window = GetWindow<SimilarityReviewWindow>("Current Similar Groups");
            window.translationData = data;
            window.isViewingExistingGroups = true;
            window.LoadExistingGroups();
            window.Show();
        }

        private void LoadExistingGroups()
        {
            if (translationData == null) return;
            
            // Get all texts that are part of similarity groups
            var groupedTexts = translationData.GetAllGroupedTexts();
            
            // Create similarity groups from existing selections
            similarityGroups = new List<TextSimilarityGroup>();
            foreach (var textGroup in groupedTexts)
            {
                var (selectedText, isAccepted) = translationData.GetGroupStatus(textGroup);
                var metadata = translationData.GetGroupMetadata(textGroup);
                
                var group = new TextSimilarityGroup
                {
                    Texts = textGroup,
                    SelectedText = !string.IsNullOrEmpty(selectedText) ? selectedText : textGroup.OrderByDescending(t => t.Length).First(),
                    Reason = metadata?.reason ?? "Existing similarity group",
                    AverageSimilarityScore = metadata?.similarityScore ?? 1.0f,
                    SourceInfo = metadata?.sourceInfo
                };
                
                similarityGroups.Add(group);
                tempSelections[group.GetGroupKey()] = (selectedText, isAccepted);
            }
        }

        private void GenerateReport()
        {
            if (translationData == null) return;
            similarityGroups = TextSimilarityChecker.GenerateSimilarityReport(translationData.allKeys)
                .OrderByDescending(g => g.AverageSimilarityScore)
                .ToList();

            // Initialize temp selections with current selections
            tempSelections.Clear();
            foreach (var group in similarityGroups)
            {
                var (selectedText, isAccepted) = translationData.GetGroupStatus(group.Texts);
                tempSelections[group.GetGroupKey()] = (
                    selectedText ?? group.SelectedText,
                    isAccepted
                );
            }
        }

        private void OnGUI()
        {
            if (translationData == null || similarityGroups == null)
            {
                EditorGUILayout.HelpBox("No translation data available.", MessageType.Warning);
                return;
            }

            EditorGUILayout.Space(10);
            
            // Header with mode indication
            string headerText = isViewingExistingGroups ? 
                $"Current Similar Text Groups: {similarityGroups.Count}" :
                $"Similar Text Groups Found: {similarityGroups.Count}";
            EditorGUILayout.LabelField(headerText, EditorStyles.boldLabel);
            
            if (isViewingExistingGroups)
            {
                EditorGUILayout.HelpBox(
                    "Showing currently defined similarity groups. Use 'Review Similar Texts' to find new similar texts.", 
                    MessageType.Info
                );
            }
            
            EditorGUILayout.Space(5);

            if (similarityGroups.Count == 0)
            {
                string message = isViewingExistingGroups ? 
                    "No similarity groups are currently defined." :
                    "No similar texts found.";
                EditorGUILayout.HelpBox(message, MessageType.Info);
                return;
            }

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            foreach (var group in similarityGroups)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                
                // Group info
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Group Similarity: {group.AverageSimilarityScore:P0}", GUILayout.Width(150));
                EditorGUILayout.LabelField($"Texts in group: {group.Texts.Count}");
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.LabelField($"Reason: {group.Reason}");
                if (!string.IsNullOrEmpty(group.SourceInfo))
                {
                    EditorGUILayout.LabelField($"Source: {group.SourceInfo}");
                }

                // Show timestamps if available
                var metadata = translationData.GetGroupMetadata(group.Texts);
                if (metadata != null)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField($"Created: {metadata.createdTime:g}", EditorStyles.miniLabel);
                    EditorGUILayout.LabelField($"Modified: {metadata.lastModifiedTime:g}", EditorStyles.miniLabel);
                    EditorGUILayout.EndHorizontal();
                }

                string groupKey = group.GetGroupKey();
                var (currentSelection, isAccepted) = tempSelections.TryGetValue(groupKey, out var status) ? 
                    status : (group.SelectedText, false);

                EditorGUILayout.Space(5);

                // Begin toggle group
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                // Accept variations option
                bool newAccepted = EditorGUILayout.ToggleLeft(
                    "Accept all texts as valid variations",
                    isAccepted,
                    EditorStyles.boldLabel
                );

                // Update selection based on toggle changes
                if (newAccepted != isAccepted)
                {
                    if (newAccepted)
                    {
                        tempSelections[groupKey] = (currentSelection, true);
                    }
                    else
                    {
                        tempSelections[groupKey] = (currentSelection ?? group.SelectedText, false);
                    }
                }

                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Select Text to Use:", EditorStyles.boldLabel);

                foreach (var text in group.Texts)
                {
                    EditorGUILayout.BeginHorizontal();
                    bool isSelected = text == currentSelection && !isAccepted;
                    bool newSelected = EditorGUILayout.ToggleLeft(text, isSelected);
                    
                    if (newSelected && !isSelected)
                    {
                        tempSelections[groupKey] = (text, false);
                    }

                    // Display metadata in gray and smaller font
                    if (translationData.Metadata != null)
                    {
                        var sources = translationData.Metadata.GetSources(text);
                        if (sources.Count > 0)
                        {
                            var style = new GUIStyle(EditorStyles.miniLabel)
                            {
                                normal = { textColor = new Color(0.5f, 0.5f, 0.5f) }
                            };
                            var sourceInfo = string.Join(", ", sources.Select(s => 
                                $"{s.sourceType} in {System.IO.Path.GetFileName(s.sourcePath)}"));
                            EditorGUILayout.LabelField($"({sourceInfo})", style, GUILayout.Width(300));
                        }
                    }

                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.EndVertical();
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(5);
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(10);
            
            // Apply changes button
            if (GUILayout.Button("Apply All Changes"))
            {
                ApplyChanges();
            }
        }

        private void ApplyChanges()
        {
            bool hasChanges = false;
            
            foreach (var group in similarityGroups)
            {
                string groupKey = group.GetGroupKey();
                if (tempSelections.TryGetValue(groupKey, out var status))
                {
                    if (!status.isAccepted)
                    {
                        translationData.SetGroupStatus(group.Texts, status.selectedText);
                        translationData.SetGroupMetadata(
                            group.Texts,
                            group.Reason,
                            group.AverageSimilarityScore,
                            group.SourceInfo
                        );
                        hasChanges = true;
                    }
                    else
                    {
                        translationData.ClearGroupStatus(group.Texts);
                        translationData.ClearGroupMetadata(group.Texts);
                        hasChanges = true;
                    }
                }
            }

            if (hasChanges)
            {
                EditorUtility.SetDirty(translationData);
                AssetDatabase.SaveAssets();
                Debug.Log("Applied similarity selections.");
            }
        }
    }
}
#endif 