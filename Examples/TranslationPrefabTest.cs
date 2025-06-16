using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using TMPro;

namespace Translations.Examples
{
    [Translated] // Extract all string fields from this class and its nested types
    public class TranslationPrefabTest : MonoBehaviour
    {
        [Header("Simple String Field")]
        public string testTitle = "Test_Prefab_Title";
        
        [Header("List of Strings")]
        public List<string> testCategories = new List<string>()
        {
            "Test_Prefab_Category_One",
            "Test_Prefab_Category_Two"
        };
        
        [Header("String Array")]
        public string[] testMenuItems = new string[]
        {
            "Test_Prefab_Menu_File",
            "Test_Prefab_Menu_Edit"
        };
        
        [Header("Dictionary Test")]
        public Dictionary<string, string> testMessages = new Dictionary<string, string>()
        {
            { "welcome", "Test_Prefab_Welcome_Message" },
            { "goodbye", "Test_Prefab_Goodbye_Message" }
        };
        
        [Header("Nested Data Test")]
        [SerializeField]
        private TestDialogueData testDialogue = new TestDialogueData()
        {
            speakerName = "Test_Prefab_Speaker_Name",
            mainText = "Test_Prefab_Main_Dialogue_Text",
            responseOptions = new List<string>()
            {
                "Test_Prefab_Response_Option_One",
                "Test_Prefab_Response_Option_Two"
            }
        };
        
        [SerializeField]
        private List<TestItemData> testItems = new List<TestItemData>()
        {
            new TestItemData()
            {
                itemName = "Test_Prefab_Sword_Name",
                itemDescription = "Test_Prefab_Sword_Description",
                category = "Test_Prefab_Weapon_Category"
            },
            new TestItemData()
            {
                itemName = "Test_Prefab_Potion_Name",
                itemDescription = "Test_Prefab_Potion_Description", 
                category = "Test_Prefab_Consumable_Category"
            }
        };
        
        [Header("Non-Translated Fields")]
        [NotTranslated]
        public string testID = "test_prefab_001";
        [NotTranslated]
        public string internalKey = "internal_prefab_test_key";
        
        [Header("Smart String Test")]
        public string testSmartString = "Test_Prefab_Smart_String_With_{playerName}_Placeholder";
        
        [Header("Prefab-Specific Test")]
        public string prefabInstanceID = "Test_Prefab_Instance_ID";
        public string prefabAssetPath = "Test_Prefab_Asset_Path";
        

        private void Start()
        {
            RunTranslationTests();
        }

        public void RunTranslationTests()
        {
            // Use the static test manager to run tests
            List<string> allTestStrings = TranslationTestManager.CollectStringsFromObject(this);
            TranslationTestManager.RunTranslationTest(allTestStrings, $"Prefab Test ({gameObject.name})");
            
            // Test actual translation functionality
            TranslationTestManager.TestTranslationFunctionality(testTitle, testSmartString);
        }
    }
} 