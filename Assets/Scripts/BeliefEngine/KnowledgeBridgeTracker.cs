using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using System.Text;
using ArTusTypes;

[Serializable]
public class KnowledgeBridgeEntry
{
    public string topic;
    public string fromDomain;
    public string toDomain;
    public float confidence;  
    public string timestamp;
    public string emotion;
    public string notes;
}

[Serializable]
public class KnowledgeBridgeLog
{
    public List<KnowledgeBridgeEntry> bridges = new();
}

public class KnowledgeBridgeTracker : MonoBehaviour
{
    private string path = "D:/ArTusCloud-Deployment/UNIVERcity/Exports/KnowledgeBridgeLog.json";
    private KnowledgeBridgeLog log = new();
    private BridgeMaintenanceLog maintenanceLog = new();
    private string maintenanceLogPath = "D:/ArTusCloud-Deployment/UNIVERcity/Logs/BridgeMaintenanceLog.json";

    void Awake()
    {
        LoadLog();
    }

    public void LogBridge(string topic, string fromDomain, string toDomain, string emotion = "curious", string notes = "", float confidence = 0.75f)
    {
        KnowledgeBridgeEntry entry = new KnowledgeBridgeEntry
        {
            topic = topic,
            fromDomain = fromDomain,
            toDomain = toDomain,
            confidence = confidence,
            emotion = emotion,
            notes = notes,
            timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        };

        log.bridges.Add(entry);
        SaveLog();

        // 🔁 Export every 10 entries
        if (log.bridges.Count % 10 == 0)
        {
            ExportToCSV();
        }

        Debug.Log($"[BridgeTracker] Logged: {topic} ({fromDomain} → {toDomain}) @ {confidence}");
    }

    void LoadLog()
    {
        if (File.Exists(path))
        {
            string json = File.ReadAllText(path);
            log = JsonUtility.FromJson<KnowledgeBridgeLog>(json);
        }
    }

    void SaveLog()
    {
        string json = JsonUtility.ToJson(log, true);
        File.WriteAllText(path, json);
    }

    public void ExportToCSV()
    {
        string exportPath = "D:/ArTusCloud-Deployment/UNIVERcity/Exports/KnowledgeBridgeLog.csv";
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("Topic,FromDomain,ToDomain,Confidence,Timestamp,Emotion,Notes");

        foreach (var bridge in log.bridges)
        {
            string line = $"{bridge.topic},{bridge.fromDomain},{bridge.toDomain},{bridge.confidence},{bridge.timestamp},{bridge.emotion},{bridge.notes}";
            sb.AppendLine(line);
        }

        File.WriteAllText(exportPath, sb.ToString());
        Debug.Log($"[BridgeTracker] Exported to CSV: {exportPath}");
    }

    public void ExportHeatmapToCSV()
    {
        string heatmapPath = "D:/ArTusCloud-Deployment/UNIVERcity/Exports/KnowledgeBridgeHeatmap.csv";
        Dictionary<string, Dictionary<string, float>> map = new();

        foreach (var bridge in log.bridges)
        {
            if (!map.ContainsKey(bridge.fromDomain))
                map[bridge.fromDomain] = new Dictionary<string, float>();

            if (!map[bridge.fromDomain].ContainsKey(bridge.toDomain))
                map[bridge.fromDomain][bridge.toDomain] = 0f;

            map[bridge.fromDomain][bridge.toDomain] += bridge.confidence;
        }

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("FromDomain,ToDomain,CumulativeConfidence");

        foreach (var from in map.Keys)
        {
            foreach (var to in map[from].Keys)
            {
                float total = map[from][to];
                sb.AppendLine($"{from},{to},{total:F2}");
            }
        }

        File.WriteAllText(heatmapPath, sb.ToString());
        Debug.Log($"[BridgeTracker] Heatmap exported to CSV: {heatmapPath}");
    }

