#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using PSS;

namespace PSS.Tests.Editor
{
    public class TestTranslationScript : MonoBehaviour
    {
        [SerializeField, Translated] 
        private string _testString1 = "Hello World";
        
        [Translated] 
        private string _testString2 = "Test String 2";
        
        private string _nonTranslatedString = "Should Not Extract";

        [SerializeField, Translated]
        private string _menuTitle = "Main Menu";

        public void TestMethod()
        {
            Debug.Log(TranslationManager.Translate("Dynamic Test String"));
            TranslationManager.Translate($"Invalid {_testString1}");
            
            // Test multi-line string
            TranslationManager.Translate(@"Multi
Line
String");
        }

        private void ShowDialog()
        {
            var text = TranslationManager.Translate("Dialog Text");
            Debug.Log(text);
        }
    }
}
#endif 