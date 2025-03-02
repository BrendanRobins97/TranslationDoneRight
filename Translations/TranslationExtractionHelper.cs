#if UNITY_EDITOR
using UnityEngine;
using System.Collections.Generic;
using System.Reflection;
using System.Collections;
using System.Linq;

namespace PSS
{
    public static class TranslationExtractionHelper
    {
        private static HashSet<object> visitedObjects = new HashSet<object>();

        public static void ExtractTranslationsFromObject(
            object obj, 
            HashSet<string> extractedText, 
            TranslationMetadata metadata, 
            string sourcePath,
            string objectPath = "",
            bool wasInactive = false,
            TextSourceType sourceType = TextSourceType.Scene)
        {
            if (obj == null) return;
            ExtractFieldsRecursive(obj, extractedText, metadata, sourcePath, objectPath, wasInactive, sourceType);
        }

        private static void ExtractFieldsRecursive(
            object obj, 
            HashSet<string> extractedText, 
            TranslationMetadata metadata, 
            string sourcePath, 
            string objectPath, 
            bool wasInactive,
            TextSourceType sourceType)
        {
            if (obj == null) return;

            // Prevent infinite recursion by tracking visited objects
            if (!visitedObjects.Add(obj)) return;

            try
            {
                var type = obj.GetType();
                
                // Skip Unity internal types to prevent potential issues
                if (type.FullName.StartsWith("UnityEngine") && type != typeof(GameObject) && type != typeof(Component))
                {
                    return;
                }

                // Skip if the type is marked with NotTranslated
                if (type.IsDefined(typeof(NotTranslatedAttribute), true))
                {
                    return;
                }

                bool shouldTranslateAll = type.IsDefined(typeof(TranslatedAttribute), true);
                var classAttribute = type.GetCustomAttributes(typeof(TranslatedAttribute), true).FirstOrDefault() as TranslatedAttribute;
                bool isRecursive = classAttribute?.RecursiveTranslation ?? true;

                // Only process fields if the class has [Translated] or we're looking at a field/property that has [Translated]
                if (shouldTranslateAll)
                {
                    FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                    foreach (FieldInfo field in fields)
                    {
                        // Skip fields marked with NotTranslated
                        if (!field.IsDefined(typeof(NotTranslatedAttribute), false))
                        {
                            ExtractTranslatableField(field, obj, extractedText, metadata, sourcePath, objectPath, wasInactive, sourceType);
                        }
                    }

                    PropertyInfo[] properties = type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                    foreach (PropertyInfo property in properties)
                    {
                        // Skip properties marked with NotTranslated
                        if (property.CanRead && !property.IsDefined(typeof(NotTranslatedAttribute), false))
                        {
                            ExtractTranslatableProperty(property, obj, extractedText, metadata, sourcePath, objectPath, wasInactive, sourceType);
                        }
                    }
                }
                else
                {
                    // If class isn't marked, only check fields/properties with [Translated]
                    FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                    foreach (FieldInfo field in fields)
                    {
                        if (field.IsDefined(typeof(TranslatedAttribute), false) && !field.IsDefined(typeof(NotTranslatedAttribute), false))
                        {
                            ExtractTranslatableField(field, obj, extractedText, metadata, sourcePath, objectPath, wasInactive, sourceType);
                        }
                        else if (isRecursive && !field.FieldType.IsPrimitive && !field.FieldType.IsEnum)
                        {
                            // Only recurse into field values that are marked with [Translated] and not marked with [NotTranslated]
                            object fieldValue = field.GetValue(obj);
                            if (fieldValue != null && 
                                fieldValue.GetType().IsDefined(typeof(TranslatedAttribute), true) && 
                                !fieldValue.GetType().IsDefined(typeof(NotTranslatedAttribute), true))
                            {
                                ExtractFieldsRecursive(fieldValue, extractedText, metadata, sourcePath, objectPath, wasInactive, sourceType);
                            }
                        }
                    }

                    PropertyInfo[] properties = type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                    foreach (PropertyInfo property in properties)
                    {
                        if (property.IsDefined(typeof(TranslatedAttribute), false) && 
                            !property.IsDefined(typeof(NotTranslatedAttribute), false) && 
                            property.CanRead)
                        {
                            ExtractTranslatableProperty(property, obj, extractedText, metadata, sourcePath, objectPath, wasInactive, sourceType);
                        }
                        else if (isRecursive && property.CanRead && !property.PropertyType.IsPrimitive && !property.PropertyType.IsEnum)
                        {
                            try
                            {
                                // Only recurse into property values that are marked with [Translated] and not marked with [NotTranslated]
                                object propertyValue = property.GetValue(obj);
                                if (propertyValue != null && 
                                    propertyValue.GetType().IsDefined(typeof(TranslatedAttribute), true) &&
                                    !propertyValue.GetType().IsDefined(typeof(NotTranslatedAttribute), true))
                                {
                                    ExtractFieldsRecursive(propertyValue, extractedText, metadata, sourcePath, objectPath, wasInactive, sourceType);
                                }
                            }
                            catch (System.Exception)
                            {
                                // Skip properties that throw exceptions when accessed
                            }
                        }
                    }
                }
            }
            finally
            {
                visitedObjects.Remove(obj);
            }
        }

