#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System;
using System.IO;

namespace PSS
{
    /// <summary>
    /// Core text extraction functionality that can be used independently of the translation system.
    /// Manages and coordinates multiple text extractors to collect text from various sources.
    /// </summary>
    public class TextExtractor
    {
        private static List<ITextExtractor> _extractors;
        private static Dictionary<Type, bool> _extractorStates;

        // Events for extraction lifecycle
        public static event Action<HashSet<string>> OnExtractionComplete;
        public static event Action<ITextExtractor, Exception> OnExtractionError;
        public static event Action<ITextExtractor> OnExtractorStarted;
        public static event Action<ITextExtractor> OnExtractorFinished;
        public static event Action OnExtractionStarted;

        // Optional metadata for tracking text sources
        private static TranslationMetadata _metadata;
        public static TranslationMetadata Metadata 
        { 
            get => _metadata;
            set
            {
                bool shouldClear = value != null && value != _metadata;
                _metadata = value;
                if (shouldClear)
                {
                    _metadata.ClearAllSources();
                }
            }
        }

        static TextExtractor()
        {
            InitializeExtractors();
        }

        private static void InitializeExtractors()
        {
            _extractors = new List<ITextExtractor>();
            _extractorStates = new Dictionary<Type, bool>();

            // Find all types that implement ITextExtractor
            var extractorTypes = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(assembly => assembly.GetTypes())
                .Where(type => typeof(ITextExtractor).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract);

            foreach (var type in extractorTypes)
            {
                try
                {
                    var extractor = (ITextExtractor)Activator.CreateInstance(type);
                    _extractors.Add(extractor);
                    _extractorStates[type] = EditorPrefs.GetBool($"TextExtractor_{type.Name}_Enabled", extractor.EnabledByDefault);
                }
                catch (Exception e)
                {
                    Debug.LogError($"Failed to create instance of text extractor {type.Name}: {e.Message}");
                }
            }

            // Sort extractors by priority
            _extractors = _extractors.OrderByDescending(e => e.Priority).ToList();
        }

        public static IReadOnlyList<ITextExtractor> GetExtractors()
        {
            return _extractors.AsReadOnly();
        }

        public static bool IsExtractorEnabled(Type extractorType)
        {
            return _extractorStates.TryGetValue(extractorType, out bool enabled) && enabled;
        }

        public static void SetExtractorEnabled(Type extractorType, bool enabled)
        {
            if (_extractorStates.ContainsKey(extractorType))
            {
                _extractorStates[extractorType] = enabled;
                EditorPrefs.SetBool($"TextExtractor_{extractorType.Name}_Enabled", enabled);
            }
        }

        /// <summary>
        /// Extracts all text from enabled extractors.
        /// </summary>
        /// <returns>A HashSet containing all extracted text.</returns>
        public static HashSet<string> ExtractAllText()
        {
            HashSet<string> extractedText = new HashSet<string>();

            OnExtractionStarted?.Invoke();

            // Clear metadata if it exists
            Metadata?.ClearAllSources();

            foreach (var extractor in _extractors)
            {
                if (IsExtractorEnabled(extractor.GetType()))
                {
                    try
                    {
                        OnExtractorStarted?.Invoke(extractor);
                        var newText = extractor.ExtractText(Metadata);
                        
                        // Check for similar texts within this extractor's results
                        TextSimilarityChecker.CheckForSimilarTexts(newText, $"extractor {extractor.GetType().Name}");
                        
                        // Check new texts against existing ones
                        foreach (var text in newText)
                        {
                            TextSimilarityChecker.CheckNewTextSimilarity(text, extractedText, $"comparing against existing texts");
                        }
                        
                        extractedText.UnionWith(newText);
                        OnExtractorFinished?.Invoke(extractor);
                    }
                    catch (Exception e)
                    {
                        OnExtractionError?.Invoke(extractor, e);
                        Debug.LogError($"Error in {extractor.GetType().Name}: {e.Message}\n{e.StackTrace}");
                    }
                }
            }

            // Final check for similar texts across all extractors
            TextSimilarityChecker.CheckForSimilarTexts(extractedText, "all extracted text");

            OnExtractionComplete?.Invoke(extractedText);
            return extractedText;
        }

