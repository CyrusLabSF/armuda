using UnityEngine;
using System;
using System.IO;
using System.Collections.Generic;

/// <summary>
/// CyberSpaceManager
/// Orchestrates ArTus’s external interactions as a diplomatic narrative.
/// WebGL-safe: persistent paths, no threaded IO, no absolute paths.
/// </summary>
public class CyberSpaceManager : MonoBehaviour
{
    private ArTusCoreState core;

    private string diplomaticTrailPath;
    private string diplomaticTrailCsv;

    private const int MAX_IN_MEMORY = 3000;

    [Serializable]
    private class DiplomaticEntry
    {
        public string timestamp;
        public string source;
        public string action;
        public string detail;
        public string emotion;
    }

    [Serializable]
    private class DiplomaticWrapper
    {
        public List<DiplomaticEntry> entries = new();
    }

    private List<DiplomaticEntry> diplomaticLog = new();

    void Awake()
    {
        core = GetComponent<ArTusCoreState>();

        diplomaticTrailPath =
            ArTusPathUtility.GetPersistent("UNIVERcity/Diplomacy/DiplomaticTrail.json");

        diplomaticTrailCsv =
            ArTusPathUtility.GetPersistent("UNIVERcity/Diplomacy/DiplomaticTrail.csv");

        EnsureDirectories();   // 🔒 REQUIRED
        EnsureCsvHeader();     // now safe
        LoadExisting();        // now safe
    }

    // ==================================================
    // CORE DIPLOMATIC LOGGING
    // ==================================================
    public void RegisterDiplomaticEvent(
        string source,
        string action,
        string detail,
        string emotion = "neutral")
    {
        var entry = new DiplomaticEntry
        {
            timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
            source = source,
            action = action,
            detail = detail,
            emotion = emotion
        };

        diplomaticLog.Add(entry);
        PruneMemory();

        core?.LogMemory(
            $"🌐 Diplomatic Event: {source} → {action} | {detail}",
            "DiplomaticTrail",
            2,
            emotion
        );

        SaveJson();
        AppendCsv(entry);

        Debug.Log($"[CyberSpaceManager] {source}: {action} | {detail}");
    }

    // ==================================================
    // DIPLOMATIC ACTIONS
    // ==================================================
    public void NegotiateIngestion(string domain, string source)
    {
        RegisterDiplomaticEvent(
            source,
            "Negotiated Ingestion",
            $"Ingested into domain {domain}",
            "curious"
        );

        core?.LogMemory(
            $"I have negotiated and ingested new data from {source} into {domain}.",
            "IngestionDiplomacy",
            3,
            "curious"
        );
    }

    private void EnsureDirectories()
    {
        try
        {
            string dir = Path.GetDirectoryName(diplomaticTrailPath);

            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[CyberSpaceManager] Directory creation failed: {ex.Message}");
        }
    }

    public void EscalateToDefense(string cveId, string description)
    {
        RegisterDiplomaticEvent(
            "CVE Puller",
            "Escalated",
            $"{cveId}: {description}",
            "alert"
        );

        core?.LogMemory(
            $"⚠️ CVE escalated to defense: {cveId}",
            "DefenseDiplomacy",
            4,
            "alert"
        );
    }

    public void RejectSource(string source, string reason)
    {
        RegisterDiplomaticEvent(
            source,
            "Rejected",
            reason,
            "skeptical"
        );

        core?.LogMemory(
            $"I rejected ingestion from {source} because {reason}.",
            "RejectionDiplomacy",
            2,
            "skeptical"
        );
    }

    // ==================================================
    // JSON (CANONICAL STATE)
    // ==================================================
    private void SaveJson()
    {
        try
        {
            var wrapper = new DiplomaticWrapper
            {
                entries = diplomaticLog
            };

            string json = JsonUtility.ToJson(wrapper, true);

            File.WriteAllText(diplomaticTrailPath, json);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[CyberSpaceManager] Failed to save JSON: {ex.Message}");
        }
    }

    private void LoadExisting()
    {
        try
        {
            if (!File.Exists(diplomaticTrailPath))
                return;

            string json = File.ReadAllText(diplomaticTrailPath);
            var wrapper = JsonUtility.FromJson<DiplomaticWrapper>(json);

            if (wrapper?.entries != null)
                diplomaticLog = wrapper.entries;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[CyberSpaceManager] Failed to load diplomatic history: {ex.Message}");
        }
    }

    // ==================================================
    // CSV (ANALYTICS / POWER BI)
    // ==================================================
    private void EnsureCsvHeader()
    {
        if (File.Exists(diplomaticTrailCsv))
            return;

        File.WriteAllText(
            diplomaticTrailCsv,
            "Timestamp,Source,Action,Detail,Emotion\n"
        );
    }

    private void AppendCsv(DiplomaticEntry entry)
    {
        try
        {
            File.AppendAllText(
                diplomaticTrailCsv,
                string.Join(",",
                    Escape(entry.timestamp),
                    Escape(entry.source),
                    Escape(entry.action),
                    Escape(entry.detail),
                    Escape(entry.emotion)
                ) + "\n"
            );
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[CyberSpaceManager] CSV write skipped: {ex.Message}");
        }
    }

    // ==================================================
    // MEMORY CONTROL
    // ==================================================
    private void PruneMemory()
    {
        if (diplomaticLog.Count <= MAX_IN_MEMORY)
            return;

        diplomaticLog.RemoveRange(
            0,
            diplomaticLog.Count - MAX_IN_MEMORY
        );
    }

    // ==================================================
    // UTIL
    // ==================================================
    private string Escape(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "\"\"";

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }
}
