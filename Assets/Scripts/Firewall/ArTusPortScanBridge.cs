using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ArTusPortScanBridge
/// -------------------
/// • Imports port scan JSON results
/// • Logs memory + voice feedback
/// • Feeds ThreatPatternEngine safely
/// • Writes trail CSV (WebGL-safe)
///
/// ❌ Does NOT evaluate or advise
/// ❌ Does NOT resolve contradictions
/// </summary>
public class ArTusPortScanBridge : MonoBehaviour
{
    // =====================================================
    // PATH CONFIG (RELATIVE ONLY)
    // =====================================================

    [Header("Paths (Relative)")]
    [SerializeField]
    private string jsonRelativePath =
        "UNIVERcity/Logs_Outputs/PortScanResult.json";

    [SerializeField]
    private string exportTrailRelativePath =
        "UNIVERcity/Logs_Outputs/PortScanTrail.csv";

    // =====================================================
    // RESOLVED PATHS (RUNTIME)
    // =====================================================

    private string jsonPath;
    private string exportTrailPath;

    // =====================================================
    // DEPENDENCIES
    // =====================================================

    [Header("Dependencies")]
    public ArTusCoreState core;
    public ArTusSpeechResponder speech;
    public ArTusThreatPatternEngine patternEngine;

    // =====================================================
    // UNITY LIFECYCLE
    // =====================================================

    void Awake()
    {
        jsonPath =
            ArTusPathUtility.GetPersistent(jsonRelativePath);

        exportTrailPath =
            ArTusPathUtility.GetPersistent(exportTrailRelativePath);
    }

    // =====================================================
    // PUBLIC ENTRY
    // =====================================================

    public void ImportScan()
    {
#if UNITY_WEBGL
        Debug.LogWarning("[PortScanBridge] File IO disabled in WebGL.");
        return;
#else
        if (!File.Exists(jsonPath))
        {
            Debug.LogWarning("[PortScanBridge] No scan report found.");
            return;
        }

        string json = File.ReadAllText(jsonPath);
        ScanResult result = JsonUtility.FromJson<ScanResult>(json);

        if (result == null)
        {
            Debug.LogWarning("[PortScanBridge] Failed to parse scan report.");
            return;
        }

        int severityScore = result.flaggedPorts.Count;
        string emotion = GetSeverityEmotion(severityScore);

        string memoryEntry =
            $"🛡 Port Scan ({result.timestamp})\n" +
            $"Open: {string.Join(", ", result.openPorts)}\n" +
            $"Flagged: {string.Join(", ", result.flaggedPorts)}\n" +
            $"Severity Score: {severityScore}\n" +
            $"Emotional State: {emotion}";

        core?.LogMemory(memoryEntry, "PortScan", 3, emotion);

        if (severityScore > 0)
        {
            speech?.RequestSpeak(
                $"Threat level {emotion}. {severityScore} suspicious ports detected.",
                ArTusSpeechResponder.SpeechCategory.System
            );
        }
        else
        {
            speech?.RequestSpeak(
                "No suspicious ports found. System appears stable.",
                ArTusSpeechResponder.SpeechCategory.System
            );
        }

        // =================================================
        // THREAT PATTERN FEED (PASSIVE)
        // =================================================

        if (patternEngine != null && result.flaggedPorts.Count > 0)
        {
            EnsureCsv(
                exportTrailPath,
                "Timestamp,Port,Category,Belief,Contradiction\n"
            );

            foreach (int port in result.flaggedPorts)
            {
                string portLabel = port.ToString();

                // Passive observation only
                patternEngine.ObservePattern(portLabel, "port");

                File.AppendAllText(
                    exportTrailPath,
                    $"{DateTime.UtcNow:o},{portLabel},port,Observed risk,false\n"
                );

                core?.LogMemory(
                    $"⚠️ Port {portLabel} flagged for review.",
                    "PortScanFlag",
                    2,
                    "thinking"
                );
            }
        }

        core?.LogMemory(
            $"🧠 PortScanBridge reflection complete for {result.openPorts.Count} ports.",
            "PortScanBridgeMeta",
            2,
            "thinking"
        );

        Debug.Log("[PortScanBridge] Scan data successfully imported.");
#endif
    }

    // =====================================================
    // HELPERS
    // =====================================================

#if !UNITY_WEBGL
    private static void EnsureCsv(string path, string header)
    {
        string dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        if (!File.Exists(path))
            File.WriteAllText(path, header);
    }
#endif

    private string GetSeverityEmotion(int score)
    {
        if (score >= 5) return "alert";
        if (score >= 2) return "concerned";
        return "calm";
    }

    // =====================================================
    // DATA MODEL
    // =====================================================

    [Serializable]
    public class ScanResult
    {
        public string timestamp;
        public List<int> openPorts = new();
        public List<int> flaggedPorts = new();
    }
}
