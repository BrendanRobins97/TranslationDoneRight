using System.Collections.Generic;
using UnityEngine;

namespace PSS
{
    [CreateAssetMenu(fileName = "LanguageData", menuName = "Localization/LanguageData")]
    public class LanguageData : ScriptableObject
    {
        public List<string> allText = new List<string>();
    }
}
