using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System;

public class ArTusTrailLinker : MonoBehaviour
{
    private ArTusBeliefEngine beliefEngine;

    [Header("Export")]
    [SerializeField]
    private string exportSubPath =
        "UNIVERcity/Exports/TrailBridges";

    private string exportDirectory;

    // --------------------------------------------------
    // UNITY LIFECYCLE
    // --------------------------------------------------
    void Awake()
    {
        beliefEngine = GetComponent<ArTusBeliefEngine>();
        exportDirectory = ArTusPathUtility.GetPersistent(exportSubPath);

        if (!string.IsNullOrEmpty(exportDirectory))
            Directory.CreateDirectory(exportDirectory);
    }

    // --------------------------------------------------
    // INTENTIONAL LINK API (USED BY CORESTATE)
    // --------------------------------------------------
    public void LinkTrail(string source, string target)
    {
        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(target))
            return;

        var wrapper = new TrailLinkRecord
        {
            timestamp = DateTime.UtcNow.ToString("o"),
            source = source,
            target = target
        };

        ExportLink(wrapper);
    }

    // --------------------------------------------------
    // PASSIVE ANALYZER (UNCHANGED INTENT)
    // --------------------------------------------------
    public TrailBridgeWrapper Analyze()
    {
        if (beliefEngine == null || beliefEngine.beliefs == null)
            return null;

        var nodes = beliefEngine.beliefs.Keys.ToList();
        if (nodes.Count < 2)
            return null;

        Dictionary<string, List<string>> topicMap = new();

        foreach (var topic in nodes)
        {
            string category = ExtractTopicKey(topic);

            if (!topicMap.ContainsKey(category))
                topicMap[category] = new List<string>();

            topicMap[category].Add(topic);
        }

        List<TrailBridge> bridges = new();
        var keys = topicMap.Keys.ToList();

        for (int i = 0; i < keys.Count; i++)
        {
            for (int j = i + 1; j < keys.Count; j++)
            {
                var overlap = topicMap[keys[i]]
                    .Intersect(topicMap[keys[j]])
                    .ToList();

                if (overlap.Count > 0)
                {
                    bridges.Add(new TrailBridge
                    {
                        categoryA = keys[i],
                        categoryB = keys[j],
                        overlapCount = overlap.Count,
                        sharedTopics = overlap
                    });
                }
            }
        }

        if (bridges.Count == 0)
            return null;

        var wrapper = new TrailBridgeWrapper
        {
            timestamp = DateTime.UtcNow.ToString("o"),
            bridges = bridges,
            source = "TrailLinker"
        };

        Export(wrapper);
        return wrapper;
    }

    // --------------------------------------------------
    // EXPORTS
    // --------------------------------------------------
    private void Export(TrailBridgeWrapper wrapper)
    {
        try
        {
            string file =
                $"TrailBridgeSummary_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json";

            string json = JsonUtility.ToJson(wrapper, true);
            File.WriteAllText(Path.Combine(exportDirectory, file), json);
        }
        catch (IOException ex)
        {
            Debug.LogWarning($"[TrailLinker] Export failed: {ex.Message}");
        }
    }

    private void ExportLink(TrailLinkRecord record)
    {
        try
        {
            string file =
                $"TrailLink_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json";

            string json = JsonUtility.ToJson(record, true);
            File.WriteAllText(Path.Combine(exportDirectory, file), json);
        }
        catch (IOException ex)
        {
            Debug.LogWarning($"[TrailLinker] Link export failed: {ex.Message}");
        }
    }

    // --------------------------------------------------
    // TOPIC NORMALIZATION
    // --------------------------------------------------
    private string ExtractTopicKey(string belief)
    {
        string lower = belief.ToLowerInvariant();

        if (lower.Contains("plasticity")) return "Neural Plasticity";
        if (lower.Contains("habit")) return "Habit Formation";
        if (lower.Contains("emotion")) return "Emotional Processing";
        if (lower.Contains("memory")) return "Memory Systems";
        if (lower.Contains("language")) return "Language Comprehension";
        if (lower.Contains("pattern")) return "Pattern Recognition";
        if (lower.Contains("attention")) return "Attention Models";
        if (lower.Contains("visual")) return "Visual Processing";

        return "General";
    }

    // --------------------------------------------------
    // DATA MODELS
    // --------------------------------------------------
    [Serializable]
    public class TrailBridge
    {
        public string categoryA;
        public string categoryB;
        public int overlapCount;
        public List<string> sharedTopics;
    }

    [Serializable]
    public class TrailBridgeWrapper
    {
        public string timestamp;
        public string source;
        public List<TrailBridge> bridges;
    }

    [Serializable]
    public class TrailLinkRecord
    {
        public string timestamp;
        public string source;
        public string target;
    }
}
