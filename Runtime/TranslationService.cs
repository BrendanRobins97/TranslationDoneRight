using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;
using System;

namespace Translations
{
    /// <summary>
    /// Service responsible for handling runtime translations, parameter substitution,
    /// and language switching functionality.
    /// </summary>
    public static class TranslationService
    {
        private static bool isInitialized = false;
        private static readonly HashSet<int> processedTMPInstanceIDs = new HashSet<int>();
        private static readonly List<GameObject> batchUpdateList = new List<GameObject>(32);
        
        /// <summary>
        /// Initialize the translation service. Call this at application startup.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void Initialize()
        {
            if (isInitialized) return;
            
            SceneManager.sceneLoaded += OnSceneLoaded;
            FindAndSetupTMPs();
            isInitialized = true;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Only clear when loading single or first scene
            if (mode == LoadSceneMode.Single) 
            {
                processedTMPInstanceIDs.Clear();
            }
            FindAndSetupTMPs();
        }
        
        private static void FindAndSetupTMPs()
        {
            batchUpdateList.Clear();
            
            // Get all TMPs in the current scene - fastest version of FindObjectsOfType
            var textObjects = UnityEngine.Object.FindObjectsOfType<TextMeshProUGUI>(false);

            foreach (var textObject in textObjects)
            {
                // Skip null objects (could happen after scene unload)
                if (textObject == null) continue;
                
                int instanceID = textObject.GetInstanceID();
                
                // Skip if we've already processed this TMP
                if (processedTMPInstanceIDs.Contains(instanceID))
                {
                    continue;
                }

                // Cache component lookups
                var gameObject = textObject.gameObject;
                var notTranslated = gameObject.GetComponent<NotTranslatedTMP>();
                var translatedTMP = gameObject.GetComponent<TranslatedTMP>();

                // Skip over TMPs that are dynamic or already have TranslatedTMP
                if (notTranslated != null || translatedTMP != null)
                {
                    processedTMPInstanceIDs.Add(instanceID);
                    continue;
                }

                // Add to batch update list
                batchUpdateList.Add(gameObject);
                processedTMPInstanceIDs.Add(instanceID);
            }
            
            // Batch process component additions
            for (int i = 0; i < batchUpdateList.Count; i++)
            {
                batchUpdateList[i].AddComponent<TranslatedTMP>();
            }
        }
    }
} 