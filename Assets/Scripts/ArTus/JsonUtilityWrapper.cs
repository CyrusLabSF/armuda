using System.Collections.Generic;
using UnityEngine;

public static class JsonUtilityWrapper
{
    public static Dictionary<string, T> FromJson<T>(string json)
    {
        return JsonUtility.FromJson<SerializationWrapper<T>>(FixJson(json)).ToDictionary();
    }

    private static string FixJson(string value)
    {
        return "{\"items\":" + value + "}";
    }

    [System.Serializable]
    private class SerializationWrapper<T>
    {
        public List<KeyValue> items;

        public Dictionary<string, T> ToDictionary()
        {
            var dict = new Dictionary<string, T>();
            foreach (var kv in items)
            {
                dict[kv.key] = kv.value;
            }
            return dict;
        }

        [System.Serializable]
        public class KeyValue
        {
            public string key;
            public T value;
        }
    }
}
