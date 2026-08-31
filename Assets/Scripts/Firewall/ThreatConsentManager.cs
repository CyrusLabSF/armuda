using UnityEngine;
using System;
using System.IO;
using System.Collections.Generic;

/// <summary>
/// Hi-Class Threat Consent Manager
/// Passive consent ledger for security-related actions
/// No speech, no emotion, no memory writes, no learning triggers
/// STEP-2 SAFE
/// WEBGL SAFE
/// </summary>
public class ArTusThreatConsentManager : MonoBehaviour
{
    // --------------------------------------------------
    // PATHS (RESOLVED AT RUNTIME — SAFE)
    // --------------------------------------------------

    private string csvConsentPath;
    private string jsonConsentPath;

    private bool ioEnabled = true;

    // --------------------------------------------------
    // DATA MODEL
    // --------------------------------------------------

    [Serializable]
    public class ConsentRecord
    {
        public string timestamp;
        public string actionType;
        public int port;
        public string status;   // pending / approved / rejected
        public string source;
    }

    [Serializable]
    private class ConsentWrapper
    {
        public List<ConsentRecord> records = new();
    }

    private ConsentWrapper consentLog = new();

    // --------------------------------------------------
    // UNITY LIFECYCLE
    // --------------------------------------------------

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

        try
        {
            csvConsentPath = ArTusPathUtility.GetPersistent(
                "UNIVERcity/Defense/ConsentTrail.csv"
            );

            jsonConsentPath = ArTusPathUtility.GetPersistent(
                "UNIVERcity/Defense/ConsentLog.json"
            );

            string csvDir = Path.GetDirectoryName(csvConsentPath);
            string jsonDir = Path.GetDirectoryName(jsonConsentPath);

            if (!string.IsNullOrEmpty(csvDir))
                Directory.CreateDirectory(csvDir);

            if (!string.IsNullOrEmpty(jsonDir))
                Directory.CreateDirectory(jsonDir);

            if (!File.Exists(csvConsentPath))
            {
                File.WriteAllText(
                    csvConsentPath,
                    "Timestamp,ActionType,Port,Status,Source\n"
                );
            }
        }
        catch (Exception ex)
        {
            ioEnabled = false;
            enabled = false;
            Debug.LogWarning(
                $"[ThreatConsentManager] Disabled due to I/O failure: {ex.Message}"
            );
        }
    }

    // --------------------------------------------------
    // PUBLIC API (PASSIVE ONLY)
    // --------------------------------------------------

    public ConsentRecord RequestConsent(
        string actionType,
        int port,
        string source = "Security"
    )
    {
        return Record(actionType, port, "pending", source);
    }

    public ConsentRecord ApproveAction(
        string actionType,
        int port,
        string source = "User"
    )
    {
        return Record(actionType, port, "approved", source);
    }

    public ConsentRecord RejectAction(
        string actionType,
        int port,
        string source = "User"
    )
    {
        return Record(actionType, port, "rejected", source);
    }

    // --------------------------------------------------
    // CORE LEDGER LOGIC (SAFE)
    // --------------------------------------------------

    private ConsentRecord Record(
        string actionType,
        int port,
        string status,
        string source
    )
    {
        var record = new ConsentRecord
        {
            timestamp = DateTime.UtcNow.ToString("o"),
            actionType = actionType,
            port = port,
            status = status,
            source = source
        };

        // Always keep in-memory record
        consentLog.records.Add(record);

        // Persist only if allowed
        if (ioEnabled)
        {
            ExportCSV(record);
            ExportJSON();
        }

        return record;
    }

    // --------------------------------------------------
    // EXPORTS (SAFE)
    // --------------------------------------------------

    private void ExportCSV(ConsentRecord r)
    {
        try
        {
            File.AppendAllText(
                csvConsentPath,
                $"{r.timestamp},{r.actionType},{r.port},{r.status},{r.source}\n"
            );
        }
        catch { }
    }

    private void ExportJSON()
    {
        try
        {
            File.WriteAllText(
                jsonConsentPath,
                JsonUtility.ToJson(consentLog, true)
            );
        }
        catch { }
    }
}
