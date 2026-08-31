using UnityEngine;
using System.Collections.Generic;
using System;

public class ArTusCVEAnalyzer : MonoBehaviour
{
    private ArTusCoreState core;
    private ArTusThreatPatternEngine patternEngine;
    private ArTusOntologyEngine ontology;
    private ArTusDefenseAdvisor advisor;

    private Dictionary<string, List<string>> beliefPatterns = new();

    // ======================================================
    // UNITY
    // ======================================================
    void Awake()
    {
        // 🔒 HARD PLATFORM GATE (STEP-2 RULE)
#if UNITY_WEBGL || UNITY_ANDROID || UNITY_IOS
        enabled = false;
        return;
#endif
    }

    void Start()
    {
        if (!enabled)
            return;

        core = GetComponent<ArTusCoreState>();
        patternEngine = GetComponent<ArTusThreatPatternEngine>();
        ontology = GetComponent<ArTusOntologyEngine>();
        advisor = GetComponent<ArTusDefenseAdvisor>();
    }

    // ======================================================
    // ANALYSIS (COGNITIVE ONLY)
    // ======================================================
    public void AnalyzeCVE(string cveID, string description)
    {
        if (!enabled)
            return;

        if (string.IsNullOrWhiteSpace(description))
            return;

        var matched = DetectPatterns(description);

        if (matched.Count == 0)
        {
            Debug.Log($"[CVEAnalyzer] {cveID} matched no known patterns.");
            return;
        }

        foreach (var pattern in matched)
        {
            if (!beliefPatterns.ContainsKey(pattern))
                beliefPatterns[pattern] = new List<string>();

            beliefPatterns[pattern].Add(cveID);

            // 🔒 Pattern observation only (no action)
            patternEngine?.ObservePattern(pattern, "cve");

            string advice =
                advisor?.GetRecommendationFor(pattern)
                ?? "No recommendation available.";

            core?.LogMemory(
                $"🛡 CVE {cveID} reinforces pattern '{pattern}'. {advice}",
                "CVE_Belief",
                3,
                "alert"
            );
        }
    }

    // ======================================================
    // PATTERN HEURISTICS (STABLE)
    // ======================================================
    private List<string> DetectPatterns(string text)
    {
        text = text.ToLower();
        var hits = new List<string>();

        if (text.Contains("overflow"))
            hits.Add("buffer overflow");

        if (text.Contains("injection"))
            hits.Add("injection");

        if (text.Contains("escalation") || text.Contains("privilege"))
            hits.Add("privilege escalation");

        if (text.Contains("xss") || text.Contains("cross-site"))
            hits.Add("cross-site scripting");

        if (text.Contains("remote code execution") || text.Contains("rce"))
            hits.Add("remote code execution");

        if (text.Contains("denial of service") || text.Contains("dos"))
            hits.Add("denial of service");

        if (text.Contains("authentication bypass"))
            hits.Add("authentication bypass");

        return hits;
    }
}
