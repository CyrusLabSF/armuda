using System;
using System.IO;
using UnityEngine;

public class ArTusDriverImport : MonoBehaviour
{
    // =========================================================
    // Runtime-resolved paths (WebGL safe)
    // =========================================================
    private string importPath;
    private string exportPath;

    // =========================================================
    // References
    // =========================================================
    private ArTusCoreState core;
    private MonoBehaviour firewall; // intentionally loose

    // =========================================================
    // Unity Lifecycle
    // =========================================================
    private void Awake()
    {
        core = GetComponent<ArTusCoreState>();
        firewall = GetComponent<ArTusFirewall>(); // may be null

        importPath = ArTusPathUtility.GetPersistent(
            "UNIVERcity/Defense/DriverScan.json"
        );

        exportPath = ArTusPathUtility.GetPersistent(
            "UNIVERcity/Defense/DriverScanLog.csv"
        );

        EnsureDirectories();
    }

    // =========================================================
    // Public API
    // =========================================================
    public void ImportDriverScan()
    {
        if (!File.Exists(importPath))
        {
            Debug.LogWarning("[DriverImport] No driver scan file found.");
            return;
        }

        try
        {
            string json = File.ReadAllText(importPath);
            if (string.IsNullOrWhiteSpace(json))
                return;

            DriverScanResult result =
                JsonUtility.FromJson<DriverScanResult>(json);

            if (result == null)
            {
                Debug.LogWarning("[DriverImport] Failed to parse scan file.");
                return;
            }

            HandleScanResult(result);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[DriverImport] Import failed: {ex.Message}");
        }
    }

    // =========================================================
    // Internal Logic
    // =========================================================
    private void HandleScanResult(DriverScanResult result)
    {
        int threatCount = result.flaggedDrivers != null
            ? result.flaggedDrivers.Count
            : 0;

        string emotion = threatCount > 0 ? "alert" : "neutral";

        string memory =
            $"🧩 Driver Scan ({result.timestamp})\n" +
            $"Installed: {result.installedDrivers?.Count ?? 0}\n" +
            $"Flagged: {threatCount}";

        core?.LogMemory(
            memory,
            "DriverScan",
            threatCount > 0 ? 3f : 1f,
            emotion
        );

        // 🔐 OPTIONAL firewall hook (reflection-safe)
        TryNotifyFirewall(threatCount);

        AppendCsvLog(result);
    }

    // =========================================================
    // Firewall integration (SAFE / OPTIONAL)
    // =========================================================
    private void TryNotifyFirewall(int threatCount)
    {
        if (firewall == null)
            return;

        var method = firewall.GetType().GetMethod(
            "OnDriverScan",
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic
        );

        if (method != null)
        {
            method.Invoke(firewall, new object[] { threatCount });
        }
        else
        {
            Debug.Log(
                "[DriverImport] Firewall present, but no OnDriverScan handler found."
            );
        }
    }

    // =========================================================
    // CSV Logging
    // =========================================================
    private void AppendCsvLog(DriverScanResult result)
    {
        try
        {
            if (!File.Exists(exportPath))
            {
                File.WriteAllText(
                    exportPath,
                    "Timestamp,Installed,Flagged\n"
                );
            }

            string line =
                $"{result.timestamp}," +
                $"{result.installedDrivers?.Count ?? 0}," +
                $"{result.flaggedDrivers?.Count ?? 0}\n";

            File.AppendAllText(exportPath, line);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[DriverImport] CSV write failed: {ex.Message}");
        }
    }

    private void EnsureDirectories()
    {
        try
        {
            string dir = Path.GetDirectoryName(importPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }
        catch (Exception ex)
        {
            Debug.LogWarning(
                $"[DriverImport] Directory creation failed: {ex.Message}"
            );
        }
    }
}

// =========================================================
// Data Model
// =========================================================
[Serializable]
public class DriverScanResult
{
    public string timestamp;
    public System.Collections.Generic.List<string> installedDrivers;
    public System.Collections.Generic.List<string> flaggedDrivers;
}
