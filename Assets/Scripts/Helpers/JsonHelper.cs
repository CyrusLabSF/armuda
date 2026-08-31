using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;

public static class JsonHelper
{
    [Serializable]
    private class Wrapper<T>
    {
        public T[] Items;
    }

    // --- Core Methods ---

    public static T[] FromJson<T>(string json)
    {
        Wrapper<T> wrapper = JsonUtility.FromJson<Wrapper<T>>(json);
        return wrapper.Items;
    }

    public static string ToJson<T>(T[] array, bool prettyPrint = true)
    {
        Wrapper<T> wrapper = new Wrapper<T> { Items = array };
        return JsonUtility.ToJson(wrapper, prettyPrint);
    }

    // --- List<T> Support ---

    public static List<T> FromJsonList<T>(string json)
    {
        return new List<T>(FromJson<T>(json));
    }

    public static string ToJson<T>(List<T> list, bool prettyPrint = true)
    {
        return ToJson(list.ToArray(), prettyPrint);
    }

    // --- File Helpers ---

    public static T[] FromJsonFile<T>(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Debug.LogWarning($"[JsonHelper] File not found: {filePath}");
            return Array.Empty<T>();
        }

        string json = File.ReadAllText(filePath);
        return FromJson<T>(json);
    }

    public static List<T> FromJsonFileList<T>(string filePath)
    {
        return new List<T>(FromJsonFile<T>(filePath));
    }

    public static void ToJsonFile<T>(T[] array, string filePath, bool prettyPrint = true)
    {
        string json = ToJson(array, prettyPrint);
        File.WriteAllText(filePath, json);
    }

    public static void ToJsonFile<T>(List<T> list, string filePath, bool prettyPrint = true)
    {
        ToJsonFile(list.ToArray(), filePath, prettyPrint);
    }
}
