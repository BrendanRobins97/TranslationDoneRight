using UnityEngine;
using System;
using System.Collections.Generic;

namespace PSS
{
    [Serializable]
    public class SerializableDictionary<TKey, TValue> : Dictionary<TKey, TValue>, ISerializationCallbackReceiver
    {
        [SerializeField]
        private List<TKey> keys = new List<TKey>();
        
        [SerializeField]
        private List<TValue> values = new List<TValue>();
        
        // Save the dictionary to lists
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
        
        // Load dictionary from lists
        public void OnAfterDeserialize()
        {
            this.Clear();

            if (keys.Count != values.Count)
            {
                Debug.LogError($"SerializableDictionary deserialization error: key count ({keys.Count}) != value count ({values.Count})");
                return;
            }

            for(int i = 0; i < keys.Count; i++)
            {
                this.Add(keys[i], values[i]);
            }
        }
    }
} 