        private static void ExtractTranslatableField(
            FieldInfo field, 
            object obj, 
            HashSet<string> extractedText,
            TranslationMetadata metadata, 
            string sourcePath, 
            string objectPath, 
            bool wasInactive,
            TextSourceType sourceType)
        {
            try
            {
                // For const or static fields, we don't need the instance
                object fieldValue = field.IsStatic ? field.GetValue(null) : field.GetValue(obj);
                if (fieldValue == null) return;

                if (field.FieldType == typeof(string))
                {
                    AddTranslationIfValid(fieldValue as string, obj ?? field.DeclaringType, field.Name, extractedText, metadata, sourcePath, objectPath, wasInactive, sourceType);
                }
                else if (typeof(IEnumerable<string>).IsAssignableFrom(field.FieldType))
                {
                    foreach (string str in (IEnumerable<string>)fieldValue)
                    {
                        AddTranslationIfValid(str, obj ?? field.DeclaringType, field.Name, extractedText, metadata, sourcePath, objectPath, wasInactive, sourceType);
                    }
                }
                // Add dictionary handling
                else if (field.FieldType.IsGenericType && 
                    (field.FieldType.GetGenericTypeDefinition() == typeof(Dictionary<,>) ||
                     typeof(IDictionary<,>).MakeGenericType(field.FieldType.GetGenericArguments()).IsAssignableFrom(field.FieldType)))
                {
                    var valueType = field.FieldType.GetGenericArguments()[1];
                    if (valueType == typeof(string))
                    {
                        var dictionary = fieldValue as IDictionary;
                        foreach (DictionaryEntry entry in dictionary)
                        {
                            AddTranslationIfValid(entry.Value as string, obj ?? field.DeclaringType, field.Name, extractedText, metadata, sourcePath, objectPath, wasInactive, sourceType);
                        }
                    }
                }
                else if (typeof(IEnumerable).IsAssignableFrom(field.FieldType) && field.FieldType != typeof(string))
                {
                    foreach (object item in (IEnumerable)fieldValue)
                    {
                        if (item != null)
                        {
                            ExtractFieldsRecursive(item, extractedText, metadata, sourcePath, objectPath, wasInactive, sourceType);
                        }
                    }
                }
                else if (!field.FieldType.IsPrimitive && !field.FieldType.IsEnum)
                {
                    ExtractFieldsRecursive(fieldValue, extractedText, metadata, sourcePath, objectPath, wasInactive, sourceType);
                }
            }
            catch (System.Exception)
            {
                // Skip fields that throw exceptions when accessed
            }
        }

        private static void ExtractTranslatableProperty(
            PropertyInfo property, 
            object obj, 
            HashSet<string> extractedText,
            TranslationMetadata metadata, 
            string sourcePath, 
            string objectPath, 
            bool wasInactive,
            TextSourceType sourceType)
        {
            if (!property.CanRead) return;

            try
            {
                object propertyValue = property.GetValue(obj);
                if (propertyValue == null) return;

                if (property.PropertyType == typeof(string))
                {
                    AddTranslationIfValid(propertyValue as string, obj, property.Name, extractedText, metadata, sourcePath, objectPath, wasInactive, sourceType);
                }
                else if (typeof(IEnumerable<string>).IsAssignableFrom(property.PropertyType))
                {
                    foreach (string str in (IEnumerable<string>)propertyValue)
                    {
                        AddTranslationIfValid(str, obj, property.Name, extractedText, metadata, sourcePath, objectPath, wasInactive, sourceType);
                    }
                }
                // Add dictionary handling for properties
                else if (property.PropertyType.IsGenericType && 
                    (property.PropertyType.GetGenericTypeDefinition() == typeof(Dictionary<,>) ||
                     typeof(IDictionary<,>).MakeGenericType(property.PropertyType.GetGenericArguments()).IsAssignableFrom(property.PropertyType)))
                {
                    var valueType = property.PropertyType.GetGenericArguments()[1];
                    if (valueType == typeof(string))
                    {
                        var dictionary = propertyValue as IDictionary;
                        foreach (DictionaryEntry entry in dictionary)
                        {
                            AddTranslationIfValid(entry.Value as string, obj, property.Name, extractedText, metadata, sourcePath, objectPath, wasInactive, sourceType);
                        }
                    }
                }
                else if (typeof(IEnumerable).IsAssignableFrom(property.PropertyType) && property.PropertyType != typeof(string))
                {
                    foreach (object item in (IEnumerable)propertyValue)
                    {
                        if (item != null)
                        {
                            ExtractFieldsRecursive(item, extractedText, metadata, sourcePath, objectPath, wasInactive, sourceType);
                        }
                    }
                }
                else if (!property.PropertyType.IsPrimitive && !property.PropertyType.IsEnum)
                {
                    ExtractFieldsRecursive(propertyValue, extractedText, metadata, sourcePath, objectPath, wasInactive, sourceType);
                }
            }
            catch (System.Exception)
            {
                // Skip properties that throw exceptions when accessed
            }
        }

        private static void AddTranslationIfValid(
            string text, 
            object obj, 
            string memberName, 
            HashSet<string> extractedText,
            TranslationMetadata metadata, 
            string sourcePath, 
            string objectPath, 
            bool wasInactive,
            TextSourceType sourceType)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            extractedText.Add(text);
            
            var sourceInfo = new TextSourceInfo
            {
                sourceType = sourceType,
                sourcePath = sourcePath,
                objectPath = objectPath,
                componentName = obj.GetType().Name,
                fieldName = memberName,
                wasInactive = wasInactive
            };
            metadata.AddSource(text, sourceInfo);
        }
    }
}
#endif 