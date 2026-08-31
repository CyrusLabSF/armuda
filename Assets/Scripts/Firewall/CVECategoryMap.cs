using System.Collections.Generic;
using System;
using System.Linq;
using UnityEngine;

public static class CVECategoryMap
{
    private static Dictionary<string, string> keywordToCategory = new()
    {
        { "buffer overflow", "Code Execution" },
        { "privilege escalation", "Privilege Escalation" },
        { "information disclosure", "Info Disclosure" },
        { "sql injection", "Injection" },
        { "cross-site scripting", "Injection" },
        { "xss", "Injection" },
        { "denial of service", "Denial of Service" },
        { "arbitrary code", "Code Execution" },
        { "bypass", "Authentication Bypass" },
        { "remote", "Remote Exploit" },
        { "bluetooth", "Peripheral Exploit" },
        { "android", "Mobile Exploit" },
        { "ios", "Mobile Exploit" },
        { "usb", "Peripheral Exploit" },
        { "driver", "Kernel Exploit" },
        { "kernel", "Kernel Exploit" },
        { "race condition", "Concurrency Vulnerability" },
        { "memory leak", "Memory Vulnerability" },
        { "integer overflow", "Arithmetic Error" }
    };

    private static Dictionary<string, string> categoryToRiskLevel = new()
    {
        { "Code Execution", "critical" },
        { "Privilege Escalation", "high" },
        { "Info Disclosure", "medium" },
        { "Injection", "high" },
        { "Authentication Bypass", "high" },
        { "Remote Exploit", "critical" },
        { "Peripheral Exploit", "medium" },
        { "Kernel Exploit", "critical" },
        { "Mobile Exploit", "medium" },
        { "Denial of Service", "low" },
        { "Concurrency Vulnerability", "medium" },
        { "Memory Vulnerability", "medium" },
        { "Arithmetic Error", "medium" }
    };

    public class CVECategoryResult
    {
        public List<string> categories = new();
        public float confidence = 0f;
        public string rawMatch = "";
        public string riskLevel = "low";
    }

    public static CVECategoryResult Analyze(string description)
    {
        var result = new CVECategoryResult();
        string desc = description.ToLower();
        int hits = 0;

        foreach (var kvp in keywordToCategory)
        {
            if (desc.Contains(kvp.Key))
            {
                result.categories.Add(kvp.Value);
                result.rawMatch = kvp.Key;
                hits++;
            }
        }

        result.confidence = Mathf.Clamp01(hits * 0.2f);
        result.riskLevel = DetermineHighestRisk(result.categories);

        if (result.categories.Count == 0)
            result.categories.Add("Uncategorized");

        return result;
    }

    private static string DetermineHighestRisk(List<string> categories)
    {
        string[] order = { "critical", "high", "medium", "low" };

        foreach (var level in order)
        {
            if (categories.Any(c => categoryToRiskLevel.TryGetValue(c, out var r) && r == level))
                return level;
        }

        return "low";
    }
}
