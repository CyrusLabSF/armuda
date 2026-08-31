using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public class ArTusPatternEngine : MonoBehaviour
{
    // =========================================================
    // INTERNAL DATA STRUCTURES
    // =========================================================

    [Serializable]
    public class PatternEntry
    {
        public string patternKey;
        public int occurrences;
        public float confidence;
        public string lastObserved;
        public List<string> supportingMemories = new();
    }

    [Serializable]
    private class PatternWrapper
    {
        public List<PatternEntry> patterns = new();
    }

    // =========================================================
    // STATE
    // =========================================================

    private Dictionary<string, PatternEntry> patternMap = new();

    private string patternSavePath;

    // =========================================================
    // UNITY LIFECYCLE
    // =========================================================

    private void Awake()
    {
        patternSavePath =
            ArTusPathUtility.GetPersistent(
                "UNIVERcity/Patterns/PatternMap.json"
            );

        LoadPatterns();
    }

    // =========================================================
    // PATTERN INGESTION
    // =========================================================

    public void ObserveMemory(string content, string emotion, float score)
    {
        if (string.IsNullOrWhiteSpace(content))
            return;

        string key = GeneratePatternKey(content);

        if (!patternMap.ContainsKey(key))
        {
            patternMap[key] = new PatternEntry
            {
                patternKey = key,
                occurrences = 1,
                confidence = Mathf.Clamp01(score),
                lastObserved = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                supportingMemories = new List<string> { content }
            };
        }
        else
        {
            var entry = patternMap[key];
            entry.occurrences++;
            entry.confidence = Mathf.Clamp01(entry.confidence + 0.05f);
            entry.lastObserved = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            if (!entry.supportingMemories.Contains(content))
                entry.supportingMemories.Add(content);
        }
    }

    // =========================================================
    // PATTERN ANALYSIS
    // =========================================================

    public List<PatternEntry> GetStrongPatterns(float minConfidence = 0.6f)
    {
        return patternMap.Values
            .Where(p => p.confidence >= minConfidence && p.occurrences >= 2)
            .OrderByDescending(p => p.confidence)
            .ToList();
    }

    public bool HasPattern(string keyword)
    {
        return patternMap.Keys.Any(k =>
            k.Contains(keyword.ToLowerInvariant()));
    }

    // =========================================================
    // EXPORT / IMPORT
    // =========================================================

    public void SavePatterns()
    {
        try
        {
            string dir = Path.GetDirectoryName(patternSavePath);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            PatternWrapper wrapper = new()
            {
                patterns = patternMap.Values.ToList()
            };

            string json = JsonUtility.ToJson(wrapper, true);
            File.WriteAllText(patternSavePath, json);

            Debug.Log($"[PatternEngine] Patterns saved → {patternSavePath}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[PatternEngine] Save failed: {ex.Message}");
        }
    }

    private void LoadPatterns()
    {
        if (!File.Exists(patternSavePath))
            return;

        try
        {
            string json = File.ReadAllText(patternSavePath);
            PatternWrapper wrapper =
                JsonUtility.FromJson<PatternWrapper>(json);

            if (wrapper?.patterns == null)
                return;

            patternMap.Clear();
            foreach (var p in wrapper.patterns)
                patternMap[p.patternKey] = p;

            Debug.Log($"[PatternEngine] Loaded {patternMap.Count} patterns.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[PatternEngine] Load failed: {ex.Message}");
        }
    }

    // =========================================================
    // UTILITIES
    // =========================================================

    private string GeneratePatternKey(string content)
    {
        content = content.ToLowerInvariant();

        if (content.Contains("threat") || content.Contains("attack"))
            return "threat-awareness";

        if (content.Contains("belief") || content.Contains("confidence"))
            return "belief-evolution";

        if (content.Contains("emotion") || content.Contains("feeling"))
            return "emotional-pattern";

        if (content.Contains("system") || content.Contains("runtime"))
            return "system-behavior";

        return "general-pattern";
    }

    // =========================================================
    // DEBUG / INSPECTION
    // =========================================================

    public void DebugPrintTopPatterns(int max = 5)
    {
        var top = patternMap.Values
            .OrderByDescending(p => p.confidence)
            .Take(max);

        foreach (var p in top)
        {
            Debug.Log(
                $"[Pattern] {p.patternKey} | conf={p.confidence:F2} | count={p.occurrences}"
            );
        }
    }
}
