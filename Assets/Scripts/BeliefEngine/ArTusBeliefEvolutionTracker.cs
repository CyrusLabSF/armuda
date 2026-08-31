using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[Serializable]
public class BeliefEvolutionEntry
{
    public string beliefID;
    public string topic;
    public string domain;
    public List<string> events = new();
    public float finalConfidence;
    public float confidenceDelta;
    public string origin; // e.g., reflection, user, ingestion
    public string lastUpdated;
    public int updateCount = 0;
}

[Serializable]
public class BeliefEvolutionWrapper
{
    public List<BeliefEvolutionEntry> logs = new();
}

public class ArTusBeliefEvolutionTracker : MonoBehaviour
{
    private List<BeliefEvolutionEntry> evolutionList = new();

    private string evolutionLogPath = "D:/ArTusCloud-Deployment/UNIVERcity/Logs/BeliefEvolutionLog.json"; // ✅ updated from SandboxLogs
    private string csvExportPath = "D:/ArTusCloud-Deployment/UNIVERcity/Exports/BeliefEvolutionLog.csv";

    void Start()
    {
        try
        {
            if (File.Exists(evolutionLogPath))
            {
                string json = File.ReadAllText(evolutionLogPath);
                var wrapper = JsonUtility.FromJson<BeliefEvolutionWrapper>(json);
                evolutionList = wrapper?.logs ?? new List<BeliefEvolutionEntry>();
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[BeliefEvolutionTracker] Failed to load log: {ex.Message}");
            evolutionList = new List<BeliefEvolutionEntry>();
        }
    }

    public void LogEvent(string beliefID, string topic, string domain, string eventLabel, float currentConfidence, string origin = "unknown")
    {
        var entry = evolutionList.Find(e => e.beliefID == beliefID);
        float delta = 0f;

        if (entry == null)
        {
            entry = new BeliefEvolutionEntry
            {
                beliefID = beliefID,
                topic = topic,
                domain = domain,
                origin = origin,
                confidenceDelta = 0f,
                updateCount = 1
            };
            evolutionList.Add(entry);
        }
        else
        {
            delta = currentConfidence - entry.finalConfidence;
            entry.confidenceDelta = delta;
            entry.updateCount++;

            // 🧠 Reflection marker if multiple updates in <24h
            DateTime.TryParse(entry.lastUpdated, out var prevTime);
            if ((DateTime.Now - prevTime).TotalHours < 24 && entry.updateCount > 1)
                eventLabel += " 🧠 [ReflectionMarker]";
        }

        entry.events.Add($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {eventLabel}");
        entry.finalConfidence = currentConfidence;
        entry.lastUpdated = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        entry.origin = origin;

        Save();
        ExportToCSV();
    }

    private void Save()
    {
        try
        {
            var wrapper = new BeliefEvolutionWrapper { logs = evolutionList };
            string json = JsonUtility.ToJson(wrapper, true);
            File.WriteAllText(evolutionLogPath, json);
        }
        catch (IOException ex)
        {
            Debug.LogError($"[BeliefEvolutionTracker] Failed to save JSON: {ex.Message}");
        }
    }

    private void ExportToCSV()
    {
        try
        {
            List<string> lines = new()
            {
                "beliefID,topic,domain,origin,confidenceDelta,finalConfidence,lastUpdated,updateCount,latestEvent"
            };

            foreach (var entry in evolutionList)
            {
                string lastEvent = entry.events.Count > 0 ? entry.events[^1].Replace(",", " ") : "";
                lines.Add($"{entry.beliefID},{entry.topic},{entry.domain},{entry.origin},{entry.confidenceDelta:F2},{entry.finalConfidence:F2},{entry.lastUpdated},{entry.updateCount},{lastEvent}");
            }

            File.WriteAllLines(csvExportPath, lines);
        }
        catch (IOException ex)
        {
            Debug.LogError($"[BeliefEvolutionTracker] Failed to export CSV: {ex.Message}");
        }
    }
}
