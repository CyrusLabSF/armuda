using UnityEngine;
using System;
using System.IO;

public class ArTusSystemBaseline : MonoBehaviour
{
    [Header("Baseline Settings")]
    [Tooltip("Relative path under persistent data")]
    public string csvRelativePath = "UNIVERcity/System/SystemBaseline.csv";

    public float severityTriggerThreshold = 1.5f;
    public float sampleIntervalSeconds = 5f;   // 🔒 throttle

    private BaselineSnapshot lastSnapshot;
    private float lastSampleTime;
    private bool baselineInitialized;

    private string CsvExportPath =>
        ArTusPathUtility.GetPersistent(csvRelativePath);

    // ==============================
    // UNITY LIFECYCLE
    // ==============================
    void Start()
    {
        EnsureCsvHeader();

        lastSnapshot = CaptureSnapshot();
        baselineInitialized = true;
        lastSampleTime = Time.time;
    }

    void Update()
    {
        if (!baselineInitialized)
            return;

        if (Time.time - lastSampleTime < sampleIntervalSeconds)
            return;

        lastSampleTime = Time.time;

        BaselineSnapshot current = CaptureSnapshot();
        float severity = CalculateSeverity(current, lastSnapshot);

        if (severity >= severityTriggerThreshold)
        {
            ExportToCsv(CsvExportPath, current, lastSnapshot, severity);
            lastSnapshot = current;

            // 🔮 Future hooks:
            // core?.LogMemory(...)
            // emotion?.RegisterThreatPulse(...)
            // speech?.Notify(...)
        }
    }

    // ==============================
    // SNAPSHOT CAPTURE
    // ==============================
    private BaselineSnapshot CaptureSnapshot()
    {
        return new BaselineSnapshot
        {
            cpuUsagePercent = GetCpuUsage(),
            ramUsageMB = GetRamUsage(),
            openPortCount = GetOpenPortCount()
        };
    }

    // ==============================
    // SEVERITY LOGIC
    // ==============================
    private float CalculateSeverity(BaselineSnapshot current, BaselineSnapshot previous)
    {
        float cpuDelta = Mathf.Abs(current.cpuUsagePercent - previous.cpuUsagePercent);
        float ramDelta = Mathf.Abs(current.ramUsageMB - previous.ramUsageMB);
        float portDelta = Mathf.Abs(current.openPortCount - previous.openPortCount);

        // Hi-Class weighted model
        return
            (cpuDelta * 0.4f) +
            (ramDelta * 0.3f) +
            (portDelta * 0.3f);
    }

    // ==============================
    // CSV EXPORT (WEBGL SAFE)
    // ==============================
    private static void ExportToCsv(
        string csvPath,
        BaselineSnapshot current,
        BaselineSnapshot previous,
        float severity)
    {
#if UNITY_WEBGL
        // WebGL cannot write files — intentionally no-op
        return;
#else
        using StreamWriter writer = new StreamWriter(csvPath, true);
        writer.WriteLine(
            $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}," +
            $"{Mathf.Abs(current.cpuUsagePercent - previous.cpuUsagePercent):F1}," +
            $"{Mathf.Abs(current.ramUsageMB - previous.ramUsageMB):F1}," +
            $"{Mathf.Abs(current.openPortCount - previous.openPortCount)}," +
            $"{severity:F2}"
        );
#endif
    }

    private void EnsureCsvHeader()
    {
#if UNITY_WEBGL
        return;
#else
        if (!File.Exists(CsvExportPath))
        {
            string dir = Path.GetDirectoryName(CsvExportPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(
                CsvExportPath,
                "Timestamp,CPU_Delta,RAM_Delta,Port_Delta,Severity\n"
            );
        }
#endif
    }

    // ==============================
    // SYSTEM METRICS (SAFE STUBS)
    // ==============================
    private float GetCpuUsage()
    {
        // Placeholder — replace with native telemetry later
        return UnityEngine.Random.Range(1f, 12f);
    }

    private float GetRamUsage()
    {
        return UnityEngine.Random.Range(600f, 2400f);
    }

    private int GetOpenPortCount()
    {
        return UnityEngine.Random.Range(0, 10);
    }

    // ==============================
    // DATA STRUCT
    // ==============================
    [Serializable]
    public struct BaselineSnapshot
    {
        public float cpuUsagePercent;
        public float ramUsageMB;
        public int openPortCount;
    }
}
