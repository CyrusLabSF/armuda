using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEngine;

public class CVEPatternAnalyzer : MonoBehaviour
{
    private ArTusCoreState core;
    private ArTusSpeechResponder speech;
    private CVEIngestor ingestor;

    [Header("Analysis Settings")]
    public int minCvesRequired = 5;
    public int sampleWindow = 50;
    public float analysisInterval = 60f;

    [Header("Export Settings")]
    [SerializeField]
    private string threatLibraryFolder =
        "UNIVERcity/Security/ThreatLibrary";

    [SerializeField]
    private string powerBiCsvRelativePath =
        "UNIVERcity/Exports/CVEThreatPatterns.csv";

    private string lastBeliefHash = "";

    // 🔐 Resolved at runtime (WebGL-safe)
    private string threatLibraryPath;
    private string powerBiCsvPath;

    void Awake()
    {
        core = GetComponent<ArTusCoreState>();
        speech = GetComponent<ArTusSpeechResponder>();
        ingestor = GetComponent<CVEIngestor>();

        threatLibraryPath =
            ArTusPathUtility.GetPersistent(threatLibraryFolder);

        powerBiCsvPath =
            ArTusPathUtility.GetPersistent(powerBiCsvRelativePath);

#if !UNITY_WEBGL
        Directory.CreateDirectory(threatLibraryPath);
#endif
    }

    void Start()
    {
        InvokeRepeating(nameof(AnalyzePatterns), 10f, analysisInterval);
    }

    private void AnalyzePatterns()
    {
        if (ingestor == null || ingestor.parsedCVE == null)
            return;

        if (ingestor.parsedCVE.Count < minCvesRequired)
            return;

        var recent = ingestor.parsedCVE
            .OrderByDescending(c => c.timestamp)
            .Take(sampleWindow)
            .ToList();

        if (recent.Count == 0)
            return;

        Dictionary<string, int> categoryCounts = new();
        Dictionary<string, int> platformCounts = new();

        foreach (var cve in recent)
        {
            var result = CVECategoryMap.Analyze(cve.description);
            string category = result.categories.FirstOrDefault() ?? "Uncategorized";
            string platform = GetPlatformTag(cve.description);

            categoryCounts[category] =
                categoryCounts.ContainsKey(category)
                    ? categoryCounts[category] + 1
                    : 1;

            platformCounts[platform] =
                platformCounts.ContainsKey(platform)
                    ? platformCounts[platform] + 1
                    : 1;
        }

        if (categoryCounts.Count == 0 || platformCounts.Count == 0)
            return;

        string topCategory =
            categoryCounts.OrderByDescending(k => k.Value).First().Key;

        string topPlatform =
            platformCounts.OrderByDescending(k => k.Value).First().Key;

        float confidence =
            Mathf.Clamp01(categoryCounts[topCategory] / (float)recent.Count);

        string belief =
            $"I believe the most common recent vulnerability pattern is {topCategory}, " +
            $"primarily affecting {topPlatform} systems.";

        string beliefHash =
            $"{topCategory}:{topPlatform}:{confidence:F2}";

        if (beliefHash == lastBeliefHash)
            return; // 🧠 prevent belief spam

        lastBeliefHash = beliefHash;

        core?.LogMemory(belief, "ThreatPattern", 3, "concerned");

        speech?.TriggerVoice("I’ve detected a pattern in recent vulnerabilities.");
        speech?.TriggerVoice(belief);

        Debug.Log($"[CVEPatternAnalyzer] {belief}");

#if !UNITY_WEBGL
        ExportBeliefToUNIVERcity(belief, topCategory, topPlatform, confidence);
        ExportToPowerBI(topCategory, topPlatform, confidence);
#endif
    }

    private string GetPlatformTag(string description)
    {
        if (string.IsNullOrEmpty(description))
            return "Unknown";

        string lower = description.ToLowerInvariant();

        if (lower.Contains("windows")) return "Windows";
        if (lower.Contains("android")) return "Android";
        if (lower.Contains("ios") || lower.Contains("iphone")) return "iOS";
        if (lower.Contains("linux")) return "Linux";
        if (lower.Contains("usb") || lower.Contains("bluetooth")) return "Peripheral";

        return "Unknown";
    }

#if !UNITY_WEBGL
    private void ExportBeliefToUNIVERcity(
        string belief,
        string category,
        string platform,
        float confidence)
    {
        string filename =
            $"Belief_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json";

        var beliefEntry = new ThreatBelief
        {
            belief = belief,
            top_category = category,
            platform = platform,
            generated = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
            confidence = confidence,
            curiosity = Mathf.Clamp01(1f - confidence)
        };

        try
        {
            string json =
                JsonUtility.ToJson(beliefEntry, true);

            File.WriteAllText(
                Path.Combine(threatLibraryPath, filename),
                json
            );
        }
        catch (Exception ex)
        {
            Debug.LogError(
                $"[CVEPatternAnalyzer] Export failed: {ex.Message}"
            );
        }
    }

    private void ExportToPowerBI(
        string category,
        string platform,
        float confidence)
    {
        EnsureCsvHeader(
            powerBiCsvPath,
            "Timestamp,Category,Platform,Confidence\n"
        );

        File.AppendAllText(
            powerBiCsvPath,
            $"{DateTime.UtcNow:o},{category},{platform},{confidence:F3}\n"
        );
    }

    private void EnsureCsvHeader(string path, string header)
    {
        if (!File.Exists(path))
            File.WriteAllText(path, header);
    }
#endif

    [Serializable]
    public class ThreatBelief
    {
        public string belief;
        public string top_category;
        public string platform;
        public string generated;
        public float confidence;
        public float curiosity;
    }
}
