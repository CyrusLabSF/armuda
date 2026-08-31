using UnityEngine;
using System.Collections.Generic;
using System;
using System.IO;

/// <summary>
/// Hi-Class Threat Explainer
/// Produces structured explanations for detected threats
/// No speech, no emotion, no memory logging, no consent logic
/// WebGL-safe
/// </summary>
public class ArTusThreatExplainer : MonoBehaviour
{
    // --------------------------------------------------
    // PORT → THREAT MAP (STATIC DATA — SAFE)
    // --------------------------------------------------
    private readonly Dictionary<int, string> portCveMap = new()
    {
        { 4444, "Metasploit handler — CVE-2022-1388: remote code execution exploit vector." },
        { 31337, "Back Orifice — legacy trojan port used in historical RCE payloads." },
        { 6667, "IRC — commonly abused in denial-of-service scenarios." },
        { 12345, "NetBus — legacy RAT backdoor port enabling remote access." },
        { 54321, "Linux backdoor socket behavior linked to legacy CVEs." }
    };

    // --------------------------------------------------
    // PATHS (DEFERRED — WEBGL SAFE)
    // --------------------------------------------------
    [Header("Export Paths")]
    [SerializeField]
    private string csvRelativePath =
        "UNIVERcity/Defense/ThreatExplainLog.csv";

    private string csvExportPath;

    // --------------------------------------------------
    // DATA MODEL
    // --------------------------------------------------
    [Serializable]
    public class ThreatExplanation
    {
        public string timestamp;
        public string type;
        public int port;
        public string summary;
        public string severity;
        public bool knownThreat;
    }

    // --------------------------------------------------
    // LIFECYCLE
    // --------------------------------------------------
    void Awake()
    {
        // ✅ Resolve persistent path at runtime only
        csvExportPath = ArTusPathUtility.GetPersistent(csvRelativePath);

        string dir = Path.GetDirectoryName(csvExportPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        if (!File.Exists(csvExportPath))
        {
            File.WriteAllText(
                csvExportPath,
                "Timestamp,Type,Port,KnownThreat,Severity,Summary\n"
            );
        }
    }

    // --------------------------------------------------
    // PUBLIC API
    // --------------------------------------------------
    public ThreatExplanation ExplainPortThreat(int port)
    {
        bool known = portCveMap.TryGetValue(port, out string explanation);

        if (!known)
            explanation = "Port is not mapped to a known CVE. Further investigation recommended.";

        string severity = known ? "High" : "Unknown";

        var record = new ThreatExplanation
        {
            timestamp = DateTime.UtcNow.ToString("o"),
            type = "Port",
            port = port,
            summary = explanation,
            severity = severity,
            knownThreat = known
        };

        Export(record);
        return record;
    }

    public ThreatExplanation ExplainGenericThreat(string message)
    {
        var record = new ThreatExplanation
        {
            timestamp = DateTime.UtcNow.ToString("o"),
            type = "General",
            port = -1,
            summary = message,
            severity = "Info",
            knownThreat = false
        };

        Export(record);
        return record;
    }

    // --------------------------------------------------
    // LEGACY COMPATIBILITY SHIMS
    // --------------------------------------------------
    public ThreatExplanation ExplainThreat(int port)
    {
        return ExplainPortThreat(port);
    }

    public ThreatExplanation ExplainThreat(string threat)
    {
        return ExplainGenericThreat(threat);
    }

    // --------------------------------------------------
    // EXPORT
    // --------------------------------------------------
    private void Export(ThreatExplanation e)
    {
        File.AppendAllText(
            csvExportPath,
            $"{e.timestamp}," +
            $"{e.type}," +
            $"{e.port}," +
            $"{e.knownThreat}," +
            $"{e.severity}," +
            $"{Escape(e.summary)}{Environment.NewLine}"
        );
    }

    // --------------------------------------------------
    // UTIL
    // --------------------------------------------------
    private static string Escape(string input)
    {
        if (string.IsNullOrEmpty(input))
            return "\"\"";

        return $"\"{input.Replace("\"", "\"\"")}\"";
    }
}
