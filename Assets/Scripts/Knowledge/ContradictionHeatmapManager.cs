using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

/// <summary>
/// Tracks contradictions between beliefs and exports a heatmap.
/// - Singleton manager (global access point)
/// - Maintains per-domain + per-topic counters
/// - Analyzes severity based on conflict count + confidence delta
/// - Saves JSON + CSV for PowerBI
/// - Provides Unity/Armuda visualization hooks
/// </summary>
public class ContradictionHeatmapManager : MonoBehaviour
{
    public static ContradictionHeatmapManager Instance { get; private set; }

    private string jsonPath = "D:/ArTusCloud-Deployment/UNIVERcity/Exports/ContradictionHeatmap.json";
    private string csvPath = "D:/ArTusCloud-Deployment/UNIVERcity/Exports/ContradictionHeatmap.csv";

    [Serializable]
    public class HeatmapEntry
    {
        public string domain;
        public string topic;
        public int conflictCount;
        public string severity;
        public float avgConfidenceDelta;
        public string lastDetected;
    }

    private Dictionary<string, Dictionary<string, HeatmapEntry>> heatmap =
        new Dictionary<string, Dictionary<string, HeatmapEntry>>();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadHeatmap();
    }

    /// <summary>
    /// Register a contradiction and update the heatmap
    /// </summary>
    public void RegisterContradiction(string domain, string topic, float confidenceDelta = 0f)
    {
        if (!heatmap.ContainsKey(domain))
            heatmap[domain] = new Dictionary<string, HeatmapEntry>();

        if (!heatmap[domain].ContainsKey(topic))
        {
            heatmap[domain][topic] = new HeatmapEntry
            {
                domain = domain,
                topic = topic,
                conflictCount = 0,
                severity = "low",
                avgConfidenceDelta = 0f,
                lastDetected = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
            };
        }

        var entry = heatmap[domain][topic];
        entry.conflictCount++;
        entry.lastDetected = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

        // Rolling average for confidence deltas
        entry.avgConfidenceDelta = (entry.avgConfidenceDelta + Math.Abs(confidenceDelta)) / 2f;

        // Severity: based on both conflict count + confidence mismatch
        entry.severity = (entry.conflictCount, entry.avgConfidenceDelta) switch
        {
            ( >= 10, >= 0.5f) => "critical",
            ( >= 10, _) => "high",
            ( >= 5, >= 0.4f) => "high",
            ( >= 5, _) => "moderate",
            (_, >= 0.6f) => "moderate",
            _ => "low"
        };

        Debug.Log($"[Heatmap] Contradiction updated: {domain}/{topic} → {entry.conflictCount} ({entry.severity}, Δ={entry.avgConfidenceDelta:F2})");

        SaveHeatmap();
        ExportCSV();

        // Optional: Armuda visual pulse
        // ArmudaVisualizer.Instance?.PulseContradiction(domain, topic, entry.severity);
    }

    private void SaveHeatmap()
    {
        try
        {
            string json = JsonConvert.SerializeObject(heatmap, Formatting.Indented);
            FileIOManager.QueueWrite(jsonPath, json, "HeatmapSave");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Heatmap] Failed to save JSON: {ex.Message}");
        }
    }

    private void ExportCSV()
    {
        try
        {
            using StreamWriter writer = new StreamWriter(csvPath, false);
            writer.WriteLine("Domain,Topic,Conflicts,Severity,AvgConfidenceDelta,LastDetected");

            foreach (var domain in heatmap)
            {
                foreach (var topic in domain.Value.Values)
                {
                    writer.WriteLine($"{topic.domain},{topic.topic},{topic.conflictCount},{topic.severity},{topic.avgConfidenceDelta:F2},{topic.lastDetected}");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Heatmap] Failed to export CSV: {ex.Message}");
        }
    }

    private void LoadHeatmap()
    {
        if (!File.Exists(jsonPath))
        {
            Debug.LogWarning("[Heatmap] No heatmap file found — starting fresh.");
            heatmap = new Dictionary<string, Dictionary<string, HeatmapEntry>>();
            return;
        }

        try
        {
            string json = File.ReadAllText(jsonPath);

            // Defensive deserialization with fallback
            var loaded = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, HeatmapEntry>>>(json);

            if (loaded != null)
            {
                heatmap = loaded;
                Debug.Log($"[Heatmap] ✅ Loaded heatmap with {heatmap.Count} domains.");
            }
            else
            {
                Debug.LogWarning("[Heatmap] File parsed but contained no data — starting new map.");
                heatmap = new Dictionary<string, Dictionary<string, HeatmapEntry>>();
            }
        }
        catch (IOException ioEx)
        {
            Debug.LogError($"[Heatmap] IO error while loading: {ioEx.Message}");
            heatmap = new Dictionary<string, Dictionary<string, HeatmapEntry>>();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Heatmap] Failed to load JSON: {ex.Message}");
            heatmap = new Dictionary<string, Dictionary<string, HeatmapEntry>>();
        }
    }
}
