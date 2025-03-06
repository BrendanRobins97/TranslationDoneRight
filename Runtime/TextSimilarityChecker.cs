#if UNITY_EDITOR
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;
using UnityEditor;

namespace Translations
{
    /// <summary>
    /// Represents a group of similar texts
    /// </summary>
    public class TextSimilarityGroup
    {
        public List<string> Texts { get; set; } = new List<string>();
        public string SelectedText { get; set; }
        public string Reason { get; set; }
        public float AverageSimilarityScore { get; set; }
        public string SourceInfo { get; set; }

        public string GetGroupKey() => string.Join("|", Texts.OrderBy(t => t));
    }

    /// <summary>
    /// Provides intelligent text similarity detection for translation keys.
    /// Uses multiple algorithms to detect similar text entries and avoid duplication.
    /// </summary>
    public static class TextSimilarityChecker
    {
        // Replace const thresholds with properties
        private static float _levenshteinThreshold = EditorPrefs.GetFloat("TextSimilarity_LevenshteinThreshold", 0.85f);
        private static float _caseInsensitiveThreshold = EditorPrefs.GetFloat("TextSimilarity_CaseInsensitiveThreshold", 0.95f);
        private static float _punctuationThreshold = EditorPrefs.GetFloat("TextSimilarity_PunctuationThreshold", 0.90f);

        public static float LevenshteinThreshold
        {
            get => _levenshteinThreshold;
            set
            {
                _levenshteinThreshold = value;
                EditorPrefs.SetFloat("TextSimilarity_LevenshteinThreshold", value);
            }
        }

        public static float CaseInsensitiveThreshold
        {
            get => _caseInsensitiveThreshold;
            set
            {
                _caseInsensitiveThreshold = value;
                EditorPrefs.SetFloat("TextSimilarity_CaseInsensitiveThreshold", value);
            }
        }

        public static float PunctuationThreshold
        {
            get => _punctuationThreshold;
            set
            {
                _punctuationThreshold = value;
                EditorPrefs.SetFloat("TextSimilarity_PunctuationThreshold", value);
            }
        }

        private static readonly char[] CommonPunctuation = new[] { '.', '!', '?', ',', ';', ':', '-', '(', ')', '[', ']', '{', '}' };

        /// <summary>
        /// Checks a collection of texts for similar entries and logs warnings for potential duplicates.
        /// </summary>
        /// <param name="texts">The collection of texts to check</param>
        /// <param name="sourceInfo">Optional source information for better error messages</param>
        public static void CheckForSimilarTexts(IEnumerable<string> texts, string sourceInfo = null)
        {
            var textList = texts.Where(t => !string.IsNullOrEmpty(t)).ToList();
            var processedPairs = new HashSet<string>(); // Track pairs we've already compared
            
            for (int i = 0; i < textList.Count; i++)
            {
                for (int j = i + 1; j < textList.Count; j++)
                {
                    var text1 = textList[i];
                    var text2 = textList[j];
                    
                    // Create a unique key for this pair
                    var pairKey = $"{text1}|{text2}";
                    if (processedPairs.Contains(pairKey)) continue;
                    processedPairs.Add(pairKey);
                    
                    // Check similarity using different methods
                    var similarity = GetTextSimilarity(text1, text2);
                    
                    if (similarity.IsSimilar)
                    {
                        string message = $"Potential duplicate translation keys found{(sourceInfo != null ? $" in {sourceInfo}" : "")}:\n" +
                                       $"1: \"{text1}\"\n" +
                                       $"2: \"{text2}\"\n" +
                                       $"Reason: {similarity.Reason}";
                        
                        Debug.LogWarning(message);
                    }
                }
            }
        }

        /// <summary>
        /// Checks if a new text is similar to any existing texts.
        /// </summary>
        /// <param name="newText">The new text to check</param>
        /// <param name="existingTexts">The collection of existing texts</param>
        /// <param name="sourceInfo">Optional source information for better error messages</param>
        /// <returns>True if similar texts were found</returns>
        public static bool CheckNewTextSimilarity(string newText, IEnumerable<string> existingTexts, string sourceInfo = null)
        {
            if (string.IsNullOrEmpty(newText)) return false;
            
            bool foundSimilar = false;
            foreach (var existingText in existingTexts)
            {
                if (string.IsNullOrEmpty(existingText)) continue;
                
                var similarity = GetTextSimilarity(newText, existingText);
                if (similarity.IsSimilar)
                {
                    string message = $"New translation key is similar to existing key{(sourceInfo != null ? $" in {sourceInfo}" : "")}:\n" +
                                   $"New: \"{newText}\"\n" +
                                   $"Existing: \"{existingText}\"\n" +
                                   $"Reason: {similarity.Reason}";
                    
                    Debug.LogWarning(message);
                    foundSimilar = true;
                }
            }
            
            return foundSimilar;
        }

