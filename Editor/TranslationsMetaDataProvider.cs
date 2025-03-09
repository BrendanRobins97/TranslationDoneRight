#if UNITY_EDITOR
using UnityEngine;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;

namespace Translations
{
    public static class TranslationMetaDataProvider
    {
        // Path to the main translation data and metadata assets
        private static string DataPath => $"{TranslationPaths.DataFolder}/TranslationData.asset";
        private static string MetadataPath => $"{TranslationPaths.DataFolder}/TranslationMetadata.asset";
        
        // Addressable key for the TranslationData asset
        private const string TRANSLATION_DATA_KEY = "TranslationData";
        
        // This will store the loaded TranslationData asset
        private static TranslationData cachedTranslationData;
        
        // This will store the loaded TranslationMetadata asset
        private static TranslationMetadata cachedTranslationMetadata;
        
        // Property to access the TranslationMetadata asset
        public static TranslationMetadata Metadata
        {
            get
            {
                return LoadOrCreateTranslationMetadata();
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
        /// Load the TranslationMetadata asset from the data folder or create a new one
        /// </summary>
        /// <returns>The existing or newly created TranslationMetadata asset</returns>
        private static TranslationMetadata LoadOrCreateTranslationMetadata()
        {
            // Return cached instance if available
            if (cachedTranslationMetadata != null)
                return cachedTranslationMetadata;
            
            // Try to load the metadata from the data folder
            var metadata = AssetDatabase.LoadAssetAtPath<TranslationMetadata>(MetadataPath);
            
            // Create if it doesn't exist
            if (metadata == null)
            {
                metadata = ScriptableObject.CreateInstance<TranslationMetadata>();
                EnsureDirectoryExists(TranslationPaths.DataFolder);
                AssetDatabase.CreateAsset(metadata, MetadataPath);
                AssetDatabase.SaveAssets();
            }
            
            cachedTranslationMetadata = metadata;
            return metadata;
        }

        /// <summary>
        /// Load the TranslationData asset from its custom location
        /// </summary>
        /// <returns>The TranslationData asset, or null if it doesn't exist</returns>
        private static TranslationData LoadTranslationData()
        {
            return AssetDatabase.LoadAssetAtPath<TranslationData>(DataPath);
        }

        /// <summary>
        /// Create a new TranslationData asset at the specified location
        /// </summary>
        /// <returns>The newly created TranslationData asset</returns>
        private static TranslationData CreateTranslationData()
        {
            TranslationData translationData = ScriptableObject.CreateInstance<TranslationData>();
            
            // Create the necessary folder structure
            EnsureDirectoryExists(TranslationPaths.DataFolder);
            
            // Create the language folder if it doesn't exist
            EnsureDirectoryExists(TranslationPaths.LanguagesFolder);
            
            // Create the asset
            AssetDatabase.CreateAsset(translationData, DataPath);
            AssetDatabase.SaveAssets();
            
            // Add the asset to Addressables for runtime access
            AddAssetToAddressables(DataPath, TRANSLATION_DATA_KEY);
            
            AssetDatabase.Refresh();
            
            Debug.Log($"Created new TranslationData asset at: {DataPath}");
            return translationData;
        }

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
        /// Add a language data asset to Addressables
        /// </summary>
        /// <param name="languageCode">The language code</param>
        /// <param name="assetPath">The path to the asset</param>
        public static void AddLanguageDataToAddressables(string languageCode, string assetPath)
        {
            AddAssetToAddressables(assetPath, languageCode);
        }
    }
}
#endif 