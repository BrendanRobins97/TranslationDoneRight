using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Translations
{
    /// <summary>
    /// Example script demonstrating all supported ways to use the [Translated] attribute
    /// </summary>
    [Translated]
    public class TranslationExample_SimpleScript : MonoBehaviour
    {
        public string myString = "Test 1";
        private string privateTranslatedString = "Test 2";
        public const string constTranslatedString = "const string";
        public static string staticTranslatedString = "static string";
        
        [Header("Complex Type Examples")]
        public DialogueContainer dialogueContainer = new DialogueContainer();
    }
}