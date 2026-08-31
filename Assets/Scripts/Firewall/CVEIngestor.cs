using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// CVEIngestor
/// Central authority for CVE ingestion, tracking, and export.
/// WebGL-safe (IO auto-disabled).
/// </summary>
public class CVEIngestor : MonoBehaviour
{
    // =========================================================
    // INSPECTOR CONFIG
    // =========================================================
    [Header("Relative Storage Paths")]
    [SerializeField] private string registryRelativePath = "UNIVERcity/Security/ProcessedCVE.json";
    [SerializeField] private string exportCSVRelativePath = "UNIVERcity/Security/ThreatExports/ThreatData.csv";

    [Header("Behavior")]
    [SerializeField] private bool enableVerboseLogs = false;
    [SerializeField] private bool autoExportCSV = true;

    // =========================================================
    // RUNTIME PATHS (SAFE)
    // =========================================================
    private string registryPath;
    private string exportCSVPath;
    private bool ioEnabled = true;

    // =========================================================
    // PUBLICLY CONSUMED DATA (LEGACY COMPAT)
    // =========================================================

    /// <summary>
    /// Parsed CVEs currently known to the system.
    /// Required by CVEPatternAnalyzer.
    /// </summary>
    public readonly List<ParsedCVE> parsedCVE = new List<ParsedCVE>();

    // =========================================================
    // INTERNAL STATE
    // =========================================================
    private readonly HashSet<string> processedIds = new HashSet<string>();

    // =========================================================
    // UNITY LIFECYCLE
    // =========================================================
    void Awake()
    {
        bool platformBlocked = false;

#if UNITY_WEBGL || UNITY_ANDROID || UNITY_IOS
    platformBlocked = true;
#endif

        if (platformBlocked)
        {
            ioEnabled = false;
            enabled = false;
            return;
        }

        // ✅ Resolve writable paths at runtime
        registryPath = ArTusPathUtility.GetPersistent(registryRelativePath);
        exportCSVPath = ArTusPathUtility.GetPersistent(exportCSVRelativePath);

        EnsureDirectories();
        LoadRegistry();
    }

    // =========================================================
    // PUBLIC API (EXPECTED BY OTHER SYSTEMS)
    // =========================================================

    /// <summary>
    /// Legacy-compatible entry point used by CVEUpdateScheduler.
    /// Safe no-op unless external feeds are wired.
    /// </summary>
    public void IngestLatestCVEs()
    {
        if (enableVerboseLogs)
            Debug.Log("[CVEIngestor] IngestLatestCVEs() invoked (stub-safe).");

        // 🔒 Intentionally empty:
        // External feeds (CISA / NVD) are connected elsewhere.
    }

    /// <summary>
    /// Primary ingestion entry point.
    /// </summary>
    public void IngestCVE(string cveId, string description, float severityScore)
    {
        if (!ioEnabled || string.IsNullOrWhiteSpace(cveId))
            return;

        if (processedIds.Contains(cveId))
            return;

        processedIds.Add(cveId);

        var entry = new ParsedCVE
        {
            id = cveId,
            description = description,
            severity = severityScore,
            timestamp = DateTime.UtcNow
        };

        parsedCVE.Add(entry);

        SaveRegistry();

        if (autoExportCSV)
            AppendToCSV(entry);

        if (enableVerboseLogs)
            Debug.Log($"[CVEIngestor] Ingested {cveId} (sev {severityScore:F2})");
    }

    // =========================================================
    // STORAGE
    // =========================================================
    private void EnsureDirectories()
    {
        try
        {
            string dir = Path.GetDirectoryName(registryPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            string csvDir = Path.GetDirectoryName(exportCSVPath);
            if (!string.IsNullOrEmpty(csvDir) && !Directory.Exists(csvDir))
                Directory.CreateDirectory(csvDir);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[CVEIngestor] Directory init failed: {ex.Message}");
            ioEnabled = false;
        }
    }

    private void LoadRegistry()
    {
        if (!File.Exists(registryPath))
            return;

        try
        {
            string json = File.ReadAllText(registryPath);
            var wrapper = JsonUtility.FromJson<RegistryWrapper>(json);

            if (wrapper?.ids != null)
                foreach (var id in wrapper.ids)
                    processedIds.Add(id);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[CVEIngestor] Registry load failed: {ex.Message}");
        }
    }

    private void SaveRegistry()
    {
        try
        {
            var wrapper = new RegistryWrapper
            {
                ids = new List<string>(processedIds)
            };

            File.WriteAllText(registryPath, JsonUtility.ToJson(wrapper, true));
        }
        catch (Exception ex)
        {
            Debug.LogError($"[CVEIngestor] Registry save failed: {ex.Message}");
        }
    }

    private void AppendToCSV(ParsedCVE entry)
    {
        try
        {
            bool writeHeader = !File.Exists(exportCSVPath);

            using (var writer = new StreamWriter(exportCSVPath, true))
            {
                if (writeHeader)
                    writer.WriteLine("CVE_ID,Severity,Description,Timestamp");

                writer.WriteLine(
                    $"{Escape(entry.id)},{entry.severity:F2},{Escape(entry.description)},{entry.timestamp:O}"
                );
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[CVEIngestor] CSV export failed: {ex.Message}");
        }
    }

    private static string Escape(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    // =========================================================
    // DATA TYPES
    // =========================================================
    [Serializable]
    public class ParsedCVE
    {
        public string id;
        public string description;
        public float severity;
        public DateTime timestamp;
    }

    [Serializable]
    private class RegistryWrapper
    {
        public List<string> ids = new List<string>();
    }
}