        /// <summary>
        /// Generates a report of all similar text groups in the collection.
        /// </summary>
        /// <param name="texts">The collection of texts to check</param>
        /// <param name="sourceInfo">Optional source information for better error messages</param>
        /// <returns>A list of similar text groups</returns>
        public static List<TextSimilarityGroup> GenerateSimilarityReport(IEnumerable<string> texts, string sourceInfo = null)
        {
            var textList = texts.Where(t => !string.IsNullOrEmpty(t)).ToList();
            var similarityGroups = new List<TextSimilarityGroup>();
            var processedTexts = new HashSet<string>();
            var textGroups = new Dictionary<string, HashSet<string>>();
            var groupScores = new Dictionary<string, List<float>>();
            var groupReasons = new Dictionary<string, string>();

            // First pass: Find all similar pairs and build initial groups
            for (int i = 0; i < textList.Count; i++)
            {
                if (processedTexts.Contains(textList[i])) continue;

                var currentGroup = new HashSet<string> { textList[i] };
                var currentScores = new List<float> { 1.0f };
                string currentReason = "";

                // Compare with all other texts
                for (int j = 0; j < textList.Count; j++)
                {
                    if (i == j || processedTexts.Contains(textList[j])) continue;

                    var (isSimilar, reason, score) = GetTextSimilarityWithScore(textList[i], textList[j]);
                    if (isSimilar)
                    {
                        currentGroup.Add(textList[j]);
                        currentScores.Add(score);
                        if (string.IsNullOrEmpty(currentReason)) currentReason = reason;
                    }
                }

                // If we found similar texts, store the group
                if (currentGroup.Count > 1)
                {
                    string groupKey = string.Join("|", currentGroup.OrderBy(t => t));
                    textGroups[groupKey] = currentGroup;
                    groupScores[groupKey] = currentScores;
                    groupReasons[groupKey] = currentReason;

                    // Mark all texts in this group as processed
                    foreach (var text in currentGroup)
                    {
                        processedTexts.Add(text);
                    }
                }
            }

            // Second pass: Merge overlapping groups
            bool mergedAny;
            do
            {
                mergedAny = false;
                var groupKeys = textGroups.Keys.ToList();

                for (int i = 0; i < groupKeys.Count; i++)
                {
                    for (int j = i + 1; j < groupKeys.Count; j++)
                    {
                        var group1 = textGroups[groupKeys[i]];
                        var group2 = textGroups[groupKeys[j]];

                        // Check if groups share any texts or have similar texts
                        bool shouldMerge = group1.Overlaps(group2);
                        if (!shouldMerge)
                        {
                            // Check if any texts between groups are similar
                            foreach (var text1 in group1)
                            {
                                foreach (var text2 in group2)
                                {
                                    var (isSimilar, _, score) = GetTextSimilarityWithScore(text1, text2);
                                    if (isSimilar)
                                    {
                                        shouldMerge = true;
                                        break;
                                    }
                                }
                                if (shouldMerge) break;
                            }
                        }

                        if (shouldMerge)
                        {
                            // Merge groups
                            var mergedGroup = new HashSet<string>(group1);
                            mergedGroup.UnionWith(group2);
                            var mergedScores = new List<float>();
                            mergedScores.AddRange(groupScores[groupKeys[i]]);
                            mergedScores.AddRange(groupScores[groupKeys[j]]);

                            string newGroupKey = string.Join("|", mergedGroup.OrderBy(t => t));
                            textGroups[newGroupKey] = mergedGroup;
                            groupScores[newGroupKey] = mergedScores;
                            groupReasons[newGroupKey] = groupReasons[groupKeys[i]]; // Keep the first reason

                            // Remove old groups
                            textGroups.Remove(groupKeys[i]);
                            textGroups.Remove(groupKeys[j]);
                            groupScores.Remove(groupKeys[i]);
                            groupScores.Remove(groupKeys[j]);
                            groupReasons.Remove(groupKeys[i]);
                            groupReasons.Remove(groupKeys[j]);

                            mergedAny = true;
                            break;
                        }
                    }
                    if (mergedAny) break;
                }
            } while (mergedAny);

            // Create final similarity groups
            foreach (var groupKey in textGroups.Keys)
            {
                var group = new TextSimilarityGroup
                {
                    Texts = textGroups[groupKey].ToList(),
                    SelectedText = textGroups[groupKey].OrderByDescending(t => t.Length).First(), // Select longest text as default
                    Reason = groupReasons[groupKey],
                    AverageSimilarityScore = groupScores[groupKey].Average(),
                    SourceInfo = sourceInfo
                };
                similarityGroups.Add(group);
            }

            return similarityGroups.OrderByDescending(g => g.AverageSimilarityScore).ToList();
        }

