#if UNITY_EDITOR
using UnityEditor;

namespace Translations
{
    public static class TranslationPaths
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
                    currentBaseFolder = EditorPrefs.GetString("TranslationManager_BasePath", DEFAULT_BASE_FOLDER);
                }
                return currentBaseFolder;
            }
        }
        
        // Property to get the data folder (where TranslationData and MetaData are stored)
        public static string DataFolder => $"{BaseFolder}/{DATA_SUBFOLDER}";
        
        // Property to get the languages folder (where LanguageData assets are stored)
        public static string LanguagesFolder => $"{BaseFolder}/{LANGUAGES_SUBFOLDER}";

        /// <summary>
        /// Set the base folder to a new location
        /// </summary>
        /// <param name="newFolder">The new base folder path relative to Assets</param>
        public static void SetDataFolder(string newFolder)
        {
            if (string.IsNullOrEmpty(newFolder) || newFolder == currentBaseFolder)
                return;
                
            currentBaseFolder = newFolder;
            EditorPrefs.SetString("TranslationManager_BasePath", newFolder);
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
    }
}
#endif 