using System;
using System.Collections.Generic;
using UnityEngine;

namespace Translations
{
    [Serializable]
    public class SerializableDictionary<TKey, TValue> : Dictionary<TKey, TValue>, ISerializationCallbackReceiver
    {
        [SerializeField, HideInInspector]
        private List<TKey> keys = new List<TKey>();
        
        [SerializeField, HideInInspector]
        private List<TValue> values = new List<TValue>();
        
        // Empty constructor
        public SerializableDictionary() : base() { }
        
        // Constructor to initialize with existing dictionary
        public SerializableDictionary(Dictionary<TKey, TValue> dictionary) : base(dictionary) { }
        
        // Save the dictionary to lists before serialization
        public void OnBeforeSerialize()
        {
            keys.Clear();
            values.Clear();
            foreach(KeyValuePair<TKey, TValue> pair in this)
            {
                keys.Add(pair.Key);
                values.Add(pair.Value);
            }
        }
        
        // Load the dictionary from lists after deserialization
        public void OnAfterDeserialize()
        {
            this.Clear();

            if (keys.Count != values.Count)
            {
                Debug.LogError($"Deserialization error: key count ({keys.Count}) != value count ({values.Count})");
                return;
            }

            for(int i = 0; i < keys.Count; i++)
            {
                this[keys[i]] = values[i];
            }
        }

        // Add a new key-value pair or update an existing one
        public void AddOrUpdate(TKey key, TValue value)
        {
            if (this.ContainsKey(key))
                this[key] = value;
            else
                this.Add(key, value);
        }

        // Try to get a value by key, returning default if not found
        public TValue GetValueOrDefault(TKey key, TValue defaultValue = default)
        {
            if (this.TryGetValue(key, out TValue value))
                return value;
            return defaultValue;
        }

        // Remove a key if it exists, returning true if removed
        public bool RemoveIfExists(TKey key)
        {
            if (this.ContainsKey(key))
                return this.Remove(key);
            return false;
        }
    }
} 