        private static (bool IsSimilar, string Reason) GetTextSimilarity(string text1, string text2)
        {
            // Check for case-insensitive exact match
            if (string.Equals(text1, text2, StringComparison.OrdinalIgnoreCase))
            {
                return (true, "Texts are identical except for letter case");
            }

            // Check if texts are same when punctuation is removed
            var text1NoPunct = RemovePunctuation(text1);
            var text2NoPunct = RemovePunctuation(text2);
            if (string.Equals(text1NoPunct, text2NoPunct, StringComparison.OrdinalIgnoreCase))
            {
                return (true, "Texts are identical except for punctuation");
            }

            // Calculate Levenshtein similarity
            float levenshteinSimilarity = CalculateLevenshteinSimilarity(text1, text2);
            
            // If very similar with different case
            if (levenshteinSimilarity >= CaseInsensitiveThreshold)
            {
                float caseSensitiveSimilarity = CalculateLevenshteinSimilarity(text1.ToLower(), text2.ToLower());
                if (caseSensitiveSimilarity > levenshteinSimilarity)
                {
                    return (true, $"Texts are {(caseSensitiveSimilarity * 100):F0}% similar (mainly case differences)");
                }
            }
            
            // If similar with punctuation differences
            if (levenshteinSimilarity >= PunctuationThreshold)
            {
                float noPunctSimilarity = CalculateLevenshteinSimilarity(text1NoPunct, text2NoPunct);
                if (noPunctSimilarity > levenshteinSimilarity)
                {
                    return (true, $"Texts are {(noPunctSimilarity * 100):F0}% similar (mainly punctuation differences)");
                }
            }

            // General high similarity
            if (levenshteinSimilarity >= LevenshteinThreshold)
            {
                return (true, $"Texts are {(levenshteinSimilarity * 100):F0}% similar");
            }

            return (false, string.Empty);
        }

        private static float CalculateLevenshteinSimilarity(string s1, string s2)
        {
            var distance = CalculateLevenshteinDistance(s1, s2);
            var maxLength = Math.Max(s1.Length, s2.Length);
            return maxLength > 0 ? 1 - ((float)distance / maxLength) : 1;
        }

        private static int CalculateLevenshteinDistance(string s1, string s2)
        {
            if (string.IsNullOrEmpty(s1)) return string.IsNullOrEmpty(s2) ? 0 : s2.Length;
            if (string.IsNullOrEmpty(s2)) return s1.Length;

            var distances = new int[s1.Length + 1, s2.Length + 1];

            // Initialize first row and column
            for (int i = 0; i <= s1.Length; i++) distances[i, 0] = i;
            for (int j = 0; j <= s2.Length; j++) distances[0, j] = j;

            for (int i = 1; i <= s1.Length; i++)
            {
                for (int j = 1; j <= s2.Length; j++)
                {
                    int cost = (s1[i - 1] == s2[j - 1]) ? 0 : 1;
                    distances[i, j] = Math.Min(
                        Math.Min(distances[i - 1, j] + 1, distances[i, j - 1] + 1),
                        distances[i - 1, j - 1] + cost
                    );
                }
            }

            return distances[s1.Length, s2.Length];
        }

        private static string RemovePunctuation(string text)
        {
            return new string(text.Where(c => !CommonPunctuation.Contains(c)).ToArray()).Trim();
        }

        private static (bool IsSimilar, string Reason, float Score) GetTextSimilarityWithScore(string text1, string text2)
        {
            // Check for case-insensitive exact match
            if (string.Equals(text1, text2, StringComparison.OrdinalIgnoreCase))
            {
                return (true, "Texts are identical except for letter case", 1.0f);
            }

            // Check if texts are same when punctuation is removed
            var text1NoPunct = RemovePunctuation(text1);
            var text2NoPunct = RemovePunctuation(text2);
            if (string.Equals(text1NoPunct, text2NoPunct, StringComparison.OrdinalIgnoreCase))
            {
                return (true, "Texts are identical except for punctuation", 0.95f);
            }

            // Calculate Levenshtein similarity
            float levenshteinSimilarity = CalculateLevenshteinSimilarity(text1, text2);
            
            // If very similar with different case
            if (levenshteinSimilarity >= CaseInsensitiveThreshold)
            {
                float caseSensitiveSimilarity = CalculateLevenshteinSimilarity(text1.ToLower(), text2.ToLower());
                if (caseSensitiveSimilarity > levenshteinSimilarity)
                {
                    return (true, $"Texts are {(caseSensitiveSimilarity * 100):F0}% similar (mainly case differences)", caseSensitiveSimilarity);
                }
            }
            
            // If similar with punctuation differences
            if (levenshteinSimilarity >= PunctuationThreshold)
            {
                float noPunctSimilarity = CalculateLevenshteinSimilarity(text1NoPunct, text2NoPunct);
                if (noPunctSimilarity > levenshteinSimilarity)
                {
                    return (true, $"Texts are {(noPunctSimilarity * 100):F0}% similar (mainly punctuation differences)", noPunctSimilarity);
                }
            }

            // General high similarity
            if (levenshteinSimilarity >= LevenshteinThreshold)
            {
                return (true, $"Texts are {(levenshteinSimilarity * 100):F0}% similar", levenshteinSimilarity);
            }

            return (false, string.Empty, 0f);
        }
    }
}
#endif 