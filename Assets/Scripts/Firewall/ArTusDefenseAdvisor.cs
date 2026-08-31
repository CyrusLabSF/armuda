using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System;


/// <summary>
/// Hi-Class Defense Advisor
/// Produces structured security advisories for logging, export, and alerting
/// No speech, no memory logging, no emotion triggers
/// WebGL-safe (persistent paths only)
/// </summary>
public class ArTusDefenseAdvisor : MonoBehaviour
{
    // --------------------------------------------------
    // ADVISORY TABLE
    // --------------------------------------------------
    private readonly Dictionary<string, string> advisoryActions = new()
    {
        { "OS", "Apply OS security patches and audit system users." },
        { "Network", "Harden firewall rules and monitor open ports." },
        { "Kernel", "Update kernel and run integrity verification checks." },
        { "App", "Update dependencies and sanitize all inputs." },
        { "Stealth", "Run anti-rootkit scans and verify system integrity." },
        { "Memory", "Adopt memory-safe practices and enable ASLR." }
    };

    // --------------------------------------------------
    // PATHS (WEBGL SAFE)
    // --------------------------------------------------
    private string ExportPath =>
        ArTusPathUtility.GetPersistent(
            "UNIVERcity/Defense/DefenseAdvisoryLog.csv"
        );

    // --------------------------------------------------
    // UNITY LIFECYCLE
    // --------------------------------------------------
    void Awake()
    {
        string dir = Path.GetDirectoryName(ExportPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        if (!File.Exists(ExportPath))
        {
            File.WriteAllText(
                ExportPath,
                "Timestamp,Category,Recommendation,Severity,Source\n"
            );
        }
    }

    // --------------------------------------------------
    // PUBLIC API (PASSIVE)
    // --------------------------------------------------
    public DefenseAdvisory Advise(
        string category,
        float riskScore = 0f,
        string source = "Firewall"
    )
    {
        if (!advisoryActions.TryGetValue(category, out var recommendation))
        {
            Debug.LogWarning(
                $"[DefenseAdvisor] No advisory available for category: {category}"
            );
            return null;
        }

        string severity = ClassifySeverity(riskScore);

        var advisory = new DefenseAdvisory
        {
            timestamp = DateTime.UtcNow.ToString("o"),
            category = category,
            recommendation = recommendation,
            severity = severity,
            source = source
        };

        Export(advisory);
        return advisory;
    }

    // --------------------------------------------------
    // EXPORT (POWER BI / ALERTING READY)
    // --------------------------------------------------
    private void Export(DefenseAdvisory advisory)
    {
        File.AppendAllText(
            ExportPath,
            $"{advisory.timestamp}," +
            $"{advisory.category}," +
            $"{Escape(advisory.recommendation)}," +
            $"{advisory.severity}," +
            $"{advisory.source}{Environment.NewLine}"
        );
    }

    // --------------------------------------------------
    // UTIL
    // --------------------------------------------------
    private string ClassifySeverity(float risk)
    {
        if (risk >= 8f) return "Critical";
        if (risk >= 5f) return "High";
        if (risk >= 3f) return "Medium";
        return "Low";
    }

    private static string Escape(string input)
    {
        return $"\"{input.Replace("\"", "\"\"")}\"";
    }

    // --------------------------------------------------
    // LEGACY COMPATIBILITY (SAFE ADAPTERS)
    // --------------------------------------------------

    /// <summary>
    /// Legacy helper for systems that only need the recommendation text
    /// </summary>
    public string GetRecommendationFor(string category)
    {
        if (advisoryActions.TryGetValue(category, out var recommendation))
            return recommendation;

        return "No advisory available for this category.";
    }

    /// <summary>
    /// Legacy CVE pattern advisory adapter
    /// </summary>
    public DefenseAdvisory AdviseOnPattern(
        string pattern,
        float severityScore
    )
    {
        string category = pattern.ToLowerInvariant() switch
        {
            var p when p.Contains("kernel") => "Kernel",
            var p when p.Contains("network") => "Network",
            var p when p.Contains("memory") => "Memory",
            var p when p.Contains("stealth") => "Stealth",
            var p when p.Contains("os") => "OS",
            _ => "App"
        };

        return Advise(category, severityScore, source: "CVEAnalyzer");
    }

    /// <summary>
    /// Legacy overload — assumes medium severity
    /// </summary>
    public DefenseAdvisory AdviseOnPattern(string pattern)
    {
        return AdviseOnPattern(pattern, 5f);
    }

    // --------------------------------------------------
    // DATA MODEL
    // --------------------------------------------------
    [Serializable]
    public class DefenseAdvisory
    {
        public string timestamp;
        public string category;
        public string recommendation;
        public string severity;
        public string source;
    }
}
