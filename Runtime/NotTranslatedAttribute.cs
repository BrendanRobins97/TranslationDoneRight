using System;
using UnityEngine;

namespace Translations
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
    public class NotTranslatedAttribute : PropertyAttribute
    {
    }
} 