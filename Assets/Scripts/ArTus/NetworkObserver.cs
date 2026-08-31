using UnityEngine;
using System;
using System.IO;
using Debug = UnityEngine.Debug;

public class ArTusNetworkObserver : MonoBehaviour
{
    private ArTusCoreState core;

    [Header("Detection Thresholds")]
    public float suspiciousTrafficThreshold = 0.6f;
    public float highSeverityThreshold = 0.85f;

    [Header("Telemetry")]
    [Tooltip("CSV export location (non-WebGL only)")]
    private string csvExportPath;

    [Header("Noise Control")]
    public float duplicateCooldownSeconds = 10f;

    private string lastEventHash = "";
    private double lastEventTime = 0;


    void Awake()
    {
#if UNITY_WEBGL
        // 🔒 HARD STOP — WebGL must never observe network state
        enabled = false;
        return;
#else
        core = GetComponent<ArTusCoreState>();

        csvExportPath = ArTusPathUtility.GetPersistent(
            "UNIVERcity/Defense/NetworkObservations.csv"
        );

        EnsureCsvHeader();
#endif
    }

    // --------------------------------------------------
    // OBSERVE ONLY — NO DECISIONS / NO CONTROL
    // --------------------------------------------------
    public void ObserveNetworkEvent(
        string source,
        string description,
        float severityScore
    )
    {
#if UNITY_WEBGL
        return;
#else
        if (!enabled)
            return;

        if (string.IsNullOrWhiteSpace(description))
            return;

        // 🔇 Noise suppression
        string hash =
            $"{source}:{description}:{Mathf.RoundToInt(severityScore * 100)}";

        if (hash == lastEventHash &&
            Time.time - lastEventTime < duplicateCooldownSeconds)
            return;

        lastEventHash = hash;
        lastEventTime = Time.time;

        string emotion = ResolveEmotion(severityScore);

        core?.LogMemory(
            $"🌐 Network activity observed\n" +
            $"Source: {source}\n" +
            $"Details: {description}\n" +
            $"Severity: {severityScore:F2}",
            "NetworkObservation",
            3,
            emotion
        );

        AppendCsv(source, description, severityScore, emotion);

        Debug.Log(
            $"[NetworkObserver] {source} | {description} | Severity {severityScore:F2}"
        );
#endif
    }

    // --------------------------------------------------
    // PASSIVE EMOTION MAPPING
    // --------------------------------------------------
    private string ResolveEmotion(float severity)
    {
        if (severity >= highSeverityThreshold)
            return "alert";

        if (severity >= suspiciousTrafficThreshold)
            return "concerned";

        return "calm";
    }

    // --------------------------------------------------
    // CSV EXPORT (NON-WEBGL ONLY)
    // --------------------------------------------------
    private void EnsureCsvHeader()
    {
        if (string.IsNullOrEmpty(csvExportPath))
            return;

        string dir = Path.GetDirectoryName(csvExportPath);
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        if (!File.Exists(csvExportPath))
        {
            File.WriteAllText(
                csvExportPath,
                "Timestamp,Source,Description,Severity,Emotion\n"
            );
        }
    }

    private void AppendCsv(
        string source,
        string description,
        float severity,
        string emotion
    )
    {
        if (string.IsNullOrEmpty(csvExportPath))
            return;

        File.AppendAllText(
            csvExportPath,
            $"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}," +
            $"{Escape(source)}," +
            $"{Escape(description)}," +
            $"{severity:F2}," +
            $"{emotion}\n"
        );
    }

    private string Escape(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "\"\"";

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }
}
