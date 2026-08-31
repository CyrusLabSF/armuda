using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System;
using System.Globalization;

#region DATA MODELS

[Serializable]
public class FlatMemoryTrailEntry
{
    public string id;
    public string belief;
    public float confidence;
    public string emotion;
    public string trail;
    public string domain;
    public string type;
    public string origin;
    public string lastUpdated;
    public float decay;
    public float confidenceDelta;
    public bool contradictionFlag;
}

[Serializable]
public class FlatMemoryTrailWrapper
{
    public List<FlatMemoryTrailEntry> trails = new();
}

#endregion

/// <summary>
/// Hi-Class Memory Trail Flattener
/// Passive utility: converts MemoryTrail.json → CSV
/// No cognition, no mutation, no side effects
/// WebGL & Serialization safe
/// </summary>
public class MemoryTrailFlattener : MonoBehaviour
{
    [Header("Behavior")]
    public bool overwriteCsv = true;

    // Resolved paths (runtime only)
    private string jsonPath;
    private string csvPath;
    private string logPath;

    // --------------------------------------------------
    // UNITY LIFECYCLE
    // --------------------------------------------------
    private void Awake()
    {
        jsonPath = ArTusPathUtility.GetPersistent(
            "UNIVERcity/Exports/MemoryTrail.json"
        );

        csvPath = ArTusPathUtility.GetPersistent(
            "UNIVERcity/Exports/MemoryTrail.csv"
        );

        logPath = ArTusPathUtility.GetPersistent(
            "UNIVERcity/Logs/MemoryTrailExportLog.txt"
        );
    }

    // --------------------------------------------------
    // MANUAL ENTRY POINT ONLY
    // --------------------------------------------------
    [ContextMenu("Flatten MemoryTrail.json to CSV")]
    public void FlattenMemoryTrail()
    {
        if (!ValidatePaths())
            return;

        FlatMemoryTrailWrapper wrapper;

        try
        {
            string json = File.ReadAllText(jsonPath);
            wrapper = JsonUtility.FromJson<FlatMemoryTrailWrapper>(json);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[MemoryTrailFlattener] Failed to read or parse JSON: {ex.Message}");
            return;
        }

        if (wrapper?.trails == null || wrapper.trails.Count == 0)
        {
            Debug.LogWarning("[MemoryTrailFlattener] No memory trails found.");
            return;
        }

        WriteCsv(wrapper.trails);
    }

    // --------------------------------------------------
    // CORE LOGIC
    // --------------------------------------------------
    private void WriteCsv(List<FlatMemoryTrailEntry> entries)
    {
        int validCount = 0;
        bool writeHeader = overwriteCsv || !File.Exists(csvPath);

        using StreamWriter sw = new StreamWriter(csvPath, !overwriteCsv);

        if (writeHeader)
        {
            sw.WriteLine(
                "id,belief,confidence,emotion,trail,domain,type,origin,lastUpdated,decay,confidenceDelta,contradictionFlag"
            );
        }

        foreach (var entry in entries)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.belief))
                continue;

            string line = string.Join(",",
                Escape(entry.id),
                Escape(entry.belief),
                entry.confidence.ToString("0.00", CultureInfo.InvariantCulture),
                Escape(entry.emotion),
                Escape(entry.trail),
                Escape(entry.domain),
                Escape(entry.type),
                Escape(entry.origin),
                Escape(entry.lastUpdated),
                entry.decay.ToString("0.00", CultureInfo.InvariantCulture),
                entry.confidenceDelta.ToString("0.00", CultureInfo.InvariantCulture),
                entry.contradictionFlag ? "true" : "false"
            );

            sw.WriteLine(line);
            validCount++;
        }

        LogExport(validCount);
        Debug.Log($"✅ [MemoryTrailFlattener] Exported {validCount} entries → {csvPath}");
    }

    // --------------------------------------------------
    // HELPERS
    // --------------------------------------------------
    private bool ValidatePaths()
    {
        if (!File.Exists(jsonPath))
        {
            Debug.LogWarning("[MemoryTrailFlattener] MemoryTrail.json not found.");
            return false;
        }

        string csvDir = Path.GetDirectoryName(csvPath);
        string logDir = Path.GetDirectoryName(logPath);

        if (!string.IsNullOrEmpty(csvDir))
            Directory.CreateDirectory(csvDir);

        if (!string.IsNullOrEmpty(logDir))
            Directory.CreateDirectory(logDir);

        return true;
    }

    private void LogExport(int count)
    {
        try
        {
            File.AppendAllText(
                logPath,
                $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] Flattened {count} memory trail entries.\n"
            );
        }
        catch
        {
            // Logging failure must never break export
        }
    }

    private string Escape(string input)
    {
        if (string.IsNullOrEmpty(input)) return "\"\"";
        input = input.Replace("\"", "'")
                     .Replace(",", "|")
                     .Replace("\n", " ")
                     .Replace("\r", " ");
        return $"\"{input}\"";
    }
}