        public static bool ShouldProcessPath(string assetPath, TranslationMetadata metadata)
        {
            if (metadata.extractionSources == null || metadata.extractionSources.Count == 0)
                return true;

            assetPath = assetPath.Replace('\\', '/').TrimStart('/');
            if (!assetPath.StartsWith("Assets/"))
                assetPath = "Assets/" + assetPath;

            foreach (var source in metadata.extractionSources)
            {
                if (source.type == ExtractionSourceType.Folder)
                {
                    string folderPath = source.folderPath?.Replace('\\', '/').TrimStart('/') ?? "";
                    if (!folderPath.StartsWith("Assets/"))
                        folderPath = "Assets/" + folderPath;

                    if (source.recursive)
                    {
                        if (assetPath.StartsWith(folderPath))
                            return true;
                    }
                    else
                    {
                        string directory = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
                        if (directory == folderPath)
                            return true;
                    }
                }
                else if (source.type == ExtractionSourceType.Asset && source.asset != null)
                {
                    string sourcePath = AssetDatabase.GetAssetPath(source.asset)?.Replace('\\', '/');
                    if (assetPath == sourcePath)
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Updates translation data with extracted text.
        /// </summary>
        /// <param name="translationData">The translation data to update.</param>
        /// <param name="extractedText">The extracted text to add.</param>
        /// <param name="updateMode">How to handle existing keys.</param>
        public static void UpdateTranslationData(TranslationData translationData, HashSet<string> extractedText, KeyUpdateMode updateMode)
        {
            if (translationData == null)
            {
                Debug.LogError("TranslationData asset is null");
                return;
            }

            // Clear metadata for keys that will be removed
            if (updateMode == KeyUpdateMode.Replace)
            {
                foreach (var key in translationData.allKeys)
                {
                    if (!extractedText.Contains(key))
                    {
                        translationData.Metadata?.ClearSources(key);
                    }
                }
                translationData.allKeys.Clear();
            }

            // Add new keys
            foreach (string text in extractedText)
            {
                if (!translationData.allKeys.Contains(text))
                {
                    translationData.allKeys.Add(text);
                    
                    // Add empty translations for each language
                    for (int i = 0; i < translationData.languageDataDictionary.Length; i++)
                    {
                        var assetRef = translationData.languageDataDictionary[i];
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

            // Sort keys alphabetically
            translationData.allKeys.Sort();
            EditorUtility.SetDirty(translationData);
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Extracts text from a specific type of extractor.
        /// </summary>
        /// <typeparam name="T">The type of extractor to use.</typeparam>
        /// <returns>A HashSet containing the extracted text.</returns>
        public static HashSet<string> ExtractTextFromType<T>() where T : ITextExtractor
        {
            var extractor = _extractors.FirstOrDefault(e => e is T);
            if (extractor == null)
            {
                Debug.LogError($"No extractor of type {typeof(T).Name} found");
                return new HashSet<string>();
            }

            try
            {
                OnExtractorStarted?.Invoke(extractor);
                var extractedText = extractor.ExtractText(Metadata);
                OnExtractorFinished?.Invoke(extractor);
                return extractedText;
            }
            catch (Exception e)
            {
                OnExtractionError?.Invoke(extractor, e);
                Debug.LogError($"Error in {extractor.GetType().Name}: {e.Message}\n{e.StackTrace}");
                return new HashSet<string>();
            }
        }

        /// <summary>
        /// Extracts text from specific types of extractors.
        /// </summary>
        /// <param name="extractorTypes">The types of extractors to use.</param>
        /// <returns>A HashSet containing all extracted text.</returns>
        public static HashSet<string> ExtractTextFromTypes(params Type[] extractorTypes)
        {
            HashSet<string> extractedText = new HashSet<string>();
            
            foreach (var type in extractorTypes)
            {
                if (!typeof(ITextExtractor).IsAssignableFrom(type))
                {
                    Debug.LogError($"Type {type.Name} does not implement ITextExtractor");
                    continue;
                }

                var extractor = _extractors.FirstOrDefault(e => e.GetType() == type);
                if (extractor == null)
                {
                    Debug.LogError($"No extractor of type {type.Name} found");
                    continue;
                }

                try
                {
                    OnExtractorStarted?.Invoke(extractor);
                    var newText = extractor.ExtractText(Metadata);
                    extractedText.UnionWith(newText);
                    OnExtractorFinished?.Invoke(extractor);
                }
                catch (Exception e)
                {
                    OnExtractionError?.Invoke(extractor, e);
                    Debug.LogError($"Error in {extractor.GetType().Name}: {e.Message}\n{e.StackTrace}");
                }
            }

            return extractedText;
        }
    }
}
#endif

