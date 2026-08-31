using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using ArTusTypes;

/// <summary>
/// Hi-Class Firewall
/// Passive security scanner and classifier
/// No memory logging, no advisories, no emotion, no speech
/// </summary>
public class ArTusFirewall : MonoBehaviour
{
    [Header("Trusted Vendors")]
    public List<string> knownSafeVendors = new()
    {
        "nvidia", "realtek", "intel", "amd", "microsoft", "logitech"
    };

    private string driverLogPath =>
        Path.Combine(Application.dataPath, "Firewall", "DriverSecurityScan.csv");

    // --------------------------------------------------
    // COMMAND FILTERING (PASSIVE)
    // --------------------------------------------------
    public bool IsSafeCommand(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
            return true;

        string lower = command.ToLowerInvariant();
        return !lower.Contains("delete") && !lower.Contains("format");
    }

    // --------------------------------------------------
    // APPLICATION MONITOR (STUB / PASSIVE)
    // --------------------------------------------------
    public void MonitorApplicationActivity()
    {
        // Reserved for future behavioral analysis
    }

    // --------------------------------------------------
    // DRIVER SCAN (PASSIVE)
    // --------------------------------------------------
    public InternalDriverListWrapper ScanInstalledDrivers()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(driverLogPath));

        var wrapper = new InternalDriverListWrapper();

        string[] simulatedDrivers =
        {
            "NVIDIA Audio Driver",
            "Realtek Network Adapter",
            "SuspiciousDriver_XYZ",
            "Intel Chipset",
            "UnknownKernelHookDriver"
        };

        using StreamWriter writer = new(driverLogPath);
        writer.WriteLine("DriverName,VendorTrust,Version,Status,Weight,Timestamp");

        foreach (string driverName in simulatedDrivers)
        {
            bool flagged =
                driverName.ToLower().Contains("suspicious") ||
                driverName.ToLower().Contains("unknown");

            bool vendorTrusted = IsVendorSafe(driverName);

            var entry = new DriverEntry
            {
                name = driverName,
                source = "Scan",
                value = flagged ? "flagged" : "verified",
                signalType = "driver",
                timestamp = DateTime.UtcNow.ToString("o"),
                deviceName = driverName,
                version = flagged ? "unknown" : "1.0.0",
                date = DateTime.UtcNow.ToShortDateString(),
                weight = flagged || !vendorTrusted ? "critical" : "normal"
            };

            wrapper.drivers.Add(entry);

            writer.WriteLine(
                $"{Escape(driverName)}," +
                $"{(vendorTrusted ? "trusted" : "unverified")}," +
                $"{entry.version}," +
                $"{entry.value}," +
                $"{entry.weight}," +
                $"{entry.timestamp}"
            );
        }

        Debug.Log($"[Firewall] Driver scan complete → {driverLogPath}");
        return wrapper;
    }

    // --------------------------------------------------
    // UTIL
    // --------------------------------------------------
    private bool IsVendorSafe(string name)
    {
        string lower = name.ToLowerInvariant();
        foreach (string safe in knownSafeVendors)
        {
            if (lower.Contains(safe))
                return true;
        }
        return false;
    }

    private static string Escape(string input)
    {
        return $"\"{input.Replace("\"", "\"\"")}\"";
    }
}
