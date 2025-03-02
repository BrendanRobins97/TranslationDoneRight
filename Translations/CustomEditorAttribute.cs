using System;

namespace PSS
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public class EnhancedInspectorAttribute : Attribute
    {
        public string DisplayName { get; private set; }

        public EnhancedInspectorAttribute(string displayName = "Enhanced View")
        {
            DisplayName = displayName;
        }
    }
} 