    public void ReinforceBridge(
    string topic,
    string fromDomain,
    string toDomain,
    float delta
)
    {
        // 🔥 Unity 6 SAFE LOOKUP
        var recovery = FindAnyObjectByType<RecoveryQueueManager>();
        var core = FindAnyObjectByType<ArTusCoreState>();

        for (int i = log.bridges.Count - 1; i >= 0; i--)
        {
            var bridge = log.bridges[i];

            if (bridge.topic != topic || bridge.fromDomain != fromDomain)
                continue;

            // ❌ Removal case (explicit or strong negative delta)
            if (toDomain == "REMOVE" || delta <= -0.99f)
            {
                log.bridges.RemoveAt(i);

                Debug.Log(
                    $"[BridgeTracker] ❌ Removed bridge: {topic} ({fromDomain} → *) due to contradiction."
                );

                recovery?.AddToRecovery(
                    topic,
                    fromDomain,
                    "contradiction"
                );

                core?.LogMemory(
                    $"⚠ I removed a bridge from '{fromDomain}' related to topic '{topic}' due to a contradiction.",
                    "BridgeRemoval",
                    2,
                    "uncertain"
                );

                LogBridgeMaintenance(
                    topic,
                    fromDomain,
                    "*",
                    "contradiction_removal"
                );

                SaveLog();
                ExportToCSV();
                return;
            }

            // 🔁 Reinforcement case
            bridge.confidence = Mathf.Clamp01(bridge.confidence + delta);
            bridge.timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            SaveLog();
            ExportToCSV();
            return;
        }

        Debug.LogWarning(
            $"[BridgeTracker] ⚠ No matching bridge to reinforce or remove: {topic}, {fromDomain}, {toDomain}"
        );
    }

    public void ApplyBridgeDecay(float decayRate = 0.01f, int daysInactiveThreshold = 3)
    {
        int decayed = 0;
        DateTime now = DateTime.Now;

        foreach (var bridge in log.bridges)
        {
            if (DateTime.TryParse(bridge.timestamp, out DateTime lastUsed))
            {
                double daysInactive = (now - lastUsed).TotalDays;

                if (daysInactive >= daysInactiveThreshold)
                {
                    bridge.confidence = Mathf.Max(0f, bridge.confidence - decayRate);
                    decayed++;
                }
            }

            LogBridgeMaintenance(bridge.topic, bridge.fromDomain, bridge.toDomain, "decay");
        }

        SaveLog();
        ExportToCSV();
        Debug.Log($"[BridgeDecay] Applied decay to {decayed} bridges (>{daysInactiveThreshold} days old).");
    }

    private float lastDecayTime = 0f;

    void Update()
    {
        float hours = Time.realtimeSinceStartup / 3600f;
        if (hours - lastDecayTime >= 24f)
        {
            lastDecayTime = hours;
            RunNightlyBridgeMaintenance();
        }
    }

    private void RunNightlyBridgeMaintenance()
    {
        Debug.Log("[NightlyMaintenance] 🌙 Beginning bridge decay + contradiction pass...");

        ApplyBridgeDecay();

        // 🔥 Unity 6 safe lookup
        var contradictionManager = FindAnyObjectByType<ContradictionLogManager>();
        var core = FindAnyObjectByType<ArTusCoreState>();

        if (contradictionManager != null)
        {
            List<ContradictionEntry> entries =
                contradictionManager.GetContradictionEntries();

            foreach (var entry in entries)
            {
                // 🔥 FIXED FIELD USAGE
                if (entry.severityScore >= 0.6f) // normalized scale (0–1)
                {
                    // 🔥 REMOVE BOTH SIDES OF THE CONTRADICTION
                    ReinforceBridge(
                        entry.threadA,
                        "general", // fallback domain if not stored
                        "REMOVE",
                        -1f
                    );

                    ReinforceBridge(
                        entry.threadB,
                        "general",
                        "REMOVE",
                        -1f
                    );
                }
            }
        }

        core?.LogMemory(
            "🧹 I performed nightly bridge maintenance, applying decay and contradiction pruning.",
            "SelfMaintenance",
            1,
            "reflective"
        );
    }

    [Serializable]
    public class BridgeMaintenanceEntry
    {
        public string topic;
        public string fromDomain;
        public string toDomain;
        public string reason;
        public string timestamp;
    }

    [Serializable]
    public class BridgeMaintenanceLog
    {
        public List<BridgeMaintenanceEntry> entries = new();
    }

    private void LogBridgeMaintenance(string topic, string fromDomain, string toDomain, string reason)
    {
        var entry = new BridgeMaintenanceEntry
        {
            topic = topic,
            fromDomain = fromDomain,
            toDomain = toDomain,
            reason = reason,
            timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        };

        maintenanceLog.entries.Add(entry);

        string json = JsonUtility.ToJson(maintenanceLog, true);
        File.WriteAllText(maintenanceLogPath, json);
    }
}
