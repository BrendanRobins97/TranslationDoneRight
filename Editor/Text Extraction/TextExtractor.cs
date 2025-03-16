#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Translations
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
            set => _metadata = value;  // Don't clear sources on set
        }

        // Cache for path processing decisions to avoid redundant checks
        private static Dictionary<string, Dictionary<string, bool>> _pathProcessingCache = new Dictionary<string, Dictionary<string, bool>>();
        
        /// <summary>
        /// Clears the path processing cache
        /// </summary>
        public static void ClearPathProcessingCache()
        {
            _pathProcessingCache.Clear();
        }

        // Thread-safe tracking of extraction progress
        private static readonly object _progressLock = new object();
        private static float _extractionProgress = 0f;
        private static string _currentExtractorName = string.Empty;
        private static Dictionary<string, float> _extractorProgress = new Dictionary<string, float>();
        private static bool _isExtractionRunning = false;
        private static float _baseProgress = 0f;
        private static float _progressIncrement = 0f;

        public static float ExtractionProgress => _extractionProgress;
        public static string CurrentExtractorName => _currentExtractorName;
        public static bool IsExtractionRunning => _isExtractionRunning;

        /// <summary>
        /// Updates the progress of the current extractor (0-1 range)
        /// </summary>
        public static void UpdateExtractorProgress(ITextExtractor extractor, float progress)
        {
            if (!_isExtractionRunning) return;
            
            lock (_progressLock)
            {
                // Ensure progress is in valid range
                progress = Mathf.Clamp01(progress);
                
                // Calculate overall progress as base progress + (current extractor's progress * increment)
                _extractionProgress = _baseProgress + (progress * _progressIncrement);
                _currentExtractorName = extractor.GetType().Name;
                _extractorProgress[_currentExtractorName] = progress;
            }
            
            // Update the UI from the main thread
            if (EditorApplication.isPlaying)
            {
                EditorApplication.delayCall += UpdateExtractionProgressUI;
            }
            else
            {
                UpdateExtractionProgressUI();
            }
        }

        /// <summary>
        /// Updates the UI on the main thread during extraction
        /// </summary>
        public static void UpdateExtractionProgressUI()
        {
            if (_isExtractionRunning)
            {
                string currentName;
                float currentProgress;
                
                lock (_progressLock)
                {
                    currentName = _currentExtractorName;
                    currentProgress = _extractionProgress;
                }
                
                EditorUtility.DisplayProgressBar("Extracting Text", 
                    $"Running {currentName}... ({(currentProgress * 100):F0}%)", currentProgress);
            }
        }

        /// <summary>
        /// Resets extraction progress tracking
        /// </summary>
        private static void ResetExtractionProgress()
        {
            lock (_progressLock)
            {
                _isExtractionRunning = false;
                _extractionProgress = 0f;
                _baseProgress = 0f;
                _progressIncrement = 0f;
                _currentExtractorName = string.Empty;
                _extractorProgress.Clear();
            }
        }

        /// <summary>
        /// Sets up progress tracking for a new extractor
        /// </summary>
        private static void SetupExtractorProgress(ITextExtractor extractor, float baseProgress, float increment)
        {
            lock (_progressLock)
            {
                _isExtractionRunning = true;
                _currentExtractorName = extractor.GetType().Name;
                _baseProgress = baseProgress;
                _progressIncrement = increment;
                _extractionProgress = baseProgress;
                _extractorProgress[_currentExtractorName] = 0f;
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
            HashSet<string> extractedText = new HashSet<string>(StringComparer.Ordinal);

            OnExtractionStarted?.Invoke();
            ResetExtractionProgress();

            // Get list of enabled extractors
            var enabledExtractors = _extractors.Where(e => IsExtractorEnabled(e.GetType())).ToList();
            
            // Calculate per-extractor progress increment
            float progressIncrement = 1.0f / enabledExtractors.Count;
            float currentProgress = 0f;

            // Process each extractor sequentially to avoid threading issues with Unity API
            foreach (var extractor in enabledExtractors)
            {
                SetupExtractorProgress(extractor, currentProgress, progressIncrement);
                OnExtractorStarted?.Invoke(extractor);
                
                try
                {
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

                currentProgress += progressIncrement;
            }

            // Final check for similar texts across all extractors
            TextSimilarityChecker.CheckForSimilarTexts(extractedText, "all extracted text");

            // Clear the progress bar when we're done
            EditorUtility.ClearProgressBar();
            ResetExtractionProgress();
            OnExtractionComplete?.Invoke(extractedText);
            return extractedText;
        }

        public static bool ShouldProcessPath(string assetPath, TranslationMetadata metadata)
        {
            return ShouldProcessPath(assetPath, metadata, null);
        }

        /// <summary>
        /// Determines if the given asset path should be processed based on extraction sources.
        /// </summary>
        /// <param name="assetPath">The path to check</param>
        /// <param name="metadata">The translation metadata containing sources</param>
        /// <param name="extractorType">Optional extractor type to check specific sources</param>
        /// <returns>True if the path should be processed, false otherwise</returns>
        public static bool ShouldProcessPath(string assetPath, TranslationMetadata metadata, Type extractorType)
        {
            if (metadata == null) return true;
            
            // Normalize the asset path
            assetPath = assetPath.Replace('\\', '/').TrimStart('/');
            if (!assetPath.StartsWith("Assets/"))
                assetPath = "Assets/" + assetPath;
                
            // Generate cache key
            string extractorKey = extractorType?.Name ?? "global";
            
            // Check cache first
            if (_pathProcessingCache.TryGetValue(extractorKey, out var pathCache) && 
                pathCache.TryGetValue(assetPath, out var shouldProcess))
            {
                return shouldProcess;
            }
            
            bool result;
            
            // Check extractor-specific sources first
            if (extractorType != null && 
                metadata.extractorSources != null && 
                metadata.extractorSources.TryGetValue(extractorType.Name, out var extractorSources) && 
                extractorSources.Items.Count > 0)
            {
                // If extractor has specific sources, use only those
                result = CheckSourcesList(assetPath, extractorSources);
            }
            else
            {
                // Fall back to global sources
                if (metadata.extractionSources == null || metadata.extractionSources.Count == 0)
                    result = true;
                else
                    result = CheckSourcesList(assetPath, metadata.extractionSources);
            }
            
            // Cache the result
            if (!_pathProcessingCache.TryGetValue(extractorKey, out pathCache))
            {
                pathCache = new Dictionary<string, bool>(StringComparer.Ordinal);
                _pathProcessingCache[extractorKey] = pathCache;
            }
            
            pathCache[assetPath] = result;
            
            return result;
        }

        /// <summary>
        /// Helper method to check if an asset path matches any source in a list
        /// </summary>
        private static bool CheckSourcesList(string assetPath, ExtractionSourcesList sources)
        {
            if (sources == null || sources.Items.Count == 0)
                return true;
                
            foreach (var source in sources.Items)
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
            
            // Pre-load all language data assets to avoid repeated asset loading
            var languageDataAssets = new Dictionary<string, LanguageData>(translationData.languageDataDictionary.Length);
            for (int i = 0; i < translationData.languageDataDictionary.Length; i++)
            {
                var assetRef = translationData.languageDataDictionary[i];
                string assetPath = AssetDatabase.GUIDToAssetPath(assetRef.AssetGUID);
                LanguageData languageData = AssetDatabase.LoadAssetAtPath<LanguageData>(assetPath);
                
                if (languageData != null)
                {
                    languageDataAssets[assetRef.AssetGUID] = languageData;
                }
            }

            // For complete replacement, clear all data first
            if (updateMode == KeyUpdateMode.ReplaceCompletely)
            {
                // Clear all language data first
                foreach (var languageData in languageDataAssets.Values)
                {
                    languageData.allText.Clear();
                    EditorUtility.SetDirty(languageData);
                }

                // Clear main translation data
                translationData.allKeys.Clear();
            }
            else if (updateMode == KeyUpdateMode.ReplaceButPreserveMissing)
            {
                // Keep track of keys to remove
                var keysToRemove = new HashSet<string>(translationData.allKeys);
                keysToRemove.ExceptWith(extractedText);
                
                // Clear metadata only for keys that will be removed
                foreach (var key in keysToRemove)
                {
                    TranslationMetaDataProvider.Metadata?.ClearSources(key);
                }
                
                // Remove keys from all language data and from the main data
                foreach (var key in keysToRemove)
                {
                    int index = translationData.allKeys.IndexOf(key);
                    if (index >= 0)
                    {
                        translationData.allKeys.RemoveAt(index);
                        
                        // Remove corresponding entries in all language data
                        foreach (var languageData in languageDataAssets.Values)
                        {
                            if (index < languageData.allText.Count)
                            {
                                languageData.allText.RemoveAt(index);
                                EditorUtility.SetDirty(languageData);
                            }
                        }
                    }
                }
            }

            // Prepare sets to optimize contains checks
            HashSet<string> existingKeys = new HashSet<string>(translationData.allKeys);
            
            // Add new keys and their translations
            foreach (string text in extractedText)
            {
                if (!existingKeys.Contains(text))
                {
                    translationData.allKeys.Add(text);
                    
                    // Add empty translations for each language
                    foreach (var languageData in languageDataAssets.Values)
                    {
                        languageData.allText.Add("");
                        EditorUtility.SetDirty(languageData);
                    }
                }
            }

            // Clear the path cache, as extraction sources may have changed
            ClearPathProcessingCache();

            // Sort keys alphabetically for better organization
            translationData.allKeys.Sort();
            EditorUtility.SetDirty(translationData);
            
            // Batch save assets for better performance
            AssetDatabase.SaveAssets();
        }
        
        /// <summary>
        /// Parses a potentially disambiguated term in the format "Word|context"
        /// </summary>
        /// <param name="original">The original term which may contain disambiguation</param>
        /// <param name="baseText">The base text without disambiguation</param>
        /// <param name="context">The extracted context, or null if none</param>
        private static void ParseDisambiguatedTerm(string original, out string baseText, out string context)
        {
            baseText = original;
            context = null;
            
            if (string.IsNullOrEmpty(original))
                return;
                
            int pipeIndex = original.IndexOf('|');
            if (pipeIndex > 0 && pipeIndex < original.Length - 1)
            {
                baseText = original.Substring(0, pipeIndex);
                context = original.Substring(pipeIndex + 1);
            }
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
        /// <param name="translationData">The translation data to update</param>
        /// <param name="extractorTypes">The types of extractors to use.</param>
        /// <returns>A HashSet containing all extracted text.</returns>
        public static HashSet<string> ExtractTextFromTypes(TranslationData translationData, params Type[] extractorTypes)
        {
            if (extractorTypes == null || extractorTypes.Length == 0)
            {
                Debug.LogWarning("No extractor types specified, returning empty result");
                return new HashSet<string>();
            }

            var extractedText = new HashSet<string>(StringComparer.Ordinal);
            bool anyExtractorFound = false;
            
            // Get enabled extractors of the specified types
            var targetExtractors = _extractors
                .Where(e => extractorTypes.Contains(e.GetType()) && IsExtractorEnabled(e.GetType()))
                .ToList();

            if (targetExtractors.Count == 0)
            {
                Debug.LogWarning($"No enabled extractors found matching the specified types");
                return extractedText;
            }
            
            OnExtractionStarted?.Invoke();
            ResetExtractionProgress();
            
            // Calculate per-extractor progress increment
            float progressIncrement = 1.0f / targetExtractors.Count;
            float currentProgress = 0f;
            
            // Process each extractor sequentially to avoid threading issues with Unity's API
            foreach (var extractor in targetExtractors)
            {
                SetupExtractorProgress(extractor, currentProgress, progressIncrement);
                OnExtractorStarted?.Invoke(extractor);
                
                try 
                {
                    var newText = extractor.ExtractText(Metadata);
                    
                    // Check for similar texts
                    TextSimilarityChecker.CheckForSimilarTexts(newText, $"extractor {extractor.GetType().Name}");
                    
                    extractedText.UnionWith(newText);
                    anyExtractorFound = true;
                    OnExtractorFinished?.Invoke(extractor);
                }
                catch (Exception e)
                {
                    OnExtractionError?.Invoke(extractor, e);
                    Debug.LogError($"Error in {extractor.GetType().Name}: {e.Message}\n{e.StackTrace}");
                }

                currentProgress += progressIncrement;
            }

            if (anyExtractorFound)
            {
                // Final check for similar texts across all extractors
                TextSimilarityChecker.CheckForSimilarTexts(extractedText, "all extracted text");
                OnExtractionComplete?.Invoke(extractedText);
            }
            
            // Clear the progress bar when we're done
            EditorUtility.ClearProgressBar();
            ResetExtractionProgress();
            
            return extractedText;
        }
    }
}
#endif

