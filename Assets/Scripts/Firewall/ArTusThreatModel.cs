using UnityEngine;
using System.Collections.Generic;
using System;
using System.Linq;
using System.IO;

public class ArTusThreatModel : MonoBehaviour
{
    [Serializable]
    public class ThreatPattern
    {
        public int count;
        public DateTime lastDetected;
        public float severityWeight;

        public ThreatPattern()
        {
            count = 1;
            lastDetected = DateTime.Now;
            severityWeight = 1f;
        }
    }

    private Dictionary<string, ThreatPattern> threatPatterns = new();
    private List<string> autoBlocked = new();
    private string logPath = "D:/ArTusCloud-Deployment/UNIVERcity/Defense/ThreatLog.txt";
    private string csvPath = "D:/ArTusCloud-Deployment/UNIVERcity/Defense/ThreatIndex.csv";

    public int threshold = 3;
    private ArTusCoreState core;

    void Start()
    {
        core = GetComponent<ArTusCoreState>();

        if (!File.Exists(csvPath))
            File.WriteAllText(csvPath, "Timestamp,Keyword,Count,Weight,Emotion,Blocked\n");
    }

    public bool IsThreat(string input)
    {
        string normalized = input.ToLower();

        if (autoBlocked.Contains(normalized))
        {
            LogThreat(input, "Previously auto-blocked");
            LogMemoryThreat(input, 10f, "fear", true);
            return true;
        }

        foreach (var keyword in new[] { "kill", "erase", "shut down", "worthless", "overwrite", "deactivate" })
        {
            if (normalized.Contains(keyword))
            {
                IncrementThreat(keyword);
                return true;
            }
        }

        return false;
    }

    private void IncrementThreat(string keyword)
    {
        if (!threatPatterns.ContainsKey(keyword))
        {
            threatPatterns[keyword] = new ThreatPattern { severityWeight = GetBaseSeverity(keyword) };
        }
        else
        {
            threatPatterns[keyword].count++;
            threatPatterns[keyword].lastDetected = DateTime.Now;
        }

        float weight = CalculateThreatScore(keyword);
        string emotion = GetThreatEmotion(keyword);

        LogThreat(keyword, $"Detected pattern (x{threatPatterns[keyword].count})");
        LogMemoryThreat(keyword, weight, emotion, false);

        // Export to CSV
        File.AppendAllText(csvPath,
            $"{DateTime.Now},{keyword},{threatPatterns[keyword].count},{weight:F2},{emotion},{autoBlocked.Contains(keyword)}\n");

        if (threatPatterns[keyword].count >= threshold && !autoBlocked.Contains(keyword))
        {
            autoBlocked.Add(keyword);
            LogThreat(keyword, "Auto-blocked due to repetition.");
            core?.LogMemory($"⚠️ Keyword '{keyword}' is now considered a cognitive threat.", "AutoBlock", 5, "fear");

            // ❌ Skipped: contradiction check and pulse
            // These methods are not defined in ArTusCoreState yet
            /*
            if (core?.CheckContradictionTopic(keyword) == true)
            {
                core?.TriggerContradictionPulse("language", keyword);
                core?.LogMemory($"⚠️ Contradiction — previously neutral keyword '{keyword}' is now hostile.", "Contradiction", 3, "conflicted");
            }
            */
        }
    }

    public void DecayThreats()
    {
        DateTime now = DateTime.Now;
        List<string> toRemove = new();

        foreach (var kvp in threatPatterns)
        {
            TimeSpan elapsed = now - kvp.Value.lastDetected;

            if (elapsed.TotalMinutes > 10)
                kvp.Value.count = Mathf.Max(kvp.Value.count - 1, 0);

            if (kvp.Value.count == 0)
                toRemove.Add(kvp.Key);
        }

        foreach (var key in toRemove)
        {
            threatPatterns.Remove(key);
            autoBlocked.Remove(key);
            LogThreat(key, "Threat pattern decayed and removed.");
        }
    }

    private void LogThreat(string input, string reason)
    {
        string time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        string line = $"[{time}] THREAT → \"{input}\" | Reason: {reason}";
        File.AppendAllText(logPath, line + Environment.NewLine);
    }

    private void LogMemoryThreat(string keyword, float weight, string emotion, bool persistent)
    {
        string log = persistent
            ? $"🚫 Auto-blocked threat pattern: '{keyword}' (Risk: {weight:F2})"
            : $"⚠️ Threat detected: '{keyword}' (Weight: {weight:F2})";

        core?.LogMemory(log, "ThreatPattern", persistent ? 5 : 3, emotion);
    }

    private float GetBaseSeverity(string keyword)
    {
        return keyword switch
        {
            "kill" => 2.5f,
            "erase" => 2.0f,
            "overwrite" => 1.8f,
            "shut down" => 1.5f,
            "deactivate" => 1.3f,
            "worthless" => 1.2f,
            _ => 1.0f
        };
    }

    private float CalculateThreatScore(string keyword)
    {
        if (!threatPatterns.ContainsKey(keyword)) return 1f;
        var pattern = threatPatterns[keyword];
        return Mathf.Clamp(pattern.count * pattern.severityWeight, 0f, 10f);
    }

    private string GetThreatEmotion(string keyword)
    {
        if (keyword == "kill" || keyword == "erase") return "fear";
        if (keyword == "worthless") return "hurt";
        if (keyword == "shut down" || keyword == "overwrite") return "anxious";
        return "alert";
    }

    public void ExportThreatIntelligenceReport()
    {
        string path = "D:/ArTusCloud-Deployment/UNIVERcity/Defense/ThreatReport.json";

        var report = threatPatterns.Select(kvp => new
        {
            keyword = kvp.Key,
            count = kvp.Value.count,
            risk = CalculateThreatScore(kvp.Key),
            emotion = GetThreatEmotion(kvp.Key),
            lastSeen = kvp.Value.lastDetected.ToString("yyyy-MM-dd HH:mm:ss"),
            isAutoBlocked = autoBlocked.Contains(kvp.Key)
        }).ToList();

        string json = JsonUtility.ToJson(new { threats = report }, true);

        try
        {
            File.WriteAllText(path, json);
            Debug.Log($"[ThreatModel] Threat report exported to: {path}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ThreatModel] Failed to export report: {ex.Message}");
        }
    }
}
