using Sirenix.OdinInspector;
using UnityEngine;

namespace PSS
{
    public class ExampleScript : MonoBehaviour
    {

        [Translated]
        public string myString;

        public void Start()
        {
            Application.targetFrameRate = 20;
            Test();
        }

        [Button]
        private void Test()
        {
            Debug.Log(Translations.Translate("Blue"));
            Debug.Log(Translations.Translate("Yellow"));

            Debug.Log(Translations.Translate(myString));
        }
    }
}