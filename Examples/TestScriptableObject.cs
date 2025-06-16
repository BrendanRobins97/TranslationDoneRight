using UnityEngine;
using System.Collections.Generic;

namespace Translations.Examples
{
    [CreateAssetMenu(fileName = "TestScriptableObject", menuName = "Translations/Test ScriptableObject")]
    [Translated] // Extract all string fields from this class and its nested types
    public class TestScriptableObject : ScriptableObject
    {
        [Header("Simple String Field")]
        public string testTitle = "Test_SO_Title";
        
        [Header("List of Strings")]
        public List<string> testCategories = new List<string>()
        {
            "Test_SO_Category_One",
            "Test_SO_Category_Two"
        };
        
        [Header("String Array")]
        public string[] testMenuItems = new string[]
        {
            "Test_SO_Menu_File",
            "Test_SO_Menu_Edit"
        };
        
        [Header("Dictionary Test")]
        public Dictionary<string, string> testMessages = new Dictionary<string, string>()
        {
            { "welcome", "Test_SO_Welcome_Message" },
            { "goodbye", "Test_SO_Goodbye_Message" }
        };
        
        [Header("Nested Data Test")]
        [SerializeField]
        private TestSODialogueData testDialogue = new TestSODialogueData()
        {
            speakerName = "Test_SO_Speaker_Name",
            mainText = "Test_SO_Main_Dialogue_Text",
            responseOptions = new List<string>()
            {
                "Test_SO_Response_Option_One",
                "Test_SO_Response_Option_Two"
            }
        };
        
        [SerializeField]
        private List<TestSOItemData> testItems = new List<TestSOItemData>()
        {
            new TestSOItemData()
            {
                itemName = "Test_SO_Sword_Name",
                itemDescription = "Test_SO_Sword_Description",
                category = "Test_SO_Weapon_Category"
            },
            new TestSOItemData()
            {
                itemName = "Test_SO_Potion_Name",
                itemDescription = "Test_SO_Potion_Description", 
                category = "Test_SO_Consumable_Category"
            }
        };
        
        [Header("Non-Translated Fields")]
        [NotTranslated]
        public string testID = "test_so_001";
        [NotTranslated]
        public string internalKey = "internal_so_test_key";
        
        [Header("Smart String Test")]
        public string testSmartString = "Test_SO_Smart_String_With_{playerName}_Placeholder";
        
        // Method to run translation tests on this ScriptableObject
        [ContextMenu("Run Translation Tests")]
        public void RunTranslationTests()
        {
            List<string> allTestStrings = TranslationTestManager.CollectStringsFromObject(this);
            TranslationTestManager.RunTranslationTest(allTestStrings, $"ScriptableObject Test ({name})");
            
            // Test actual translation functionality
            TranslationTestManager.TestTranslationFunctionality(testTitle, testSmartString);
        }
    }
    
    [System.Serializable]
    [Translated]
    public class TestSODialogueData
    {
        public string speakerName;
        public string mainText;
        public List<string> responseOptions = new List<string>();
        
        [NotTranslated]
        public int dialogueID;
        [NotTranslated]
        public bool isCompleted;
    }
    
    [System.Serializable]
    [Translated]
    public class TestSOItemData
    {
        public string itemName;
        public string itemDescription;
        public string category;
        
        [NotTranslated]
        public int itemID;
        [NotTranslated]
        public float value;
        [NotTranslated]
        public bool isRare;
    }
} 