using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[Serializable]
public class RecoveryEntry
{
    public string topic;
    public string domain;
    public string reason;
    public string timestamp;
}

[Serializable]
public class RecoveryQueueWrapper
{
    public List<RecoveryEntry> queue = new();
}

public class RecoveryQueueManager : MonoBehaviour
{
    private string queuePath = "D:/ArTusCloud-Deployment/UNIVERcity/Recovery/RecoveryQueue.json";
    private string domainPath = "D:/ArTusCloud-Deployment/UNIVERcity/Domains/";
    private string exportPath = "D:/ArTusCloud-Deployment/UNIVERcity/Exports/RecoveryQueue.csv";

    public RecoveryQueueWrapper wrapper = new();
    private float lastRecoveryTime = 0f;

    void Start()
    {
        if (File.Exists(queuePath))
        {
            string json = File.ReadAllText(queuePath);
            wrapper = JsonUtility.FromJson<RecoveryQueueWrapper>(json) ?? new RecoveryQueueWrapper();
        }
        else
        {
            wrapper = new RecoveryQueueWrapper();
        }
    }

    void Update()
    {
        float hours = Time.realtimeSinceStartup / 3600f;

        // ♻️ Weekly recovery cycle (7 in-game days)
        if (hours - lastRecoveryTime >= 168f)
        {
            lastRecoveryTime = hours;
            ProcessRecoveryQueue();
        }

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.Y))
        {
            ExportRecoveryQueueToCSV();
        }
#endif
    }

    // 🧠 Add a topic to be recovered later
    public void AddToRecovery(string topic, string domain, string reason = "unspecified")
    {
        RecoveryEntry entry = new RecoveryEntry
        {
            topic = topic,
            domain = domain,
            reason = reason,
            timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        };

        wrapper.queue.Add(entry);
        SaveQueue();

        Debug.Log($"[RecoveryQueue] 🧩 Logged: {topic} ({domain}) | Reason: {reason}");
    }

    private void SaveQueue()
    {
        string json = JsonUtility.ToJson(wrapper, true);
        File.WriteAllText(queuePath, json);
    }

    // ♻️ Attempt to heal contradictions or gaps
    public void ProcessRecoveryQueue()
    {
        Debug.Log("[Recovery] ♻️ Attempting to recover topics...");

        var ingestor = FindAnyObjectByType<ArTusDomainIngestor>();
        var core = FindAnyObjectByType<ArTusCoreState>();

        if (ingestor == null)
        {
            Debug.LogWarning("[Recovery] ❌ DomainIngestor not found.");
            return;
        }

        if (core == null)
        {
            Debug.LogWarning("[Recovery] ❌ CoreState not found.");
            return;
        }

        int recovered = 0;

        foreach (var entry in wrapper.queue)
        {
            string safeName = entry.topic.Replace(" ", "_");
            string filePath = Path.Combine(domainPath, entry.domain, $"{safeName}.txt");

            if (!File.Exists(filePath))
                continue;

            ingestor.IngestTopic(
                entry.topic,
                entry.domain,
                "resilient",
                3,
                "recovery-cycle"
            );

            core.LogMemory(
                $"♻️ Re-ingested topic '{entry.topic}' from '{entry.domain}' | Reason: {entry.reason}",
                "RecoveryCycle",
                2,
                "resilient"
            );

            recovered++;
        }

        // 🔥 UPDATED VOICE SYSTEM (IMPORTANT)
        core.QueueVoice(
            $"Recovery complete. I reprocessed {recovered} of {wrapper.queue.Count} queued topics."
        );

        Debug.Log($"[Recovery] ✅ Recovered {recovered}/{wrapper.queue.Count} topics.");
    }

    // 📤 Export current queue for review
    public void ExportRecoveryQueueToCSV()
    {
        List<string> lines = new() { "Topic,Domain,Reason,Timestamp" };

        foreach (var entry in wrapper.queue)
        {
            string cleanReason = entry.reason.Replace(",", ";");
            lines.Add($"{entry.topic},{entry.domain},{cleanReason},{entry.timestamp}");
        }

        File.WriteAllLines(exportPath, lines);
        Debug.Log($"[RecoveryExport] ✅ Exported {wrapper.queue.Count} items → {exportPath}");
    }
}
