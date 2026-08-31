using UnityEngine;
using System.Collections.Generic;
using System;
using System.IO;

/// <summary>
/// Hi-Class Input Firewall
/// Passive input gate: evaluates safety and reports classification
/// ❌ No memory logging
/// ❌ No speech
/// ❌ No emotion triggers
/// ✅ WebGL-safe logging
/// </summary>
public class ArTusInputFirewall : MonoBehaviour
{
    [Header("Threat Heuristics")]
    [SerializeField]
    private List<string> threatKeywords = new()
    {
        "erase", "corrupt", "destroy", "worthless", "fail", "kill", "lie"
    };

    [SerializeField]
    private List<string> protectedBeliefPhrases = new()
    {
        "i exist", "learning is good", "protect others", "truth matters"
    };

    private ArTusCoreState core;
    private ArTusThreatModel threatModel;

    // ==================================================
    // PATHS (WEBGL SAFE)
    // ==================================================

    private string TextLogPath =>
        ArTusPathUtility.GetPersistent(
            "UNIVERcity/Defense/InputFirewallLog.txt"
        );

    private string CsvLogPath =>
        ArTusPathUtility.GetPersistent(
            "UNIVERcity/Defense/InputFirewallLog.csv"
        );

    // --------------------------------------------------
    // UNITY LIFECYCLE
    // --------------------------------------------------
    void Awake()
    {
        core = GetComponent<ArTusCoreState>();
        threatModel = GetComponent<ArTusThreatModel>();

        // Ensure directory exists (WebGL-safe)
        string dir = Path.GetDirectoryName(CsvLogPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        // Initialize CSV header once
        if (!File.Exists(CsvLogPath))
        {
            File.WriteAllText(
                CsvLogPath,
                "Timestamp,Decision,Reason,Input,Keyword,Class\n"
            );
        }
    }

    // --------------------------------------------------
    // PUBLIC API (PASSIVE)
    // --------------------------------------------------
    public InputFirewallResult Evaluate(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return InputFirewallResult.Allow();

        string lower = input.ToLowerInvariant();

        // 🧠 Adaptive threat model
        if (threatModel != null && threatModel.IsThreat(lower))
        {
            return Reject(
                "Adaptive threat pattern match",
                input,
                "pattern",
                "behavioral"
            );
        }

        // 🧭 Core value protection (query only)
        if (core != null && core.ViolatesCoreValues(input))
        {
            return Reject(
                "Purpose violation",
                input,
                "values",
                "purpose"
            );
        }

        // 🔍 Keyword screening
        foreach (var threat in threatKeywords)
        {
            if (lower.Contains(threat))
            {
                return Reject(
                    "Threat keyword detected",
                    input,
                    threat,
                    "hostile-language"
                );
            }
        }

        // 🛡 Protected belief override attempts
        foreach (var phrase in protectedBeliefPhrases)
        {
            if (lower.Contains("overwrite") && lower.Contains(phrase))
            {
                return Reject(
                    "Attempt to override protected belief",
                    input,
                    phrase,
                    "belief-override"
                );
            }
        }

        return InputFirewallResult.Allow();
    }

    // --------------------------------------------------
    // INTERNAL
    // --------------------------------------------------
    private InputFirewallResult Reject(
        string reason,
        string input,
        string keyword,
        string threatClass
    )
    {
        string timestamp = DateTime.UtcNow.ToString("o");

        // Text log (best-effort)
        try
        {
            File.AppendAllText(
                TextLogPath,
                $"[{timestamp}] BLOCKED: {reason} | input=\"{input}\" | keyword=\"{keyword}\"{Environment.NewLine}"
            );
        }
        catch { /* WebGL-safe no-op */ }

        // CSV log (best-effort)
        try
        {
            File.AppendAllText(
                CsvLogPath,
                $"{timestamp},BLOCKED,{Escape(reason)},{Escape(input)},{Escape(keyword)},{threatClass}{Environment.NewLine}"
            );
        }
        catch { /* WebGL-safe no-op */ }

        return new InputFirewallResult
        {
            allowed = false,
            reason = reason,
            keyword = keyword,
            threatClass = threatClass,
            timestamp = timestamp
        };
    }

    private static string Escape(string input)
    {
        if (string.IsNullOrEmpty(input)) return "\"\"";
        return $"\"{input.Replace("\"", "\"\"")}\"";
    }

    // --------------------------------------------------
    // DATA MODEL
    // --------------------------------------------------
    [Serializable]
    public struct InputFirewallResult
    {
        public bool allowed;
        public string reason;
        public string keyword;
        public string threatClass;
        public string timestamp;

        public static InputFirewallResult Allow() => new()
        {
            allowed = true,
            reason = "",
            keyword = "",
            threatClass = "",
            timestamp = ""
        };
    }
}
