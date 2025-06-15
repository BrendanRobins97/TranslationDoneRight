using UnityEngine;
using System.Collections.Generic;
using TMPro;

namespace Translations.Examples
{
    /// <summary>
    /// Simple test script containing code patterns that ScriptTextExtractor would find.
    /// This demonstrates actual usage of translation methods in code.
    /// </summary>
    public class TranslationCodeTest : MonoBehaviour
    {
        [Header("UI Components")]
        public TextMeshProUGUI titleText;
        public TextMeshProUGUI messageText;
        public TextMeshProUGUI statusText;
        
        private void Start()
        {
            TestTranslationMethods();
            RunCodeTranslationTests();
        }
        
        private void TestTranslationMethods()
        {
            // Pattern 1: Translations.Translate("text")
            string welcomeMessage = Translations.Translate("Test_Code_Welcome_Message");
            Debug.Log("Welcome: " + welcomeMessage);
            
            // Pattern 2: "text".TranslateString()
            string buttonLabel = "Test_Code_Button_Label".TranslateString();
            Debug.Log("Button: " + buttonLabel);
            
            // Pattern 3: SetTextTranslated("text") - typically used with UI components
            if (titleText != null)
            {
                titleText.SetTextTranslated("Test_Code_Title_Text");
            }
            
            if (messageText != null)
            {
                messageText.SetTextTranslated("Test_Code_Message_Text");
            }
            
            // Pattern 4: Translations.Format("text", args)
            var playerData = new Dictionary<string, object>()
            {
                { "playerName", "TestPlayer" },
                { "level", 5 },
                { "score", 1250 }
            };
            
            string formattedText = Translations.Format("Test_Code_Player_Stats", playerData);
            Debug.Log("Stats: " + formattedText);
            
            // More complex Format usage with string arguments
            string questComplete = Translations.Format("Test_Code_Quest_Complete", "Test_Code_Reward_Gold", "Test_Code_Reward_XP");
            Debug.Log("Quest: " + questComplete);
        }
        
        private void OnValidate()
        {
            // This method might contain translation calls that run in editor
            string validationMessage = Translations.Translate("Test_Code_Validation_Message");
        }
        
        public void OnButtonClick()
        {
            // Example of translation in UI event handlers
            string clickMessage = "Test_Code_Button_Clicked".TranslateString();
            
            if (statusText != null)
            {
                statusText.SetTextTranslated("Test_Code_Status_Updated");
            }
            
            Debug.Log(clickMessage);
        }
        
        // Method that might be called to update UI dynamically
        public void UpdateUI(int currentLevel, string playerName)
        {
            // Dynamic translation with variables
            string levelUp = Translations.Translate("Test_Code_Level_Up");
            string playerGreeting = playerName.TranslateString(); // This won't work but shows the pattern
            
            // Using Format for dynamic content
            var args = new Dictionary<string, object>()
            {
                { "level", currentLevel },
                { "name", playerName }
            };
            
            string dynamicMessage = Translations.Format("Test_Code_Dynamic_Message", args);
            
            if (messageText != null)
            {
                messageText.text = dynamicMessage;
            }
        }
        
        // Static method that might contain translations
        public static string GetErrorMessage(string errorType)
        {
            switch (errorType)
            {
                case "network":
                    return Translations.Translate("Test_Code_Network_Error");
                case "save":
                    return "Test_Code_Save_Error".TranslateString();
                case "load":
                    return Translations.Translate("Test_Code_Load_Error");
                default:
                    return Translations.Translate("Test_Code_Unknown_Error");
            }
        }
        
        // Test method to run translation tests using the test manager
        [ContextMenu("Run Code Translation Tests")]
        public void RunCodeTranslationTests()
        {
            // Test the actual strings that would be extracted by ScriptTextExtractor
            var codeStrings = new List<string>()
            {
                "Test_Code_Welcome_Message",
                "Test_Code_Button_Label", 
                "Test_Code_Title_Text",
                "Test_Code_Message_Text",
                "Test_Code_Player_Stats",
                "Test_Code_Quest_Complete",
                "Test_Code_Reward_Gold",
                "Test_Code_Reward_XP",
                "Test_Code_Validation_Message",
                "Test_Code_Button_Clicked",
                "Test_Code_Status_Updated",
                "Test_Code_Level_Up",
                "Test_Code_Dynamic_Message",
                "Test_Code_Network_Error",
                "Test_Code_Save_Error",
                "Test_Code_Load_Error",
                "Test_Code_Unknown_Error"
            };
            
            TranslationTestManager.RunTranslationTest(codeStrings, "Code Pattern Test");
        }
    }
} 