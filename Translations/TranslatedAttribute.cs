using System;
using UnityEngine;

namespace PSS
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
    public class TranslatedAttribute : PropertyAttribute
    {
        public bool RecursiveTranslation { get; private set; }

        public TranslatedAttribute(bool recursiveTranslation = true)
        {
            RecursiveTranslation = recursiveTranslation;
        }
    }
}
