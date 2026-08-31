using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;
using System.IO;
using ArTusTypes;

[Serializable]
public class EpisodicEvent
{
    public string eventName;
    public List<MemoryEntry> entries = new();
    public string dominantEmotion;
    public string dominantCategory;
    public float averageClarity;
    public float averageConfidence;
    public string timestamp;
    public float ageInDays;
    public string trailID;
    public string source = "EpisodicMemory";
}

public class ArTusEpisodicMemory : MonoBehaviour
{
    [Header("Episode Settings")]
    public int maxMemoriesPerEvent = 5;

    [Header("Export Settings")]
    public string exportPath = "D:/ArTusCloud-Deployment/UNIVERcity/Episodes/";
    public bool enableExport = true;

    private ArTusCoreState core;

    public readonly List<EpisodicEvent> eventHistory = new();

    // --------------------------------------------------
    // UNITY LIFECYCLE
    // --------------------------------------------------
    void Awake()
    {
        core = GetComponent<ArTusCoreState>();
        Directory.CreateDirectory(exportPath);
    }

    // --------------------------------------------------
    // PUBLIC API (PASSIVE)
    // --------------------------------------------------
    public EpisodicEvent CreateEvent(string optionalName = "")
    {
        if (core == null)
            return null;

        List<MemoryEntry> recent = core.GetAllMemoryEntries()
            .TakeLast(maxMemoriesPerEvent)
            .ToList();

        if (recent.Count == 0)
            return null;

        string dominantEmotion = GetDominantEmotion(recent);
        string dominantCategory = GetDominantCategory(recent);

        float avgClarity = recent.Average(m => m.clarity);
        float avgConfidence = recent.Average(m => m.confidence);

        string timestamp = DateTime.UtcNow.ToString("o");
        string trailID = $"Episode_{dominantEmotion}_{DateTime.UtcNow:yyyyMMddHHmmss}";

        EpisodicEvent ep = new EpisodicEvent
        {
            eventName = string.IsNullOrWhiteSpace(optionalName)
                ? GenerateName(dominantEmotion)
                : optionalName,
            entries = recent,
            dominantEmotion = dominantEmotion,
            dominantCategory = dominantCategory,
            averageClarity = avgClarity,
            averageConfidence = avgConfidence,
            timestamp = timestamp,
            ageInDays = 0f,
            trailID = trailID
        };

        eventHistory.Add(ep);

        if (enableExport)
        {
            ExportEpisode(ep);
            ExportToCSV(ep);
        }

        return ep;
    }

    // --------------------------------------------------
    // QUERY
    // --------------------------------------------------
    public EpisodicEvent GetLastEvent()
    {
        if (eventHistory.Count == 0)
            return null;

        EpisodicEvent last = eventHistory.Last();
        last.ageInDays = (float)(DateTime.UtcNow - DateTime.Parse(last.timestamp)).TotalDays;
        return last;
    }

    // --------------------------------------------------
    // BACKWARD-COMPATIBILITY SHIM (REQUIRED)
    // --------------------------------------------------
    public EpisodicEvent ReflectOnLastEvent()
    {
        return GetLastEvent();
    }

    // --------------------------------------------------
    // METRICS
    // --------------------------------------------------
    private string GetDominantEmotion(List<MemoryEntry> entries)
    {
        return entries
            .GroupBy(e => e.emotion)
            .OrderByDescending(g => g.Count())
            .FirstOrDefault()?.Key ?? "neutral";
    }

    private string GetDominantCategory(List<MemoryEntry> entries)
    {
        return entries
            .GroupBy(e => e.category)
            .OrderByDescending(g => g.Count())
            .FirstOrDefault()?.Key ?? "general";
    }

    private string GenerateName(string emotion)
    {
        string hash = Guid.NewGuid().ToString()[..8];
        return $"Episode_{emotion}_{hash}";
    }

    // --------------------------------------------------
    // EXPORT
    // --------------------------------------------------
    private void ExportEpisode(EpisodicEvent ep)
    {
        try
        {
            string safeName = ep.eventName.Replace(" ", "_").Replace(":", "").Replace("/", "");
            string json = JsonUtility.ToJson(ep, true);
            File.WriteAllText(
                Path.Combine(exportPath, $"{safeName}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json"),
                json
            );
        }
        catch (IOException ex)
        {
            Debug.LogWarning($"[EpisodicMemory] JSON export failed: {ex.Message}");
        }
    }

    private void ExportToCSV(EpisodicEvent ep)
    {
        try
        {
            string filename = $"{ep.eventName.Replace(" ", "_")}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";
            string fullPath = Path.Combine(exportPath, filename);

            using StreamWriter writer = new(fullPath);
            writer.WriteLine("Timestamp,Content,Clarity,Confidence,Emotion,Category,TrailID");

            foreach (var entry in ep.entries)
            {
                writer.WriteLine(
                    $"\"{entry.timestamp}\"," +
                    $"\"{entry.content}\"," +
                    $"{entry.clarity:F2}," +
                    $"{entry.confidence:F2}," +
                    $"\"{entry.emotion}\"," +
                    $"\"{entry.category}\"," +
                    $"\"{ep.trailID}\""
                );
            }
        }
        catch (IOException ex)
        {
            Debug.LogWarning($"[EpisodicMemory] CSV export failed: {ex.Message}");
        }
    }
}
