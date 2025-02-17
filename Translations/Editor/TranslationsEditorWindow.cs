using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace PSS
{
    public partial class TranslationsEditorWindow : EditorWindow
    {
        private Vector2 scrollPosition;
        private TranslationData translationData;
        private string newLanguageName = "";
        private string searchFilter = "";
        private bool showMissingOnly = false;
        private bool showUnusedOnly = false;
        private Vector2 textScrollPosition;
        private string selectedKey = null;
        private bool autoTranslateEnabled = false;
        private string apiKey = "";
        private bool includeInactive = false;
        private bool showSimilaritySettings = false;
        private bool showCategoryManagement = false;
        private string newCategoryName = "";
        
        private Tab currentTab = Tab.Languages;

        private Vector2 gridScrollPosition;
        private float gridViewScale = 1f;

        // Add coverage tracking
        private Dictionary<string, float> languageCoverage = new Dictionary<string, float>();
        private bool needsCoverageUpdate = true;

        private KeyUpdateMode updateMode = KeyUpdateMode.Replace;

        private bool isDirty = false;
        private float saveDelay = 1f;
        private double lastEditTime;

        private string deeplApiKey = "";
        private bool useDeepLPro = false;
        private bool includeContextInTranslation = true;
        private bool preserveFormatting = true;
        private bool formalityPreference = true;

        [MenuItem("Window/Translations")]
        public static void ShowWindow()
        {
            GetWindow<TranslationsEditorWindow>("Translations Manager");
        }

        private void OnEnable()
        {
            translationData = Resources.Load<TranslationData>("TranslationData");
            LoadEditorPrefs();
            needsCoverageUpdate = true;

            // Subscribe to TextExtractor events
            TextExtractor.OnExtractionStarted += HandleExtractionStarted;
            TextExtractor.OnExtractionComplete += HandleExtractionComplete;
            TextExtractor.OnExtractionError += HandleExtractionError;
            TextExtractor.OnExtractorStarted += HandleExtractorStarted;
            TextExtractor.OnExtractorFinished += HandleExtractorFinished;
        }

        private void OnDisable()
        {
            SaveEditorPrefs();

            // Unsubscribe from TextExtractor events
            TextExtractor.OnExtractionStarted -= HandleExtractionStarted;
            TextExtractor.OnExtractionComplete -= HandleExtractionComplete;
            TextExtractor.OnExtractionError -= HandleExtractionError;
            TextExtractor.OnExtractorStarted -= HandleExtractorStarted;
            TextExtractor.OnExtractorFinished -= HandleExtractorFinished;
        }

        // Event handlers for TextExtractor
        private void HandleExtractionStarted()
        {
            EditorUtility.DisplayProgressBar("Extracting Text", "Starting extraction...", 0f);
            TextExtractor.Metadata = translationData?.Metadata;
        }

        private void HandleExtractionComplete(HashSet<string> extractedText)
        {
            EditorUtility.ClearProgressBar();
            needsCoverageUpdate = true;
            Repaint();
        }

        private void HandleExtractionError(ITextExtractor extractor, System.Exception error)
        {
            Debug.LogError($"Error in {extractor.GetType().Name}: {error.Message}\n{error.StackTrace}");
        }

        private void HandleExtractorStarted(ITextExtractor extractor)
        {
            EditorUtility.DisplayProgressBar("Extracting Text", $"Running {extractor.GetType().Name}...", 0.5f);
        }

        private void HandleExtractorFinished(ITextExtractor extractor)
        {
            // Could be used for progress tracking if needed
        }

        private void LoadEditorPrefs()
        {
            autoTranslateEnabled = EditorPrefs.GetBool("TranslationManager_AutoTranslate", false);
            apiKey = EditorPrefs.GetString("TranslationManager_APIKey", "");
            gridViewScale = EditorPrefs.GetFloat("TranslationManager_GridScale", 1f);
            deeplApiKey = EditorPrefs.GetString("TranslationManager_DeepLApiKey", "");
            useDeepLPro = EditorPrefs.GetBool("TranslationManager_UseDeepLPro", false);
            includeContextInTranslation = EditorPrefs.GetBool("TranslationManager_IncludeContext", true);
            preserveFormatting = EditorPrefs.GetBool("TranslationManager_PreserveFormatting", true);
            formalityPreference = EditorPrefs.GetBool("TranslationManager_Formality", true);
        }

        private void SaveEditorPrefs()
        {
            EditorPrefs.SetBool("TranslationManager_AutoTranslate", autoTranslateEnabled);
            EditorPrefs.SetString("TranslationManager_APIKey", apiKey);
            EditorPrefs.SetFloat("TranslationManager_GridScale", gridViewScale);
            EditorPrefs.SetString("TranslationManager_DeepLApiKey", deeplApiKey);
            EditorPrefs.SetBool("TranslationManager_UseDeepLPro", useDeepLPro);
            EditorPrefs.SetBool("TranslationManager_IncludeContext", includeContextInTranslation);
            EditorPrefs.SetBool("TranslationManager_PreserveFormatting", preserveFormatting);
            EditorPrefs.SetBool("TranslationManager_Formality", formalityPreference);
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);

            if (translationData == null)
            {
                DrawNoTranslationDataUI();
                return;
            }

            DrawHeaderSection();
            EditorGUILayout.Space(5);

            // Draw tabs with icons using reliable built-in Unity icons
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Toggle(currentTab == Tab.Settings, new GUIContent(" Settings", EditorGUIUtility.IconContent("d_Settings").image), EditorStyles.toolbarButton))
                currentTab = Tab.Settings;
            if (GUILayout.Toggle(currentTab == Tab.TextExtraction, new GUIContent(" Text Extraction", EditorGUIUtility.IconContent("d_Prefab Icon").image), EditorStyles.toolbarButton))
                currentTab = Tab.TextExtraction;
            if (GUILayout.Toggle(currentTab == Tab.AllText, new GUIContent(" All Text", EditorGUIUtility.IconContent("d_TextAsset Icon").image), EditorStyles.toolbarButton))
                currentTab = Tab.AllText;
            if (GUILayout.Toggle(currentTab == Tab.Languages, new GUIContent(" Languages", EditorGUIUtility.IconContent("d_BuildSettings.Standalone").image), EditorStyles.toolbarButton))
                currentTab = Tab.Languages;
            if (GUILayout.Toggle(currentTab == Tab.DeepL, new GUIContent(" DeepL", EditorGUIUtility.IconContent("d_BuildSettings.Web.Small").image), EditorStyles.toolbarButton))
                currentTab = Tab.DeepL;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            
            switch (currentTab)
            {
                case Tab.Languages:
                    DrawLanguagesTab();
                    break;
                case Tab.AllText:
                    DrawAllTextTab();
                    break;
                case Tab.TextExtraction:
                    DrawTextExtractionTab();
                    break;
                case Tab.Settings:
                    DrawSettingsTab();
                    break;
                case Tab.DeepL:
                    DrawDeepLTab();
                    break;
            }

            EditorGUILayout.EndScrollView();

            // Status bar
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            EditorGUILayout.LabelField($"Total Keys: {translationData.allKeys.Count}", EditorStyles.miniLabel);
            EditorGUILayout.LabelField($"Languages: {translationData.supportedLanguages.Count}", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawNoTranslationDataUI()
        {
            EditorGUILayout.HelpBox("No TranslationData asset found in Resources folder.", MessageType.Warning);
            
            if (GUILayout.Button("Create TranslationData Asset"))
            {
                CreateTranslationDataAsset();
            }
        }

        private void DrawHeaderSection()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Translation Manager", EditorStyles.boldLabel);
            
            if (GUILayout.Button("Select TranslationData Asset", GUILayout.Width(180)))
            {
                Selection.activeObject = translationData;
            }
            EditorGUILayout.EndHorizontal();
        }

        private void CreateTranslationDataAsset()
        {
            translationData = ScriptableObject.CreateInstance<TranslationData>();
            
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            {
                AssetDatabase.CreateFolder("Assets", "Resources");
            }
            
            AssetDatabase.CreateAsset(translationData, "Assets/Resources/TranslationData.asset");
            AssetDatabase.SaveAssets();
            Selection.activeObject = translationData;
            
            Debug.Log("Created new TranslationData asset in Resources folder");
        }

        private void DrawProgressBar(float value)
        {
            Rect rect = GUILayoutUtility.GetRect(50, 18);
            EditorGUI.ProgressBar(rect, value, "");
        }

        private void OnInspectorUpdate()
        {
            // Trigger repaint to check for delayed save
            if (isDirty)
            {
                Repaint();
            }
        }
    }
} 