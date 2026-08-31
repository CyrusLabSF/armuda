using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System;
using System.Linq;
using Debug = UnityEngine.Debug;

/// <summary>
/// Threat feed manager for ArTus.
/// Exports events to CSV + JSON, supports recovery, archiving,
/// and passive integration with observers and analytics.
/// WebGL-safe (persistentDataPath only).
/// </summary>
public class ThreatFeedExporter : MonoBehaviour
{
    // --------------------------------------------------
    // PATHS (WEBGL SAFE)
    // --------------------------------------------------
    private string CsvPath =>
        ArTusPathUtility.GetPersistent("UNIVERcity/Defense/ThreatFeed.csv");

    private string JsonPath =>
        ArTusPathUtility.GetPersistent("UNIVERcity/Defense/ThreatFeed.json");

    private string ArchiveFolder =>
        ArTusPathUtility.GetPersistent("UNIVERcity/Defense/Archive");

    private const long MAX_FILE_SIZE_BYTES = 5 * 1024 * 1024; // 5 MB
    private const int MAX_IN_MEMORY = 5000;

    // --------------------------------------------------
    // DATA TYPES
    // --------------------------------------------------
    [Serializable]
    public class ThreatEntry
    {
        public string id;
        public string timestamp;
        public int port;
        public string explanation;
        public string userDecision;
        public string riskLevel;
        public string trailID;
        public string source;

        // Confidence (dual-mode)
        public string confidenceRaw;
        public float confidenceScore;

        public string resolution;
    }

    [Serializable]
    private class ThreatEntryWrapper
    {
        public List<ThreatEntry> entries = new();
    }

    private List<ThreatEntry> exportQueue = new();

    // --------------------------------------------------
    // UNITY
    // --------------------------------------------------
    void Awake()
    {
        EnsureDirectories();
        LoadExisting();
    }

    // --------------------------------------------------
    // PUBLIC ENTRY POINT
    // --------------------------------------------------
    public void AddToThreatFeed(
        int port,
        string explanation,
        string source = "unknown",
        string decision = "pending",
        string risk = "unknown",
        string confidence = "n/a",
        string resolution = "unresolved"
    )
    {
        float parsedConfidence = ParseConfidence(confidence);

        var entry = new ThreatEntry
        {
            id = $"Threat_{port}_{DateTime.Now:yyyyMMddHHmmssfff}",
            timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            port = port,
            explanation = explanation,
            userDecision = decision,
            riskLevel = risk,
            trailID = $"Trail_Port_{port}_Consent",
            source = source,
            confidenceRaw = confidence,
            confidenceScore = parsedConfidence,
            resolution = resolution
        };

        exportQueue.Add(entry);

        AppendToCsv(entry);
        SaveJson();
        CheckArchive();
        PruneMemory();
    }

    // --------------------------------------------------
    // LOAD / SAVE
    // --------------------------------------------------
    private void LoadExisting()
    {
        try
        {
            if (!File.Exists(JsonPath)) return;

            string content = File.ReadAllText(JsonPath);
            var wrapper = JsonUtility.FromJson<ThreatEntryWrapper>(content);

            if (wrapper?.entries != null)
            {
                exportQueue = wrapper.entries;
                Debug.Log($"[ThreatFeedExporter] Loaded {exportQueue.Count} entries.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ThreatFeedExporter] Load failed: {ex.Message}");
        }
    }

    private void SaveJson()
    {
        try
        {
            var wrapper = new ThreatEntryWrapper { entries = exportQueue };
            File.WriteAllText(JsonPath, JsonUtility.ToJson(wrapper, true));
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ThreatFeedExporter] JSON save failed: {ex.Message}");
        }
    }

    // --------------------------------------------------
    // CSV EXPORT
    // --------------------------------------------------
    private void AppendToCsv(ThreatEntry entry)
    {
        bool headerNeeded = !File.Exists(CsvPath);

        using StreamWriter writer = new StreamWriter(CsvPath, append: true);

        if (headerNeeded)
        {
            writer.WriteLine(
                "ID,Timestamp,Port,Explanation,UserDecision,Risk,TrailID,Source,ConfidenceRaw,ConfidenceScore,Resolution"
            );
        }

        writer.WriteLine(string.Join(",",
            Escape(entry.id),
            Escape(entry.timestamp),
            entry.port,
            Escape(entry.explanation),
            Escape(entry.userDecision),
            Escape(entry.riskLevel),
            Escape(entry.trailID),
            Escape(entry.source),
            Escape(entry.confidenceRaw),
            entry.confidenceScore.ToString("F2"),
            Escape(entry.resolution)
        ));
    }

    public void ExportAllToCsv()
    {
        try
        {
            using StreamWriter writer = new StreamWriter(CsvPath, false);

            writer.WriteLine(
                "ID,Timestamp,Port,Explanation,UserDecision,Risk,TrailID,Source,ConfidenceRaw,ConfidenceScore,Resolution"
            );

            foreach (var e in exportQueue)
            {
                writer.WriteLine(string.Join(",",
                    Escape(e.id),
                    Escape(e.timestamp),
                    e.port,
                    Escape(e.explanation),
                    Escape(e.userDecision),
                    Escape(e.riskLevel),
                    Escape(e.trailID),
                    Escape(e.source),
                    Escape(e.confidenceRaw),
                    e.confidenceScore.ToString("F2"),
                    Escape(e.resolution)
                ));
            }

            Debug.Log("[ThreatFeedExporter] Full CSV export complete.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ThreatFeedExporter] Full export failed: {ex.Message}");
        }
    }

    // --------------------------------------------------
    // HOUSEKEEPING
    // --------------------------------------------------
    private void CheckArchive()
    {
        try
        {
            if (File.Exists(CsvPath) && new FileInfo(CsvPath).Length > MAX_FILE_SIZE_BYTES)
            {
                string target =
                    Path.Combine(ArchiveFolder,
                        $"ThreatFeed_{DateTime.Now:yyyyMMddHHmmss}.csv");

                File.Move(CsvPath, target);
            }

            if (File.Exists(JsonPath) && new FileInfo(JsonPath).Length > MAX_FILE_SIZE_BYTES)
            {
                string target =
                    Path.Combine(ArchiveFolder,
                        $"ThreatFeed_{DateTime.Now:yyyyMMddHHmmss}.json");

                File.Move(JsonPath, target);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ThreatFeedExporter] Archive failed: {ex.Message}");
        }
    }

    private void PruneMemory()
    {
        if (exportQueue.Count <= MAX_IN_MEMORY)
            return;

        exportQueue = exportQueue
            .OrderByDescending(e => e.timestamp)
            .Take(MAX_IN_MEMORY)
            .ToList();
    }

    // --------------------------------------------------
    // UTIL
    // --------------------------------------------------
    private void EnsureDirectories()
    {
        Directory.CreateDirectory(
            Path.GetDirectoryName(CsvPath)
        );

        Directory.CreateDirectory(
            Path.GetDirectoryName(JsonPath)
        );

        Directory.CreateDirectory(ArchiveFolder);
    }

    private static string Escape(string value)
    {
        if (string.IsNullOrEmpty(value)) return "\"\"";
        return $"\"{value.Replace("\"", "\"\"")}\"";
    }

    private float ParseConfidence(string raw)
    {
        if (float.TryParse(raw, out float v))
            return Mathf.Clamp01(v);

        return raw switch
        {
            "low" => 0.3f,
            "medium" => 0.6f,
            "high" => 0.9f,
            _ => 0.0f
        };
    }
}
