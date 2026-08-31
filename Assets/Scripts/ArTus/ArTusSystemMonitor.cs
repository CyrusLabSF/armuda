using UnityEngine;
using System;
using System.IO;
using System.Collections.Generic;
using Debug = UnityEngine.Debug;

public class ArTusSystemMonitor : MonoBehaviour
{
    private ArTusCoreState core;

    [Header("Monitor Settings")]
    public float updateInterval = 5f;
    private float timer = 0f;

    // Paths resolved at runtime (WebGL-safe)
    private string jsonOutputPath;
    private string csvOutputPath;

    private string lastEmotion = "";

    void Awake()
    {
        core = GetComponent<ArTusCoreState>();

        jsonOutputPath = ArTusPathUtility.GetPersistent(
            "UNIVERcity/Live/SystemHealth.json"
        );

        csvOutputPath = ArTusPathUtility.GetPersistent(
            "UNIVERcity/Exports/SystemHealthLog.csv"
        );

        // Ensure directories exist (safe on desktop, ignored on WebGL)
        TryCreateDirectory(jsonOutputPath);
        TryCreateDirectory(csvOutputPath);
    }

    void Update()
    {
        timer += Time.deltaTime;
        if (timer >= updateInterval)
        {
            SampleAndExport();
            timer = 0f;
        }
    }

    // =========================================================
    // SAMPLE + EXPORT
    // =========================================================
    private void SampleAndExport()
    {
        var data = CaptureStats();

        TryWriteJson(jsonOutputPath, data);
        TryExportToCsv(data);
        EmitSignals(data);
    }

    private SystemStats CaptureStats()
    {
        return new SystemStats
        {
            timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            cpuUsage = GetCpuUsageEstimate(),
            memoryUsedMB = GC.GetTotalMemory(false) / (1024f * 1024f),
            clarity = core?.GetAverageMemoryClarity() ?? 0f,
            confidence = core?.GetMostRecentBelief()?.confidenceScore ?? 0.5f,
            emotion = core?.GetCurrentEmotion() ?? "unknown",
            status = core?.GetLastMemorySummary() ?? "No recent memory.",
            armudaStatus = "virtual",
            univercityStatus = "virtual"
        };
    }

    // =========================================================
    // SIGNAL EMISSION
    // =========================================================
    private void EmitSignals(SystemStats stats)
    {
        core?.LogMemory(
            $"📊 Telemetry | CPU {stats.cpuUsage:F1}% RAM {stats.memoryUsedMB:F1}MB Clarity {stats.clarity:F2}",
            "SystemTelemetry",
            1,
            "neutral"
        );

        if (stats.emotion != lastEmotion)
        {
            core?.LogMemory(
                $"🔄 Emotion shift: {lastEmotion} → {stats.emotion}",
                "EmotionTelemetry",
                1,
                stats.emotion
            );
            lastEmotion = stats.emotion;
        }
    }

    // =========================================================
    // FILE HELPERS (WEBGL SAFE)
    // =========================================================
    private void TryWriteJson(string path, SystemStats data)
    {
        try
        {
            File.WriteAllText(path, JsonUtility.ToJson(data, true));
        }
        catch
        {
            // WebGL-safe: silently skip
        }
    }

    private void TryExportToCsv(SystemStats data)
    {
        try
        {
            bool exists = File.Exists(csvOutputPath);
            using StreamWriter writer = new(csvOutputPath, true);

            if (!exists)
                writer.WriteLine(
                    "Timestamp,CPU,Memory,Clarity,Confidence,Emotion,Status"
                );

            writer.WriteLine(
                $"\"{data.timestamp}\"," +
                $"{data.cpuUsage:F1}," +
                $"{data.memoryUsedMB:F1}," +
                $"{data.clarity:F2}," +
                $"{data.confidence:F2}," +
                $"{data.emotion}," +
                $"\"{data.status}\""
            );
        }
        catch
        {
            // WebGL-safe no-op
        }
    }

    private void TryCreateDirectory(string filePath)
    {
        try
        {
            string dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }
        catch
        {
            // WebGL-safe no-op
        }
    }

    // =========================================================
    // UTIL
    // =========================================================
    private float GetCpuUsageEstimate()
    {
        // Placeholder — WebGL cannot access real CPU metrics
        return UnityEngine.Random.Range(5f, 25f);
    }

    [Serializable]
    public class SystemStats
    {
        public string timestamp;
        public float cpuUsage;
        public float memoryUsedMB;
        public float clarity;
        public float confidence;
        public string emotion;
        public string status;
        public string armudaStatus;
        public string univercityStatus;
    }
}
