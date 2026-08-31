using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class AdversaryTrailManager : MonoBehaviour
{
    // =========================================================
    // Paths (resolved at runtime — WebGL SAFE)
    // =========================================================
    private string trailLogPath;
    private string adversaryIndexPath;

    // =========================================================
    // State
    // =========================================================
    private List<AdversaryTrailEntry> trails = new List<AdversaryTrailEntry>();
    private bool initialized;

    // =========================================================
    // Unity Lifecycle
    // =========================================================
    private void Awake()
    {
        // ✅ Resolve paths ONLY here
        trailLogPath = ArTusPathUtility.GetPersistent(
            "UNIVERcity/Security/AdversaryTrails.jsonl"
        );

        adversaryIndexPath = ArTusPathUtility.GetPersistent(
            "UNIVERcity/Security/AdversaryIndex.json"
        );

        EnsureDirectories();
        initialized = true;
    }

    // =========================================================
    // Public API
    // =========================================================
    public void RegisterAdversaryEvent(
        string adversaryId,
        string vector,
        float severity
    )
    {
        if (!initialized)
            return;

        var entry = new AdversaryTrailEntry
        {
            adversaryId = adversaryId,
            attackVector = vector,
            severity = Mathf.Clamp01(severity),
            timestamp = DateTime.UtcNow.ToString("o")
        };

        trails.Add(entry);
        AppendTrail(entry);
    }

    // =========================================================
    // Persistence
    // =========================================================
    private void AppendTrail(AdversaryTrailEntry entry)
    {
        try
        {
            string json = JsonUtility.ToJson(entry, true);
            File.AppendAllText(trailLogPath, json + Environment.NewLine);
        }
        catch (Exception ex)
        {
            Debug.LogError(
                $"[AdversaryTrailManager] Failed to write trail: {ex.Message}"
            );
        }
    }

    private void EnsureDirectories()
    {
        try
        {
            string dir = Path.GetDirectoryName(trailLogPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }
        catch (Exception ex)
        {
            Debug.LogWarning(
                $"[AdversaryTrailManager] Directory creation failed: {ex.Message}"
            );
        }
    }
}

// =========================================================
// Data Model
// =========================================================
[Serializable]
public class AdversaryTrailEntry
{
    public string adversaryId;
    public string attackVector;
    public float severity;
    public string timestamp;
}
