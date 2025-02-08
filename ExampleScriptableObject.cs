using System.Collections;
using System.Collections.Generic;
using PSS;
using UnityEngine;

[CreateAssetMenu(fileName = "ExampleScriptableObject", menuName = "ExampleScriptableObject")]
public class ExampleScriptableObject : ScriptableObject
{

    [Translated]
    public string exampleString;
}
