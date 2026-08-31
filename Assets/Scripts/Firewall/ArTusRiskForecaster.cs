using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System;

public class ArTusRiskForecaster : MonoBehaviour
{
    // ==================================================
    // RELATIVE PATHS (SAFE TO SERIALIZE)
    // ==================================================

    [Header("Paths")]
    [SerializeField]
    private string threatPatternRelativePath =
        "UNIVERcity/ThreatLogs/ThreatPatterns.json";

    [SerializeField]
    private string csvForecastRelativePath =
        "UNIVERcity/Forecasts/ForecastSummary.csv";

    [SerializeField]
    private string jsonForecastRelativePath =
        "UNIVERcity/Forecasts/LatestForecast.json";

    // ==================================================
    // RESOLVED PATHS (RUNTIME ONLY)
    // ==================================================

    private string patternPath;
    private string csvForecast;
    private string jsonForecast;

    // ==================================================
    // DATA MODELS
    // ==================================================

    [Serializable]
    public class PatternEntry
    {
        public string type;
        public string label;
        public int count;
        public float confidence;
        public string lastSeen;
        public int contradictionCount;
    }

    [Serializable]
    private class PatternWrapper
    {
        public List<PatternEntry> patterns;
    }

    [Serializable]
    public class RiskForecast
    {
        public string timestamp;
        public string label;
        public string type;
        public float confidence;
        public int count;
        public int contradictions;
        public float riskScore;
        public string severity;
    }

    [Serializable]
    private class ForecastWrapper
    {
        public List<RiskForecast> forecasts;
    }

    // ==================================================
    // UNITY LIFECYCLE
    // ==================================================

    void Awake()
    {
        // Resolve paths safely at runtime
        patternPath = ArTusPathUtility.GetPersistent(threatPatternRelativePath);
        csvForecast = ArTusPathUtility.GetPersistent(csvForecastRelativePath);
        jsonForecast = ArTusPathUtility.GetPersistent(jsonForecastRelativePath);

        EnsureCsv(
            csvForecast,
            "Timestamp,Label,Type,Confidence,Count,Contradictions,RiskScore,Severity\n"
        );
    }

    // ==================================================
    // PUBLIC API
    // ==================================================

    public List<RiskForecast> GenerateRiskForecast()
    {
        if (!File.Exists(patternPath))
        {
            Debug.LogWarning("[RiskForecaster] No threat pattern data found.");
            return null;
        }

        var wrapper =
            JsonUtility.FromJson<PatternWrapper>(File.ReadAllText(patternPath));

        if (wrapper?.patterns == null || wrapper.patterns.Count == 0)
            return null;

        var results = new List<RiskForecast>();

        foreach (var pattern in wrapper.patterns)
        {
            if (pattern.confidence < 0.6f || pattern.count < 3)
                continue;

            float riskScore = CalculateRiskScore(pattern);
            string severity = ClassifySeverity(riskScore);

            var forecast = new RiskForecast
            {
                timestamp = DateTime.UtcNow.ToString("o"),
                label = pattern.label,
                type = pattern.type,
                confidence = pattern.confidence,
                count = pattern.count,
                contradictions = pattern.contradictionCount,
                riskScore = riskScore,
                severity = severity
            };

            results.Add(forecast);
            ExportCsv(forecast);
        }

        ExportJson(results);
        return results;
    }

    // ==================================================
    // CORE LOGIC
    // ==================================================

    private float CalculateRiskScore(PatternEntry pattern)
    {
        float score = pattern.confidence * pattern.count;

        if (pattern.contradictionCount > 0)
            score *= 0.85f;

        if (pattern.type.ToLower().Contains("network") ||
            pattern.label.ToLower().Contains("port"))
            score *= 1.2f;

        return Mathf.Clamp(score, 0f, 10f);
    }

    private string ClassifySeverity(float score)
    {
        if (score >= 8f) return "Critical";
        if (score >= 5f) return "High";
        if (score >= 3f) return "Medium";
        return "Low";
    }

    // ==================================================
    // EXPORTS
    // ==================================================

    private void ExportCsv(RiskForecast f)
    {
        File.AppendAllText(
            csvForecast,
            $"{f.timestamp},{f.label},{f.type},{f.confidence:F2}," +
            $"{f.count},{f.contradictions},{f.riskScore:F2},{f.severity}\n"
        );
    }

    private void ExportJson(List<RiskForecast> forecasts)
    {
        EnsureDir(jsonForecast);
        var wrapper = new ForecastWrapper { forecasts = forecasts };
        File.WriteAllText(jsonForecast, JsonUtility.ToJson(wrapper, true));
    }

    // ==================================================
    // HELPERS
    // ==================================================

    private static void EnsureDir(string filePath)
    {
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
    }

    private static void EnsureCsv(string filePath, string header)
    {
        EnsureDir(filePath);
        if (!File.Exists(filePath))
            File.WriteAllText(filePath, header);
    }
}
