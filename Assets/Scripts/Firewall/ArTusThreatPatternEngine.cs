using UnityEngine;
using System.Collections.Generic;
using System;
using System.IO;
using System.Linq;

/// <summary>
/// Hi-Class Threat Pattern Engine
/// Observes, aggregates, scores, decays, and exports threat patterns
/// WebGL-safe, no speech, no belief mutation, no memory writes
/// </summary>
public class ArTusThreatPatternEngine : MonoBehaviour
{
    [Serializable]
    public class PatternEntry
    {
        public string type;              // "port", "cve", "network", etc.
        public string label;
        public int count = 1;
        public float confidence = 0.1f;
        public string lastSeen;
        public int contradictionCount = 0;

        public string patternID => $"{type}:{label}";
        public float influenceScore => confidence * count;
    }

    [Serializable]
    private class PatternWrapper
    {
        public List<PatternEntry> patterns;
    }

    // --------------------------------------------------
    // CONFIG (RELATIVE PATHS ONLY)
    // --------------------------------------------------

    [Header("Export Paths (Relative)")]
    [SerializeField]
    private string exportRelativePath =
        "UNIVERcity/ThreatLogs/ThreatPatterns.json";

    [SerializeField]
    private string historyRelativePath =
        "UNIVERcity/ThreatLogs/PatternHistory.json";

    // --------------------------------------------------
    // INTERNAL STATE
    // --------------------------------------------------

    private readonly Dictionary<string, PatternEntry> patterns = new();

    private string exportPath;
    private string historyLogPath;

    // --------------------------------------------------
    // LIFECYCLE
    // --------------------------------------------------

    void Awake()
    {
        exportPath = ArTusPathUtility.GetPersistent(exportRelativePath);
        historyLogPath = ArTusPathUtility.GetPersistent(historyRelativePath);

        EnsureDirectory(exportPath);
        EnsureDirectory(historyLogPath);
    }

    // --------------------------------------------------
    // PUBLIC API
    // --------------------------------------------------

    public void ObservePattern(string label, string type = "port")
    {
        string key = $"{type}:{label}";
        string now = DateTime.UtcNow.ToString("o");

        if (!patterns.TryGetValue(key, out var entry))
        {
            entry = new PatternEntry
            {
                label = label,
                type = type,
                count = 1,
                confidence = 0.1f,
                lastSeen = now,
                contradictionCount = 0
            };
            patterns[key] = entry;
        }
        else
        {
            entry.count++;
            entry.confidence = Mathf.Clamp01(entry.confidence + 0.1f);
            entry.lastSeen = now;
        }

        AppendHistory(entry);
        ExportPatterns();
    }

    public void RegisterContradiction(string label, string type)
    {
        if (!patterns.TryGetValue($"{type}:{label}", out var entry))
            return;

        entry.contradictionCount++;
        entry.confidence = Mathf.Clamp01(entry.confidence - 0.1f);

        ExportPatterns();
    }

    public List<PatternEntry> GetAllPatterns()
    {
        return patterns.Values.ToList();
    }

    public bool TryGetPattern(string type, string label, out PatternEntry entry)
    {
        return patterns.TryGetValue($"{type}:{label}", out entry);
    }

    // --------------------------------------------------
    // PASSIVE DECAY
    // --------------------------------------------------

    public void DecayPatterns(float decayRate = 0.05f, int staleDays = 3)
    {
        DateTime now = DateTime.UtcNow;

        foreach (var entry in patterns.Values)
        {
            if (DateTime.TryParse(entry.lastSeen, out var lastSeen))
            {
                if ((now - lastSeen).TotalDays > staleDays)
                    entry.confidence = Mathf.Clamp01(entry.confidence - decayRate);
            }

            if (entry.contradictionCount > 0)
            {
                entry.confidence = Mathf.Clamp01(
                    entry.confidence - entry.contradictionCount * decayRate
                );
            }
        }

        // Prune fully decayed entries
        var alive = patterns
            .Where(kv => kv.Value.confidence > 0f)
            .ToDictionary(kv => kv.Key, kv => kv.Value);

        patterns.Clear();
        foreach (var kv in alive)
            patterns[kv.Key] = kv.Value;

        ExportPatterns();
    }

    // --------------------------------------------------
    // EXPORTS
    // --------------------------------------------------

    private void ExportPatterns()
    {
        try
        {
            var wrapper = new PatternWrapper
            {
                patterns = patterns.Values.ToList()
            };

            File.WriteAllText(
                exportPath,
                JsonUtility.ToJson(wrapper, true)
            );
        }
        catch (Exception e)
        {
            Debug.LogError($"[ThreatPatternEngine] Export failed: {e.Message}");
        }
    }

    private void AppendHistory(PatternEntry entry)
    {
        string line =
            $"{{\"timestamp\":\"{DateTime.UtcNow:o}\"," +
            $"\"type\":\"{entry.type}\"," +
            $"\"label\":\"{entry.label}\"," +
            $"\"count\":{entry.count}," +
            $"\"confidence\":{entry.confidence:F2}," +
            $"\"contradictions\":{entry.contradictionCount}}}";

        File.AppendAllText(historyLogPath, line + Environment.NewLine);
    }

    // --------------------------------------------------
    // UTILS
    // --------------------------------------------------

    private static void EnsureDirectory(string filePath)
    {
        string dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
    }
}
