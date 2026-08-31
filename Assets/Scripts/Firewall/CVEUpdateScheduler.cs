using System;
using System.IO;
using UnityEngine;
using System.Collections;

/// <summary>
/// CVEUpdateScheduler
/// ------------------
/// • Schedules CVE ingestion checks
/// • Logs update history
/// • WebGL-safe (auto-disabled)
/// • Desktop-safe
/// </summary>
public class CVEUpdateScheduler : MonoBehaviour
{
    private const int DAYS_LATE_WARNING = 21;

    // ======================================================
    // PATHS (CENTRALIZED, SAFE)
    // ======================================================

    private string schedulerDataPath =>
        ArTusPathUtility.GetPersistent(
            "UNIVERcity/Security/LastUpdate.txt"
        );

    private string updateLogPath =>
        ArTusPathUtility.GetPersistent(
            "UNIVERcity/Security/CVEUpdateHistory.csv"
        );

    // ======================================================
    // CONFIG
    // ======================================================

    private readonly TimeSpan updateInterval =
        TimeSpan.FromDays(14);

    private DateTime lastUpdate;
    private bool isUpdating;
    private bool ioEnabled = true;

    // ======================================================
    // DEPENDENCIES
    // ======================================================

    private CVEIngestor ingestor;
    private ArTusCoreState core;
    private ArTusSpeechResponder speech;

    // ======================================================
    // UNITY
    // ======================================================

    void Awake()
    {
#if UNITY_WEBGL || UNITY_ANDROID || UNITY_IOS
        ioEnabled = false;
        enabled = false;
        return;
#endif
    }

    void Start()
    {
        if (!enabled)
            return;

        ingestor = GetComponent<CVEIngestor>();
        core = GetComponent<ArTusCoreState>();
        speech = GetComponent<ArTusSpeechResponder>();

        TryEnsureDir(schedulerDataPath);
        TryEnsureCsv(updateLogPath, "Timestamp,Mode\n");

        LoadLastUpdateTime();

        if (ShouldCheckNow())
            TriggerUpdate("auto");
        else
            Debug.Log("[CVEUpdateScheduler] Update not required yet.");
    }

    // ======================================================
    // STATE
    // ======================================================

    private void LoadLastUpdateTime()
    {
        if (!ioEnabled)
        {
            lastUpdate = DateTime.Now;
            return;
        }

        try
        {
            if (File.Exists(schedulerDataPath) &&
                DateTime.TryParse(
                    File.ReadAllText(schedulerDataPath),
                    out DateTime parsed))
            {
                lastUpdate = parsed;
            }
            else
            {
                lastUpdate = DateTime.Now - TimeSpan.FromDays(100);
            }
        }
        catch
        {
            lastUpdate = DateTime.Now - TimeSpan.FromDays(100);
        }
    }

    private bool ShouldCheckNow() =>
        DateTime.Now - lastUpdate >= updateInterval;

    // ======================================================
    // PUBLIC API
    // ======================================================

    public void TriggerUpdate(string mode = "manual")
    {
        if (!enabled || isUpdating)
            return;

        StartCoroutine(UpdateRoutine(mode));
    }

    // ======================================================
    // UPDATE ROUTINE
    // ======================================================

    private IEnumerator UpdateRoutine(string mode)
    {
        isUpdating = true;

        DateTime previousUpdate = lastUpdate;

        speech?.TriggerVoice(
            "Beginning vulnerability database update."
        );

        core?.LogMemory(
            $"🗓 {mode.ToUpper()} CVE update started.",
            "SecurityUpdate",
            2,
            "thinking"
        );

        yield return null;

        ingestor?.IngestLatestCVEs();

        lastUpdate = DateTime.Now;

        if (ioEnabled)
        {
            TryWriteText(
                schedulerDataPath,
                lastUpdate.ToString("o")
            );

            TryEnsureCsv(updateLogPath, "Timestamp,Mode\n");

            TryAppendText(
                updateLogPath,
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss},{mode}{Environment.NewLine}"
            );
        }

        double daysLate =
            (lastUpdate - previousUpdate).TotalDays;

        if (daysLate > DAYS_LATE_WARNING)
        {
            core?.LogMemory(
                $"⚠️ CVE update delayed by {Mathf.RoundToInt((float)daysLate)} days.",
                "SecurityAnomaly",
                3,
                "concerned"
            );

            speech?.TriggerVoice(
                "Security updates were delayed. Please confirm system integrity."
            );
        }
        else
        {
            core?.LogMemory(
                "✅ CVE update completed successfully.",
                "SecurityUpdate",
                2,
                "relieved"
            );
        }

        core?.LogMemory(
            $"⏳ Next CVE update expected around {(lastUpdate + updateInterval):yyyy-MM-dd}.",
            "SecurityForecast",
            1,
            "neutral"
        );

        isUpdating = false;
    }

    // ======================================================
    // SAFE FILE HELPERS
    // ======================================================

    private static void TryEnsureDir(string filePath)
    {
        try
        {
            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
        }
        catch { }
    }

    private static void TryEnsureCsv(string filePath, string header)
    {
        try
        {
            TryEnsureDir(filePath);
            if (!File.Exists(filePath))
                File.WriteAllText(filePath, header);
        }
        catch { }
    }

    private static void TryWriteText(string path, string content)
    {
        try
        {
            File.WriteAllText(path, content);
        }
        catch { }
    }

    private static void TryAppendText(string path, string content)
    {
        try
        {
            File.AppendAllText(path, content);
        }
        catch { }
    }
}
