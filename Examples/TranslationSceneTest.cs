using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using TMPro;

namespace Translations.Examples
{
    [Translated] // Extract all string fields from this class and its nested types
    public class TranslationSceneTest : MonoBehaviour
    {
        [Header("Simple String Field")]
        public string testTitle = "Test_Scene_Title";
        
        [Header("List of Strings")]
        public List<string> testCategories = new List<string>()
        {
            "Test_Scene_Category_One",
            "Test_Scene_Category_Two"
        };
        
        [Header("String Array")]
        public string[] testMenuItems = new string[]
        {
            "Test_Scene_Menu_File",
            "Test_Scene_Menu_Edit"
        };
        
        [Header("Dictionary Test")]
        public Dictionary<string, string> testMessages = new Dictionary<string, string>()
        {
            { "welcome", "Test_Scene_Welcome_Message" },
            { "goodbye", "Test_Scene_Goodbye_Message" }
        };
        
        [Header("Nested Data Test")]
        [SerializeField]
        private TestDialogueData testDialogue = new TestDialogueData()
        {
            speakerName = "Test_Scene_Speaker_Name",
            mainText = "Test_Scene_Main_Dialogue_Text",
            responseOptions = new List<string>()
            {
                "Test_Scene_Response_Option_One",
                "Test_Scene_Response_Option_Two"
            }
        };
        
        [SerializeField]
        private List<TestItemData> testItems = new List<TestItemData>()
        {
            new TestItemData()
            {
                itemName = "Test_Scene_Sword_Name",
                itemDescription = "Test_Scene_Sword_Description",
                category = "Test_Scene_Weapon_Category"
            },
            new TestItemData()
            {
                itemName = "Test_Scene_Potion_Name",
                itemDescription = "Test_Scene_Potion_Description", 
                category = "Test_Scene_Consumable_Category"
            }
        };
        
        [Header("Non-Translated Fields")]
        [NotTranslated]
        public string testID = "test_scene_001";
        [NotTranslated]
        public string internalKey = "internal_scene_test_key";
        
        [Header("Smart String Test")]
        public string testSmartString = "Test_Scene_Smart_String_With_{playerName}_Placeholder";
        
        private void Start()
        {
            RunTranslationTests();
        }
        
        private void RunTranslationTests()
        {
            // Use the static test manager to run tests
            List<string> allTestStrings = TranslationTestManager.CollectStringsFromObject(this);
            TranslationTestManager.RunTranslationTest(allTestStrings, "Scene Test Script");
            
            // Test actual translation functionality
            TranslationTestManager.TestTranslationFunctionality(testTitle, testSmartString);
        }
    }
} 