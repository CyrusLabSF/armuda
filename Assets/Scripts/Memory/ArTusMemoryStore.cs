using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System;
using ArTusTypes;

public static class ArTusMemoryStore
{
    public static List<MemoryEntry> memories = new();

    private static readonly string memoryPath = "D:/ArTusCloud-Deployment/UNIVERcity/System/ArTusWorkingMemory.json";

    // 🧠 Load from disk into runtime memory
    public static void LoadMemory()
    {
        if (!File.Exists(memoryPath))
        {
            Debug.LogWarning("[MemoryStore] No existing memory file found.");
            return;
        }

        try
        {
            string json = File.ReadAllText(memoryPath);
            MemoryEntryWrapper wrapper = JsonUtility.FromJson<MemoryEntryWrapper>(json);
            memories = wrapper.entries ?? new List<MemoryEntry>();
            Debug.Log($"[MemoryStore] Loaded {memories.Count} memory entries.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[MemoryStore] Failed to load memory: {ex.Message}");
        }
    }

    // 📝 Save current memory snapshot to disk
    public static void SaveMemory()
    {
        try
        {
            var wrapper = new MemoryEntryWrapper { entries = memories };
            string json = JsonUtility.ToJson(wrapper, true);
            File.WriteAllText(memoryPath, json);
            Debug.Log($"[MemoryStore] Saved {memories.Count} memory entries.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[MemoryStore] Failed to save memory: {ex.Message}");
        }
    }

    // ➕ Add a new memory to the store
    public static void AddMemory(string content, string category = "General", float score = 1f, string emotion = "neutral", string trailID = "")
    {
        var entry = new MemoryEntry
        {
            content = content,
            category = category,
            score = Mathf.Clamp01(score),
            emotion = emotion.ToLowerInvariant(),
            clarity = 1.0f,
            timestamp = DateTime.UtcNow,   // ✅ store as DateTime
            trailID = string.IsNullOrWhiteSpace(trailID)
                ? $"Trail_{category}_{DateTime.UtcNow:yyyyMMddHHmmss}" // ✅ safe string formatting here
                : trailID
        };

        memories.Add(entry);

        Debug.Log(
            $"[MemoryStore] Added memory: \"{content}\" | " +
            $"Score: {entry.score:F2}, Emotion: {entry.emotion}, " +
            $"Trail: {entry.trailID}, Time: {entry.timestamp:yyyy-MM-dd HH:mm:ss}" // ✅ format only for logging
        );
    }

    // 🧹 Full memory clear
    public static void ClearMemory()
    {
        memories.Clear();
        Debug.Log("[MemoryStore] Cleared all memory entries.");
    }

    // 🔍 Get all memories tagged with a given emotion
    public static List<MemoryEntry> GetMemoriesByEmotion(string emotion)
    {
        if (string.IsNullOrWhiteSpace(emotion)) return new();
        return memories.FindAll(m => m.emotion.Equals(emotion.ToLowerInvariant()));
    }

    // ⏳ Get most recent memories
    public static List<MemoryEntry> GetRecent(int count = 5)
    {
        if (memories.Count == 0) return new();

        int start = Mathf.Max(0, memories.Count - count);
        return memories.GetRange(start, memories.Count - start);
    }

    [Serializable]
    public class MemoryEntryWrapper
    {
        public List<MemoryEntry> entries = new();
    }
}
