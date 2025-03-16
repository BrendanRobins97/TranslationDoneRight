#if UNITY_EDITOR
using UnityEngine;
using System.Collections.Generic;
using System.Reflection;
using System.Collections;
using System.Linq;
using System;

namespace Translations
{
    public static class TranslationExtractionHelper
    {
        private static HashSet<object> visitedObjects = new HashSet<object>();

        // Reset the visited objects tracking to help with deep object hierarchies
        public static void ResetVisitedObjects()
        {
            visitedObjects.Clear();
        }

        // Generate a unique key for tracking visited objects to include the path
        // This allows the same object to be visited again in different contexts
        private static string GetVisitedObjectKey(object obj, string objectPath)
        {
            if (obj == null) return null;
            return $"{obj.GetHashCode()}:{objectPath}";
        }

        public static void ExtractTranslationsFromObject(
            object obj, 
            HashSet<string> extractedText, 
            TranslationMetadata metadata, 
            string sourcePath,
            string objectPath = "",
            TextSourceType sourceType = TextSourceType.Scene)
        {
            if (obj == null) return;
            
            // Reset visited objects when starting extraction from a new root object
            // This helps ensure collections in later objects are properly processed
            ResetVisitedObjects();
            
            ExtractFieldsRecursive(obj, extractedText, metadata, sourcePath, objectPath, sourceType);
        }

        // Helper to check if a type is a Unity internal type we should skip
        private static bool ShouldSkipUnityType(Type type)
        {
            if (type == null) return true;
            
            // Skip Unity internal types except GameObject and Component
            string fullName = type.FullName ?? "";
            if (fullName.StartsWith("UnityEngine") || fullName.StartsWith("UnityEditor"))
            {
                // Only allow specific Unity types we want to process
                return type != typeof(GameObject) && 
                       !typeof(MonoBehaviour).IsAssignableFrom(type) &&
                       type != typeof(Transform) &&
                       (type == typeof(RuntimeAnimatorController) || 
                        typeof(RuntimeAnimatorController).IsAssignableFrom(type));
            }
            
            return false;
        }

        private static bool ShouldSkipProperty(PropertyInfo property, object obj)
        {
            if (property == null) return true;

            try
            {
                // Skip properties that might cause issues
                string propertyName = property.Name;
                
                // List of known problematic Unity properties
                string[] problematicProperties = new string[] {
                    "Animation",
                    "audio",
                    "camera",
                    "light",
                    "particleSystem",
                    "rigidbody",
                    "collider",
                    "renderer",
                    "mesh",
                    "material",
                    "materials",
                    "sharedMaterial",
                    "sharedMaterials",
                    "animator",
                    "runtimeAnimatorController"
                };

                if (problematicProperties.Contains(propertyName))
                    return true;

                // Skip if property type is a Unity type we should skip
                if (ShouldSkipUnityType(property.PropertyType))
                    return true;

                // Skip if property is from a Unity component and we're processing a Component
                if (obj is Component && property.DeclaringType?.FullName?.StartsWith("UnityEngine") == true)
                    return true;

                // Skip if property has no getter or requires parameters
                if (!property.CanRead || property.GetGetMethod()?.GetParameters().Length > 0)
                    return true;

                // Skip indexer properties
                if (property.GetIndexParameters().Length > 0)
                    return true;

                // Skip if we can't safely get the getter method
                var getMethod = property.GetGetMethod();
                if (getMethod == null || !getMethod.IsPublic || getMethod.IsAbstract)
                    return true;

                // Skip if the declaring type is not accessible
                if (property.DeclaringType == null || 
                    property.DeclaringType.IsNotPublic || 
                    property.DeclaringType.IsAbstract)
                    return true;

                // Skip if property requires special runtime context
                if (property.GetCustomAttributes(true)
                           .Any(attr => attr.GetType().FullName?.Contains("RuntimeInitialize") == true))
                    return true;

                return false;
            }
            catch
            {
                // If we get any error checking the property, skip it
                return true;
            }
        }

