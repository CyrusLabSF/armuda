using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

[System.Serializable]
public class ChatBeliefTrace
{
    public string beliefID;
    public string originTrail;
    public float confidence;
    public string dominantEmotion;
    public string lastUpdated;
    public int usageCount = 1;
}

[System.Serializable]
public class ChatBeliefLogEntry
{
    public string userQuery;
    public string responseSummary;
    public List<ChatBeliefTrace> beliefsUsed = new();
    public string timestamp;
}

[System.Serializable]
public class ChatBeliefLogWrapper
{
    public List<ChatBeliefLogEntry> logEntries = new();
}

[RequireComponent(typeof(ArTusCoreState))]
public class ArTusChatBeliefTracer : MonoBehaviour
{
    private ArTusCoreState core;
    private string jsonLogPath = "D:/ArTusCloud-Deployment/UNIVERcity/Exports/ChatBeliefLogs.json";
    private string csvLogPath = "D:/ArTusCloud-Deployment/UNIVERcity/Exports/ChatBeliefUsage.csv";
    private ChatBeliefLogWrapper beliefLog = new();

    [Header("Behavior Toggles")]
    public bool enableReflection = false;

    void Start()
    {
        core = GetComponent<ArTusCoreState>();
        LoadExistingLog();

        try
        {
            if (!File.Exists(csvLogPath))
                File.WriteAllText(csvLogPath, "Timestamp,Query,Belief,Confidence,Emotion,TrailID,UsedBefore\n");
        }
        catch (IOException ex)
        {
            Debug.LogError($"[ChatBeliefTracer] Failed to initialize CSV: {ex.Message}");
        }
    }

    private void LoadExistingLog()
    {
        try
        {
            if (File.Exists(jsonLogPath))
            {
                string json = File.ReadAllText(jsonLogPath);
                beliefLog = JsonUtility.FromJson<ChatBeliefLogWrapper>(json) ?? new ChatBeliefLogWrapper();
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ChatBeliefTracer] Failed to load JSON log: {ex.Message}");
            beliefLog = new ChatBeliefLogWrapper();
        }
    }

    public void LogChatBeliefUsage(string query, string response)
    {
        var usedBeliefs = core.beliefs
            .Where(kvp => query.ToLower().Contains(kvp.Key.ToLower()))
            .Select(kvp => new ChatBeliefTrace
            {
                beliefID = kvp.Key,
                originTrail = kvp.Value.relatedTrails.FirstOrDefault() ?? "unknown",
                confidence = kvp.Value.confidenceScore,
                dominantEmotion = kvp.Value.dominantEmotion,
                lastUpdated = kvp.Value.lastUpdated
            }).ToList();

        foreach (var trace in usedBeliefs)
        {
            if (trace.confidence < 0.4f && enableReflection)
            {
                string memo = $"🔁 Belief '{trace.beliefID}' reused during chat with low confidence ({trace.confidence:F2}).";
                core.LogMemory(memo, "BeliefReuse", 2, "uncertain", trace.originTrail);
                core.ScheduleReflection(trace.beliefID, "uncertain");
            }

            try
            {
                File.AppendAllText(csvLogPath,
                    $"{DateTime.Now},{query},{trace.beliefID},{trace.confidence:F2},{trace.dominantEmotion},{trace.originTrail},{trace.lastUpdated}\n");
            }
            catch (IOException ex)
            {
                Debug.LogError($"[ChatBeliefTracer] Failed to write CSV log: {ex.Message}");
            }
        }

        ChatBeliefLogEntry entry = new()
        {
            userQuery = query,
            responseSummary = response.Length > 120 ? response.Substring(0, 120) + "..." : response,
            beliefsUsed = usedBeliefs,
            timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        };

        beliefLog.logEntries.Add(entry);
        SaveLog();
    }

    private void SaveLog()
    {
        try
        {
            string json = JsonUtility.ToJson(beliefLog, true);
            File.WriteAllText(jsonLogPath, json);
            Debug.Log($"[ChatBeliefTracer] Belief trace saved: {jsonLogPath}");
        }
        catch (IOException ex)
        {
            Debug.LogError($"[ChatBeliefTracer] Failed to save JSON: {ex.Message}");
        }
    }
}
