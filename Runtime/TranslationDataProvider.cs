using UnityEngine;
using System.IO;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
#endif

namespace Translations
{
    public static class TranslationDataProvider
    {
        // Base folder where all translation assets will be stored
        private const string DEFAULT_BASE_FOLDER = "Assets/Translations";
        
        // Subfolders for organizing different types of assets
        private const string DATA_SUBFOLDER = "Data";
        private const string LANGUAGES_SUBFOLDER = "Languages";
        
        // Expose the default folders for UI
        public static string DefaultBaseFolder => DEFAULT_BASE_FOLDER;
        public static string DefaultDataFolder => $"{DEFAULT_BASE_FOLDER}/{DATA_SUBFOLDER}";
        public static string DefaultLanguagesFolder => $"{DEFAULT_BASE_FOLDER}/{LANGUAGES_SUBFOLDER}";
        // The actual base folder which can be changed via the UI
        private static string currentBaseFolder = null;
        
        // Property to get the current base folder
        public static string BaseFolder 
        {
            get 
            {
                if (currentBaseFolder == null)
                {
                    #if UNITY_EDITOR
                    currentBaseFolder = EditorPrefs.GetString("TranslationManager_BasePath", DEFAULT_BASE_FOLDER);
                    #else
                    currentBaseFolder = DEFAULT_BASE_FOLDER;
                    #endif
                }
                return currentBaseFolder;
            }
        }
        
        // Property to get the data folder (where TranslationData and MetaData are stored)
        public static string DataFolder => $"{BaseFolder}/{DATA_SUBFOLDER}";
        
        // Property to get the languages folder (where LanguageData assets are stored)
        public static string LanguagesFolder => $"{BaseFolder}/{LANGUAGES_SUBFOLDER}";
        
        // Path to the main translation data asset
        private static string DataPath => $"{DataFolder}/TranslationData.asset";
        
        // Addressable key for the TranslationData asset
        private const string TRANSLATION_DATA_KEY = "TranslationData";
        
        // This will store the loaded TranslationData asset
        private static TranslationData cachedTranslationData;
        
        // Flag to track if Addressables initialization has been attempted
        private static bool addressablesInitialized = false;
        
        /// <summary>
        /// Set the base folder to a new location
        /// </summary>
        /// <param name="newFolder">The new base folder path relative to Assets</param>
        public static void SetDataFolder(string newFolder)
        {
            if (string.IsNullOrEmpty(newFolder) || newFolder == currentBaseFolder)
                return;
                
            currentBaseFolder = newFolder;
            #if UNITY_EDITOR
            EditorPrefs.SetString("TranslationManager_BasePath", newFolder);
            #endif
            
            // Clear the cache to force reload from the new location
            cachedTranslationData = null;
        }
        
        // Property to access the TranslationData asset
        public static TranslationData Data
        {
            get
            {
                return LoadOrCreateTranslationData();
            }
        }

        /// <summary>
        /// Load the TranslationData asset from its custom location
        /// </summary>
        /// <returns>The existing or newly created TranslationData asset</returns>
        private static TranslationData LoadOrCreateTranslationData()
        {
            // Return cached instance if available
            if (cachedTranslationData != null)
                return cachedTranslationData;
            
            var translationData = LoadTranslationData();
            if (translationData == null)
            {
                translationData = CreateTranslationData();
            }
            
            cachedTranslationData = translationData;
            return translationData;
        }

