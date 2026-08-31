using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System;

public class ArTusExportManager : MonoBehaviour
{
    public string exportPath = "D:/ArTusCloud-Deployment/UNIVERcity/ThreatLogs/CVE_Export.json";
    private string csvPath = "D:/ArTusCloud-Deployment/UNIVERcity/Logs/CVE_Export.csv";

    [System.Serializable]
    public class CVEExportEntry
    {
        public string cveID;
        public string description;
        public string pattern;
        public string subcategory;
        public string category;
        public string superclass;
        public float score;
        public string emotion;
        public string recommendation;
        public string timestamp;
    }

    private List<CVEExportEntry> exportLog = new();
    private ArTusCoreState core;

    void Start()
    {
        core = GetComponent<ArTusCoreState>();

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(csvPath));
            if (!File.Exists(csvPath))
                File.WriteAllText(csvPath, "Timestamp,CVE_ID,Pattern,Category,Score,Emotion,Recommendation\n");
        }
        catch (IOException ex)
        {
            Debug.LogError($"[ExportManager] Failed to prepare CSV log: {ex.Message}");
        }
    }

    public void AddEntry(CVEExportEntry entry)
    {
        exportLog.Add(entry);
        SaveExport();

        if (entry.score >= 0.8f)
        {
            core?.LogMemory($"⚠ CVE Threat '{entry.cveID}' identified in pattern '{entry.pattern}' — Priority: {entry.score:F2}",
                "CVEThreat", 3, entry.emotion, entry.pattern);

            try
            {
                File.AppendAllText(csvPath,
                    $"{entry.timestamp},{entry.cveID},{entry.pattern},{entry.category},{entry.score:F2},{entry.emotion},{entry.recommendation.Replace(",", ";")}\n");
            }
            catch (IOException ex)
            {
                Debug.LogError($"[ExportManager] Failed to append to CSV: {ex.Message}");
            }
        }
    }

    private void SaveExport()
    {
        try
        {
            if (!Directory.Exists(Path.GetDirectoryName(exportPath)))
                Directory.CreateDirectory(Path.GetDirectoryName(exportPath));

            string json = JsonUtility.ToJson(new CVEExportWrapper { entries = exportLog }, true);
            File.WriteAllText(exportPath, json);
            Debug.Log($"[Export] CVE data exported to: {exportPath}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[Export Error] {e.Message}");
        }
    }

    [System.Serializable]
    private class CVEExportWrapper
    {
        public List<CVEExportEntry> entries;
    }

    public void ExportTrailMap(Dictionary<string, int> trailMap)
    {
        string trailPath = "D:/ArTusCloud-Deployment/UNIVERcity/TrailMap/TrailMap.json";

        try
        {
            if (!Directory.Exists(Path.GetDirectoryName(trailPath)))
                Directory.CreateDirectory(Path.GetDirectoryName(trailPath));

            string json = JsonUtility.ToJson(new TrailWrapper { trails = trailMap }, true);
            File.WriteAllText(trailPath, json);
            Debug.Log($"[TrailMap] Exported to {trailPath}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[TrailMap Error] {e.Message}");
        }
    }

    [System.Serializable]
    private class TrailWrapper
    {
        public Dictionary<string, int> trails;
    }

    public void ResetExportLog()
    {
        exportLog.Clear();
        SaveExport();
        Debug.Log("[ExportManager] CVE export log cleared.");
    }

    public string ExportSummaryLine(CVEExportEntry entry)
    {
        return $"{entry.timestamp} | CVE: {entry.cveID} | Pattern: {entry.pattern} | Score: {entry.score:F2}";
    }
}
