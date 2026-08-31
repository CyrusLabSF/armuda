using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

/// <summary>
/// Tracks ingested CVEs as learning trails
/// - Logs each CVE into a JSON + CSV trail
/// - Provides replay + summary
/// - Hooks into Armuda for visualization
/// </summary>
public class CVETrailManager : MonoBehaviour
{
    private string jsonPath = "D:/ArTusCloud-Deployment/UNIVERcity/Trails/CVETrails.json";
    private string csvPath = "D:/ArTusCloud-Deployment/UNIVERcity/Exports/CVETrails.csv";

    [Serializable]
    public class CVETrailEntry
    {
        public string cveId;
        public string description;
        public string severity;
        public string cvss;
        public string timestamp;
        public float confidence;
        public string emotion;
    }

    [Serializable]
    private class CVETrailWrapper
    {
        public List<CVETrailEntry> entries = new();
    }

    private CVETrailWrapper trails = new();

    void Awake()
    {
        LoadTrails();
    }

    public void RegisterCVE(string cveId, string desc, string severity, string cvss, float confidence, string emotion)
    {
        var entry = new CVETrailEntry
        {
            cveId = cveId,
            description = desc,
            severity = severity,
            cvss = cvss,
            timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            confidence = confidence,
            emotion = emotion
        };

        trails.entries.Add(entry);

        SaveTrails();
        ExportCSV(entry);

        Debug.Log($"[CVETrail] Logged CVE {cveId} ({severity}) with emotion {emotion}");

        // Optional: trigger Armuda visualization
        GetComponent<ArTusCoreState>()?.TriggerInternalAdvisory(
            "CVE Trail Update",
            $"New CVE trail node: {cveId} ({severity})",
            confidence
        );
    }

    private void SaveTrails()
    {
        try
        {
            string json = JsonConvert.SerializeObject(trails, Formatting.Indented);
            FileIOManager.QueueWrite(jsonPath, json, "CVETrailExport");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[CVETrail] Failed to save JSON: {ex.Message}");
        }
    }

    private void ExportCSV(CVETrailEntry entry)
    {
        try
        {
            bool exists = File.Exists(csvPath);
            using StreamWriter writer = new(csvPath, true);

            if (!exists)
                writer.WriteLine("Timestamp,CVE_ID,Severity,CVSS,Confidence,Emotion,Description");

            writer.WriteLine(
                $"{entry.timestamp},{entry.cveId},{entry.severity},{entry.cvss},{entry.confidence:F2},{entry.emotion},{entry.description.Replace(",", "|")}"
            );
        }
        catch (Exception ex)
        {
            Debug.LogError($"[CVETrail] Failed to export CSV: {ex.Message}");
        }
    }

    public void ReplayCVETrails(int count = 5)
    {
        int replayCount = Mathf.Min(count, trails.entries.Count);
        if (replayCount == 0)
        {
            Debug.Log("[CVETrail] No CVE trails to replay.");
            return;
        }

        Debug.Log($"[CVETrail] Replaying last {replayCount} CVE trails...");
        foreach (var entry in trails.entries.GetRange(trails.entries.Count - replayCount, replayCount))
        {
            GetComponent<ArTusSpeechResponder>()?.RequestSpeak(
                $"CVE {entry.cveId}, severity {entry.severity}, confidence {entry.confidence:F2}.",
                ArTusSpeechResponder.SpeechCategory.Reflection
            );
        }
    }

    private void LoadTrails()
    {
        if (!File.Exists(jsonPath)) return;

        try
        {
            string json = File.ReadAllText(jsonPath);
            trails = JsonConvert.DeserializeObject<CVETrailWrapper>(json) ?? new CVETrailWrapper();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[CVETrail] Failed to load JSON: {ex.Message}");
        }
    }
}