        private static void ExtractFieldsRecursive(
            object obj, 
            HashSet<string> extractedText, 
            TranslationMetadata metadata, 
            string sourcePath, 
            string objectPath, 
            TextSourceType sourceType)
        {
            if (obj == null) return;

            try
            {
                var type = obj.GetType();

                // Skip Unity internal types to prevent potential issues
                if (ShouldSkipUnityType(type))
                {
                    return;
                }

                // Skip if the type is marked with NotTranslated
                if (type.IsDefined(typeof(NotTranslatedAttribute), true))
                {
                    return;
                }

                // Generate a unique key for this object and path combination
                string visitedKey = GetVisitedObjectKey(obj, objectPath);
                if (visitedKey != null && !visitedObjects.Add(visitedKey))
                {
                    // Already processed this object at this path
                    return;
                }

                bool shouldTranslateAll = type.IsDefined(typeof(TranslatedAttribute), true);
                var classAttribute = type.GetCustomAttributes(typeof(TranslatedAttribute), true).FirstOrDefault() as TranslatedAttribute;
                bool isRecursive = classAttribute?.RecursiveTranslation ?? true;

                try
                {
                    // Only process fields if the class has [Translated] or we're looking at a field/property that has [Translated]
                    if (shouldTranslateAll)
                    {
                        // Process all fields in the class since it's marked as translated
                        FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                        foreach (FieldInfo field in fields)
                        {
                            try
                            {
                                // Skip fields marked with NotTranslated or Unity internal types
                                if (!field.IsDefined(typeof(NotTranslatedAttribute), false))
                                {
                                    bool isUnityType = ShouldSkipUnityType(field.FieldType);
                                    if (!isUnityType)
                                    {
                                        string fieldPath = string.IsNullOrEmpty(objectPath) ? field.Name : $"{objectPath}/{field.Name}";
                                        ExtractTranslatableField(field, obj, extractedText, metadata, sourcePath, fieldPath, sourceType);
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.LogWarning($"Error processing field {field.Name}: {ex.Message}");
                                continue;
                            }
                        }

                        // Process all properties in the class since it's marked as translated
                        PropertyInfo[] properties = type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                        foreach (PropertyInfo property in properties)
                        {
                            try
                            {
                                // Skip properties marked with NotTranslated or Unity internal types
                                if (property.CanRead && !property.IsDefined(typeof(NotTranslatedAttribute), false))
                                {
                                    if (!ShouldSkipProperty(property, obj))
                                    {
                                        string propertyPath = string.IsNullOrEmpty(objectPath) ? property.Name : $"{objectPath}/{property.Name}";
                                        ExtractTranslatableProperty(property, obj, extractedText, metadata, sourcePath, propertyPath, sourceType);
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.LogWarning($"Error processing property {property.Name}: {ex.Message}");
                                continue;
                            }
                        }
                    }
                    else
                    {
                        // If class isn't marked, only check fields/properties with [Translated]
                        FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                        foreach (FieldInfo field in fields)
                        {
                            try
                            {
                                if (field.IsDefined(typeof(TranslatedAttribute), false) && 
                                    !field.IsDefined(typeof(NotTranslatedAttribute), false))
                                {
                                    bool isUnityType = ShouldSkipUnityType(field.FieldType);
                                    if (!isUnityType)
                                    {
                                        ExtractTranslatableField(field, obj, extractedText, metadata, sourcePath, objectPath, sourceType);
                                    }
                                }
                                else if (isRecursive && !field.FieldType.IsPrimitive && !field.FieldType.IsEnum)
                                {
                                    bool isUnityType = ShouldSkipUnityType(field.FieldType);
                                    if (!isUnityType)
                                    {
                                        // Only recurse into field values that are marked with [Translated] and not marked with [NotTranslated]
                                        object fieldValue = field.GetValue(obj);
                                        if (fieldValue != null && 
                                            fieldValue.GetType().IsDefined(typeof(TranslatedAttribute), true) && 
                                            !fieldValue.GetType().IsDefined(typeof(NotTranslatedAttribute), true))
                                        {
                                            ExtractFieldsRecursive(fieldValue, extractedText, metadata, sourcePath, objectPath, sourceType);
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.LogWarning($"Error processing field {field.Name}: {ex.Message}");
                                continue;
                            }
                        }

                        PropertyInfo[] properties = type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                        foreach (PropertyInfo property in properties)
                        {
                            try
                            {
                                if (property.IsDefined(typeof(TranslatedAttribute), false) && 
                                    !property.IsDefined(typeof(NotTranslatedAttribute), false) && 
                                    property.CanRead)
                                {
                                    if (!ShouldSkipProperty(property, obj))
                                    {
                                        ExtractTranslatableProperty(property, obj, extractedText, metadata, sourcePath, objectPath, sourceType);
                                    }
                                }
                                else if (isRecursive && property.CanRead && !property.PropertyType.IsPrimitive && !property.PropertyType.IsEnum)
                                {
                                    if (!ShouldSkipProperty(property, obj))
                                    {
                                        try
                                        {
                                            // Only recurse into property values that are marked with [Translated] and not marked with [NotTranslated]
                                            object propertyValue = property.GetValue(obj);
                                            if (propertyValue != null && 
                                                propertyValue.GetType().IsDefined(typeof(TranslatedAttribute), true) &&
                                                !propertyValue.GetType().IsDefined(typeof(NotTranslatedAttribute), true))
                                            {
                                                ExtractFieldsRecursive(propertyValue, extractedText, metadata, sourcePath, objectPath, sourceType);
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            Debug.LogWarning($"Error accessing property {property.Name}: {ex.Message}");
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.LogWarning($"Error processing property {property.Name}: {ex.Message}");
                                continue;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Error processing type {type.Name}: {ex.Message}");
                }
            }
            finally
            {
                string visitedKey = GetVisitedObjectKey(obj, objectPath);
                if (visitedKey != null)
                {
                    visitedObjects.Remove(visitedKey);
                }
            }
        }

        private static void ExtractTranslatableField(
            FieldInfo field, 
            object obj, 
            HashSet<string> extractedText,
            TranslationMetadata metadata, 
            string sourcePath, 
            string objectPath, 
            TextSourceType sourceType)
        {
            try
            {
                // For const or static fields, we don't need the instance
                object fieldValue = field.IsStatic ? field.GetValue(null) : field.GetValue(obj);
                if (fieldValue == null) return;

                // Debug more detailed information for complex collections
                if (fieldValue is IEnumerable && !(fieldValue is string))
                {
                    try {
                        var enumerable = fieldValue as IEnumerable;
                        int itemCount = 0;
                        foreach (var _ in enumerable)
                            itemCount++;
                    }
                    catch (System.Exception ex) {
                        Debug.LogWarning($"Error counting items in {field.Name}: {ex.Message}");
                    }
                }

                if (field.FieldType == typeof(string))
                {
                    AddTranslationIfValid(fieldValue as string, obj ?? field.DeclaringType, field.Name, extractedText, metadata, sourcePath, objectPath, sourceType);
                }
                else if (typeof(IEnumerable<string>).IsAssignableFrom(field.FieldType))
                {
                    foreach (string str in (IEnumerable<string>)fieldValue)
                    {
                        AddTranslationIfValid(str, obj ?? field.DeclaringType, field.Name, extractedText, metadata, sourcePath, objectPath, sourceType);
                    }
                }
                // Add dictionary handling
                else if (field.FieldType.IsGenericType && 
                    (field.FieldType.GetGenericTypeDefinition() == typeof(Dictionary<,>) ||
                     field.FieldType.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDictionary<,>))))
                {
                    try
                    {
                        var genericArgs = field.FieldType.GetGenericArguments();
                        if (genericArgs.Length == 2 && genericArgs[1] == typeof(string))
                        {
                            var dictionary = fieldValue as IDictionary;
                            if (dictionary != null)
                            {
                                foreach (DictionaryEntry entry in dictionary)
                                {
                                    AddTranslationIfValid(entry.Value as string, obj ?? field.DeclaringType, field.Name, extractedText, metadata, sourcePath, objectPath, sourceType);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"Error processing dictionary field {field.Name}: {ex.Message}");
                    }
                }
                else if (typeof(IEnumerable).IsAssignableFrom(field.FieldType) && field.FieldType != typeof(string))
                {
                    try
                    {
                        var enumerable = (IEnumerable)fieldValue;
                        int itemIndex = 0;
                        
                        // Get the element type of the collection
                        Type elementType = null;
                        if (field.FieldType.IsArray)
                        {
                            elementType = field.FieldType.GetElementType();
                        }
                        else if (field.FieldType.IsGenericType)
                        {
                            var genericArgs = field.FieldType.GetGenericArguments();
                            if (genericArgs.Length > 0)
                            {
                                elementType = genericArgs[0];
                            }
                        }

                        // If we couldn't get the element type from the field type, try getting it from the actual value
                        if (elementType == null && fieldValue != null)
                        {
                            var valueType = fieldValue.GetType();
                            if (valueType.IsArray)
                            {
                                elementType = valueType.GetElementType();
                            }
                            else if (valueType.IsGenericType)
                            {
                                var genericArgs = valueType.GetGenericArguments();
                                if (genericArgs.Length > 0)
                                {
                                    elementType = genericArgs[0];
                                }
                            }
                        }

                        // Check if the element type is marked with [Translated]
                        bool elementIsTranslated = elementType != null && elementType.IsDefined(typeof(TranslatedAttribute), true);
                        
                        foreach (object item in enumerable)
                        {
                            if (item != null)
                            {
                                // Track the index within the collection for better debugging
                                string itemPath = $"{objectPath}/{field.Name}[{itemIndex}]";
                                
                                // For QuestDefinition or any translated type, process all fields
                                if (elementIsTranslated)
                                {
                                    // Clear visited objects to make sure we can process this item properly
                                    visitedObjects.Clear();
                                    // Process all fields of the collection item since it's marked as translated
                                    ExtractFieldsRecursive(item, extractedText, metadata, sourcePath, itemPath, sourceType);
                                }
                                else
                                {
                                    // For non-translated types, only process if they have [Translated] fields
                                    var itemType = item.GetType();
                                    if (itemType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                                            .Any(f => f.IsDefined(typeof(TranslatedAttribute), false)))
                                    {
                                        ExtractFieldsRecursive(item, extractedText, metadata, sourcePath, itemPath, sourceType);
                                    }
                                }
                            }
                            itemIndex++;
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning($"Error processing items in collection {field.Name}: {ex.Message}\n{ex.StackTrace}");
                    }
                }
                else if (!field.FieldType.IsPrimitive && !field.FieldType.IsEnum)
                {
                    ExtractFieldsRecursive(fieldValue, extractedText, metadata, sourcePath, objectPath, sourceType);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"Error extracting field {field.Name}: {ex.Message}");
            }
        }

        private static void ExtractTranslatableProperty(
            PropertyInfo property, 
            object obj, 
            HashSet<string> extractedText,
            TranslationMetadata metadata, 
            string sourcePath, 
            string objectPath, 
            TextSourceType sourceType)
        {
            if (!property.CanRead || ShouldSkipProperty(property, obj)) return;

            try
            {
                object propertyValue = null;
                try
                {
                    propertyValue = property.GetValue(obj);
                }
                catch (Exception)
                {
                    // If we can't get the property value, skip it
                    return;
                }

                if (propertyValue == null) return;

                // Handle collections
                if (propertyValue is IEnumerable && !(propertyValue is string))
                {
                    try {
                        var enumerable = propertyValue as IEnumerable;
                        int itemCount = 0;
                        foreach (var _ in enumerable)
                            itemCount++;
                        
                        // Reset visited objects when processing a collection to ensure we can process all items
                        visitedObjects.Clear();
                    }
                    catch (System.Exception ex) {
                        Debug.LogWarning($"Error counting items in property {property.Name}: {ex.Message}");
                    }
                }

                if (property.PropertyType == typeof(string))
                {
                    AddTranslationIfValid(propertyValue as string, obj, property.Name, extractedText, metadata, sourcePath, objectPath, sourceType);
                }
                else if (typeof(IEnumerable<string>).IsAssignableFrom(property.PropertyType))
                {
                    foreach (string str in (IEnumerable<string>)propertyValue)
                    {
                        AddTranslationIfValid(str, obj, property.Name, extractedText, metadata, sourcePath, objectPath, sourceType);
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
                            AddTranslationIfValid(entry.Value as string, obj, property.Name, extractedText, metadata, sourcePath, objectPath, sourceType);
                        }
                    }
                }
                else if (typeof(IEnumerable).IsAssignableFrom(property.PropertyType) && property.PropertyType != typeof(string))
                {
                    try
                    {
                        var enumerable = (IEnumerable)propertyValue;
                        int itemIndex = 0;
                        
                        // Get the element type of the collection
                        Type elementType = null;
                        if (property.PropertyType.IsArray)
                        {
                            elementType = property.PropertyType.GetElementType();
                        }
                        else if (property.PropertyType.IsGenericType)
                        {
                            elementType = property.PropertyType.GetGenericArguments()[0];
                        }

                        // Check if the element type is marked with [Translated]
                        bool elementIsTranslated = elementType != null && elementType.IsDefined(typeof(TranslatedAttribute), true);
                        
                        foreach (object item in enumerable)
                        {
                            if (item != null)
                            {
                                // Track the index within the collection for better debugging
                                string itemPath = $"{objectPath}/{property.Name}[{itemIndex}]";
                                
                                // For QuestDefinition or any translated type, process all fields
                                if (elementIsTranslated)
                                {
                                    // Clear visited objects to make sure we can process this item properly
                                    visitedObjects.Clear();
                                    // Process all fields of the collection item since it's marked as translated
                                    ExtractFieldsRecursive(item, extractedText, metadata, sourcePath, itemPath, sourceType);
                                }
                                else
                                {
                                    // For non-translated types, only process if they have [Translated] fields
                                    var itemType = item.GetType();
                                    if (itemType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                                            .Any(f => f.IsDefined(typeof(TranslatedAttribute), false)))
                                    {
                                        ExtractFieldsRecursive(item, extractedText, metadata, sourcePath, itemPath, sourceType);
                                    }
                                }
                            }
                            itemIndex++;
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning($"Error processing items in collection property {property.Name}: {ex.Message}\n{ex.StackTrace}");
                    }
                }
                else if (!property.PropertyType.IsPrimitive && !property.PropertyType.IsEnum)
                {
                    ExtractFieldsRecursive(propertyValue, extractedText, metadata, sourcePath, objectPath, sourceType);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"Error extracting property {property.Name}: {ex.Message}");
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
            };
            metadata.AddSource(text, sourceInfo);
        }
    }
}
#endif 