        /// <summary>
        /// Load the TranslationData asset from its custom location
        /// </summary>
        /// <returns>The TranslationData asset, or null if it doesn't exist</returns>
        private static TranslationData LoadTranslationData()
        {
#if UNITY_EDITOR
            // Load from the location
            return AssetDatabase.LoadAssetAtPath<TranslationData>(DataPath);
#else
            // Initialize Addressables if it hasn't been initialized yet
            if (!addressablesInitialized)
            {
                InitializeAddressables();
            }
            
            // For runtime, we'll use Addressables to load the asset
            // This is a synchronous loading approach, which is acceptable for this core asset
            // that needs to be available immediately
            try
            {
                AsyncOperationHandle<TranslationData> handle = Addressables.LoadAssetAsync<TranslationData>(TRANSLATION_DATA_KEY);
                handle.WaitForCompletion();
                
                if (handle.Status == AsyncOperationStatus.Succeeded)
                {
                    return handle.Result;
                }
                else
                {
                    Debug.LogError($"Failed to load TranslationData asset via Addressables: {handle.OperationException}");
                    return null;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Exception when loading TranslationData asset via Addressables: {e.Message}");
                return null;
            }
#endif
        }

#if !UNITY_EDITOR
        /// <summary>
        /// Initialize the Addressables system
        /// </summary>
        private static void InitializeAddressables()
        {
            try
            {
                Addressables.InitializeAsync().WaitForCompletion();
                addressablesInitialized = true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to initialize Addressables: {e.Message}");
            }
        }
#endif

        /// <summary>
        /// Create a new TranslationData asset at the specified location
        /// </summary>
        /// <returns>The newly created TranslationData asset</returns>
        private static TranslationData CreateTranslationData()
        {
#if UNITY_EDITOR
            TranslationData translationData = ScriptableObject.CreateInstance<TranslationData>();
            
            // Create the necessary folder structure
            EnsureDirectoryExists(DataFolder);
            
            // Create the language folder if it doesn't exist
            EnsureDirectoryExists(LanguagesFolder);
            
            // Create the asset
            AssetDatabase.CreateAsset(translationData, DataPath);
            AssetDatabase.SaveAssets();
            
            // Add the asset to Addressables for runtime access
            AddAssetToAddressables(DataPath, TRANSLATION_DATA_KEY);
            
            AssetDatabase.Refresh();
            
            Debug.Log($"Created new TranslationData asset at: {DataPath}");
            return translationData;
#else
            Debug.LogError("Cannot create TranslationData asset at runtime");
            return null;
#endif
        }

#if UNITY_EDITOR
        /// <summary>
        /// Ensure that the directory exists, creating it if necessary
        /// </summary>
        /// <param name="directory">The directory path to ensure exists</param>
        private static void EnsureDirectoryExists(string directory)
        {
            if (!AssetDatabase.IsValidFolder(directory))
            {
                string parentFolder = Path.GetDirectoryName(directory);
                string folderName = Path.GetFileName(directory);
                
                // Make sure the parent directory exists
                EnsureDirectoryExists(parentFolder);
                
                // Create the directory
                AssetDatabase.CreateFolder(parentFolder, folderName);
            }
        }
        
        /// <summary>
        /// Add an asset to the Addressables system
        /// </summary>
        /// <param name="assetPath">The path to the asset</param>
        /// <param name="addressableKey">The addressable key to use</param>
        private static void AddAssetToAddressables(string assetPath, string addressableKey)
        {
            // Get the settings
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                Debug.LogError("Addressables not initialized in the project. Please set up Addressables first.");
                return;
            }
            
            // Get the default group
            AddressableAssetGroup defaultGroup = settings.DefaultGroup;
            if (defaultGroup == null)
            {
                Debug.LogError("No default Addressables group found.");
                return;
            }
            
            // Create entry if it doesn't exist
            var guid = AssetDatabase.AssetPathToGUID(assetPath);
            var entry = settings.CreateOrMoveEntry(guid, defaultGroup);
            
            // Set the address
            entry.address = addressableKey;
            
            // Mark settings dirty
            EditorUtility.SetDirty(settings);
        }
        
        /// <summary>
        /// Get the path for a language data asset
        /// </summary>
        /// <param name="languageCode">The language code</param>
        /// <returns>The path where the language data asset should be stored</returns>
        public static string GetLanguageDataPath(string languageCode)
        {
            return $"{LanguagesFolder}/{languageCode}.asset";
        }
        
        /// <summary>
        /// Add a language data asset to Addressables
        /// </summary>
        /// <param name="languageCode">The language code</param>
        /// <param name="assetPath">The path to the asset</param>
        public static void AddLanguageDataToAddressables(string languageCode, string assetPath)
        {
            AddAssetToAddressables(assetPath, languageCode);
        }
#endif
    }
} 