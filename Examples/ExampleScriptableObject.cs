using System.Collections.Generic;
using PSS;
using UnityEngine;

[CreateAssetMenu(fileName = "ExampleScriptableObject", menuName = "ExampleScriptableObject")]
[Translated]
public class ExampleScriptableObject : ScriptableObject
{

    public string exampleString;

    public List<string> shopTexts;
}
