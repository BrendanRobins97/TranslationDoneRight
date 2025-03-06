using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Translations
{
    /// <summary>
    /// Example script demonstrating all supported ways to use the [Translated] attribute
    /// </summary>
    public class ExampleScript : MonoBehaviour
    {
        [Header("Simple String Examples")]
        [Tooltip("Basic string field that will be included in translations")]
        public string myString = "Hello World";

        [Translated]
        private string privateTranslatedString = "Private strings work too";

        [Translated]
        public string TranslatedProperty1 { get; set; } = "Properties are supported 123{test}1";

        [Header("Collection Examples")]
        [Tooltip("List of strings that will all be included in translations")]
        [Translated]
        public List<string> myList = new List<string> 
        { 
            "First string",
            "Second string",
            "Third string"
        };

        [Translated]
        public string[] stringArray = new string[]
        {
            "Array item 1",
            "Array item 2"
        };

        [Header("Complex Type Examples")]
        [Translated]
        public DialogueContainer dialogueContainer = new DialogueContainer();

        public void Start()
        {
            Application.targetFrameRate = 20;
            Test();
        }

        [Button]
        private void Test()
        {
            Debug.Log("Hello World Translations".TranslateString());
            Debug.Log(Translations.Translate("Blue"));
        }
    }

    /// <summary>
    /// Example of a complex type containing translatable strings
    /// </summary>
    [System.Serializable]
    public class DialogueContainer
    {
        [Translated]
        public string speakerName = "John Doe";

        [Translated]
        public List<string> dialogueLines = new List<string>
        {
            "This is the first line of dialogue",
            "This is the second line of dialogue",
            "This is the third line of dialogue"
        };
    }

    /// <summary>
    /// Another example of a complex type with nested translatable content
    /// </summary>
    [System.Serializable]
    public class QuestData
    {
        [Translated]
        public string title = "Example Quest";

        [Translated]
        public string description = "This is an example quest description";

        [Translated]
        public List<string> objectives = new List<string>
        {
            "First objective",
            "Second objective",
            "Third objective"
        };
    }
}