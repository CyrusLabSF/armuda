using System.Collections.Generic;
using System.IO;
using System.Linq;
using System;
using UnityEngine;

public class AutoCategorizer
{
    private Dictionary<string, List<string>> keywordMap = new();

    private string logPath = "D:/ArTusCloud-Deployment/UNIVERcity/Logs/AutoCategorizerLog.csv";

    public AutoCategorizer()
    {
        // Pre-seed categories
        keywordMap["biology"] = new() { "cell", "organism", "dna", "mutation", "photosynthesis" };
        keywordMap["astronomy"] = new() { "galaxy", "orbit", "telescope", "planet", "supernova" };
        keywordMap["psychology"] = new() { "cognition", "emotion", "perception", "mind", "memory" };
        keywordMap["cybersecurity"] = new() { "vulnerability", "exploit", "firewall", "breach", "patch" };

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(logPath));
            if (!File.Exists(logPath))
                File.WriteAllText(logPath, "Timestamp,Text,Primary,Secondary,Confidence\n");
        }
        catch (IOException ex)
        {
            Debug.LogError($"[AutoCategorizer] Failed to prepare log file: {ex.Message}");
        }
    }

    public string Detect(string text)
    {
        var result = DetectMulti(text);
        return result.primary ?? "uncategorized";
    }

    public (string primary, string secondary, float confidence) DetectMulti(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return ("uncategorized", "", 0f);

        Dictionary<string, int> matchCounts = new();

        foreach (var kv in keywordMap)
        {
            int matches = kv.Value.Count(k => text.ToLower().Contains(k.ToLower()));
            if (matches > 0)
                matchCounts[kv.Key] = matches;
        }

        var ranked = matchCounts.OrderByDescending(k => k.Value).ToList();
        string primary = ranked.Count > 0 ? ranked[0].Key : "uncategorized";
        string secondary = ranked.Count > 1 ? ranked[1].Key : "";
        float confidence = ranked.Count > 0 ? Mathf.Clamp01(ranked[0].Value / 5f) : 0f;

        LogDetection(text, primary, secondary, confidence);

        return (primary, secondary, confidence);
    }

    private void LogDetection(string text, string primary, string secondary, float confidence)
    {
        string logLine = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss},{text.Replace(",", ";")},{primary},{secondary},{confidence:F2}";
        try
        {
            File.AppendAllText(logPath, logLine + "\n");
        }
        catch (IOException ex)
        {
            Debug.LogError($"[AutoCategorizer] Failed to log detection: {ex.Message}");
        }
    }

    public void AddKeyword(string category, string keyword)
    {
        if (!keywordMap.ContainsKey(category))
            keywordMap[category] = new();

        if (!keywordMap[category].Contains(keyword))
        {
            keywordMap[category].Add(keyword);
            Debug.Log($"[AutoCategorizer] 📥 Keyword '{keyword}' added to category '{category}'.");
        }
    }

    public List<string> GetKeywords(string category)
    {
        return keywordMap.ContainsKey(category) ? keywordMap[category] : new();
    }

    public Dictionary<string, List<string>> GetFullMap()
    {
        return keywordMap ?? new();
    }
}
