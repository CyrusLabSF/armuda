using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ArTus Download Agent — Hi-Class Autonomous Reporter
/// ---------------------------------------------------
/// • Logs ingestion lifecycle events
/// • Tracks sessions, stages, and progress
/// • CSV + structured process awareness
///
/// ❌ Does NOT decide retries
/// ❌ Does NOT trigger ingestion
/// ❌ Does NOT mutate beliefs
/// </summary>
public class ArTusDownloadAgent : MonoBehaviour
{
    [Header("Paths")]
    [SerializeField]
    private string exportPathRelative = "UNIVERcity/Logs_Outputs/DownloadFeed.csv";

    [Header("Safety")]
    [SerializeField]
    private int maxQueuedWrites = 500;

    private string exportPath;
    private string exportDirectory;

    public enum DownloadStage
    {
        Requested,
        Fetching,
        Received,
        Persisted,
        Failed,
        Retrying,
        Completed
    }

    [Serializable]
    public class DownloadEvent
    {
        public string sessionId;
        public string source;
        public string topic;
        public string detail;
        public DownloadStage stage;
        public int progress;
        public DateTime timestamp;
    }

    private readonly Queue<DownloadEvent> writeQueue = new();
    private bool isWriting;
    private string currentSessionId;

    private void Awake()
    {
        exportPath = ArTusPathUtility.GetPersistent(exportPathRelative);
        exportDirectory = Path.GetDirectoryName(exportPath);

        try
        {
            PrepareExportPath();
            EnsureHeader();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[DownloadAgent] Initialization failed: {ex.Message}");
        }
    }

    private void Update()
    {
        if (!isWriting && writeQueue.Count > 0)
            StartCoroutine(ProcessWriteQueue());
    }

    private void PrepareExportPath()
    {
        if (!string.IsNullOrEmpty(exportDirectory) && !Directory.Exists(exportDirectory))
        {
            Directory.CreateDirectory(exportDirectory);
            Debug.Log($"[DownloadAgent] Created directory: {exportDirectory}");
        }
    }

    public string BeginSession(string source, string topic)
    {
        currentSessionId = Guid.NewGuid().ToString("N");

        EnqueueEvent(new DownloadEvent
        {
            sessionId = currentSessionId,
            source = Sanitize(source, "unknown"),
            topic = Sanitize(topic, "unknown"),
            detail = "Download session started",
            stage = DownloadStage.Requested,
            progress = 0,
            timestamp = DateTime.UtcNow
        });

        return currentSessionId;
    }

    public void EndSession(string detail = "Session completed")
    {
        if (string.IsNullOrEmpty(currentSessionId))
            return;

        EnqueueEvent(new DownloadEvent
        {
            sessionId = currentSessionId,
            source = "system",
            topic = "session",
            detail = Sanitize(detail, "Session completed"),
            stage = DownloadStage.Completed,
            progress = 100,
            timestamp = DateTime.UtcNow
        });

        currentSessionId = null;
    }

    public void Report(
        string source,
        string topic,
        string detail,
        DownloadStage stage,
        int progress = 0
    )
    {
        EnqueueEvent(new DownloadEvent
        {
            sessionId = string.IsNullOrEmpty(currentSessionId) ? "unspecified" : currentSessionId,
            source = Sanitize(source, "unknown"),
            topic = Sanitize(topic, "unknown"),
            detail = Sanitize(detail, ""),
            stage = stage,
            progress = Mathf.Clamp(progress, 0, 100),
            timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Legacy-compatible export path.
    /// Preserves the old signature but maps it into the current CSV schema.
    /// </summary>
    public void ExportFeedEntry(
        string source,
        string id,
        string detail,
        string type,
        string stageName,
        string progressCount
    )
    {
        if (!Enum.TryParse(stageName, true, out DownloadStage parsedStage))
            parsedStage = DownloadStage.Fetching;

        int parsedProgress = 0;
        int.TryParse(progressCount, out parsedProgress);

        EnqueueEvent(new DownloadEvent
        {
            sessionId = string.IsNullOrEmpty(currentSessionId) ? Sanitize(id, "legacy") : currentSessionId,
            source = Sanitize(source, "legacy"),
            topic = Sanitize(type, "legacy"),
            detail = Sanitize(detail, ""),
            stage = parsedStage,
            progress = Mathf.Clamp(parsedProgress, 0, 100),
            timestamp = DateTime.UtcNow
        });
    }

    private void EnqueueEvent(DownloadEvent ev)
    {
        if (writeQueue.Count >= maxQueuedWrites)
        {
            Debug.LogWarning("[DownloadAgent] Write queue full. Dropping oldest event.");
            writeQueue.Dequeue();
        }

        writeQueue.Enqueue(ev);
    }

    private IEnumerator ProcessWriteQueue()
    {
        isWriting = true;

        while (writeQueue.Count > 0)
        {
            WriteEvent(writeQueue.Dequeue());
            yield return null;
        }

        isWriting = false;
    }

    private void WriteEvent(DownloadEvent ev)
    {
        try
        {
            PrepareExportPath();
            EnsureHeader();

            string line = string.Join(",",
                Csv(ev.timestamp.ToString("o")),
                Csv(ev.sessionId),
                Csv(ev.source),
                Csv(ev.topic),
                Csv(ev.stage.ToString()),
                Csv(ev.progress.ToString()),
                Csv(ev.detail)
            );

            File.AppendAllText(exportPath, line + Environment.NewLine);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[DownloadAgent] Write failed: {ex.Message}");
        }
    }

    private void EnsureHeader()
    {
        if (File.Exists(exportPath))
            return;

        string header = "timestamp,sessionId,source,topic,stage,progress,detail";
        File.WriteAllText(exportPath, header + Environment.NewLine);
    }

    private static string Sanitize(string value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private static string Csv(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "\"\"";

        value = value.Replace("\"", "\"\"");

        if (value.Contains(",") || value.Contains("\"") || value.Contains("\n") || value.Contains("\r"))
            return $"\"{value}\"";

        return value;
    }
}