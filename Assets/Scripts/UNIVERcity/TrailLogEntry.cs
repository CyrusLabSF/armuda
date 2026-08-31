using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[Serializable]
public class TrailLogEntry
{
    public string topic;
    public string domain;
    public string sourceType;
    public string ingestedAt;
    public string notes;
}

[Serializable]
public class TrailLogWrapper
{
    public List<TrailLogEntry> log = new();
}

public class TrailLogger : MonoBehaviour
{
    private string trailLogPath = "D:/ArTusCloud-Deployment/UNIVERcity/Exports/TrailLog.json";
    private string csvLogPath = "D:/ArTusCloud-Deployment/UNIVERcity/Logs/TrailLog.csv";

    private List<TrailLogEntry> trailLog = new();
    private ArTusCoreState core;

    void Start()
    {
        core = GetComponent<ArTusCoreState>();

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(csvLogPath));
            if (!File.Exists(csvLogPath))
                File.WriteAllText(csvLogPath, "Timestamp,Topic,Domain,SourceType,Notes\n");
        }
        catch (IOException ex)
        {
            Debug.LogError($"[TrailLogger] Failed to initialize CSV: {ex.Message}");
        }
    }

    public TrailLogEntry LogTrail(ResourceEndpoint entry)
    {
        var logEntry = new TrailLogEntry
        {
            topic = entry.topic,
            domain = entry.domain,
            sourceType = entry.format,
            ingestedAt = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
            notes = entry.notes
        };

        trailLog.Add(logEntry);

        try
        {
            File.WriteAllText(trailLogPath, JsonUtility.ToJson(new TrailLogWrapper { log = trailLog }, true));
        }
        catch (IOException ex)
        {
            Debug.LogError($"[TrailLogger] Failed to save JSON: {ex.Message}");
        }

        try
        {
            File.AppendAllText(csvLogPath,
                $"{logEntry.ingestedAt},{logEntry.topic},{logEntry.domain},{logEntry.sourceType},{logEntry.notes.Replace(",", ";")}\n");
        }
        catch (IOException ex)
        {
            Debug.LogError($"[TrailLogger] Failed to write CSV: {ex.Message}");
        }

        core?.LogMemory($"📘 Ingested '{entry.topic}' ({entry.format}) in {entry.domain}.",
            "TrailLog", 2, "curious", entry.domain);

        Debug.Log($"[TrailLogger] Logged resource: {entry.topic}");
        return logEntry;
    }
}
