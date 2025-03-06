using Translations;
using TMPro;
using UnityEngine;

public class ExampleShop : MonoBehaviour
{
    public GameObject shopTextPrefab;

    public ExampleScriptableObject exampleScriptableObject;

    private void Start()
    {
        var shopText = Instantiate(shopTextPrefab, transform);
        
        // Build format string and args based on number of shop texts
        string format = "";
        object[] args = new object[exampleScriptableObject.shopTexts.Count];
        
        for (int i = 0; i < exampleScriptableObject.shopTexts.Count; i++)
        {
            format += "{" + i + "}!";
            if (i < exampleScriptableObject.shopTexts.Count - 1)
            {
                format += " ";
            }
            args[i] = exampleScriptableObject.shopTexts[i];
        }

        shopText.GetComponent<TextMeshProUGUI>().SetTextTranslated(format, args);
    }
}
