using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Newtonsoft.Json;

public class ArTusOntologyEngine : MonoBehaviour
{
    private Dictionary<string, CVEOntologyEntry> ontologyMap = new();

    [System.Serializable]
    public class CVEOntologyEntry
    {
        public string subcategory;
        public string category;
        public string superclass;
    }

    [Header("Ontology JSON Path")]
    public string ontologyPath = "D:/ArTusCloud-Deployment/Ontology/CVE_Ontology.json";

    void Start()
    {
        LoadOntology();
    }

    // 🧠 Load Ontology File
    public void LoadOntology()
    {
        if (string.IsNullOrEmpty(ontologyPath))
        {
            Debug.LogWarning("[🛡 Ontology] No ontology path defined.");
            return;
        }

        if (!File.Exists(ontologyPath))
        {
            Debug.LogWarning($"[🛡 Ontology] Ontology file not found at: {ontologyPath}");
            return;
        }

        try
        {
            string json = File.ReadAllText(ontologyPath);
            var rawMap = JsonConvert.DeserializeObject<Dictionary<string, CVEOntologyEntry>>(json)
                         ?? new Dictionary<string, CVEOntologyEntry>();

            // 🔑 Normalize all keys to lowercase
            ontologyMap = new Dictionary<string, CVEOntologyEntry>();
            foreach (var kvp in rawMap)
            {
                string normalizedKey = kvp.Key.Trim().ToLowerInvariant();
                ontologyMap[normalizedKey] = kvp.Value;
            }

            Debug.Log($"[🛡 Ontology] Loaded {ontologyMap.Count} CVE ontology entries (keys normalized).");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[🛡 Ontology] Failed to load ontology: {ex.Message}");
        }
    }

    // 🔎 Get Info for Specific Threat
    public CVEOntologyEntry GetOntologyInfo(string threatType)
    {
        if (string.IsNullOrWhiteSpace(threatType)) return null;

        string key = threatType.Trim().ToLowerInvariant();
        return ontologyMap.TryGetValue(key, out var entry) ? entry : null;
    }

    // 🧠 Log Ontology Insight to CoreState
    public void LogOntologyInsight(string threatType, ArTusCoreState core)
    {
        var info = GetOntologyInfo(threatType);
        if (info == null)
        {
            Debug.LogWarning($"[🛡 Ontology] No ontology info found for: {threatType}");
            return;
        }

        string insight = $"🧠 {threatType} is a {info.subcategory} under {info.category}, within the {info.superclass} class.";
        core?.LogMemory(insight, "CVE_Ontology", 2, "reflective");

        // [Future] Could trigger belief creation or reinforcement here
        // core?.AddOrUpdateBelief(threatType, insight, "ontology", "reflective");
    }

    // ⚖️ Scoring Logic for CVE Priority
    public int GetPriorityScore(string threatType)
    {
        var info = GetOntologyInfo(threatType);
        if (info == null) return 0;

        switch (info.superclass)
        {
            case "Application Security": return 5;
            case "Software Exploits": return 4;
            case "System Security": return 4;
            case "Information Security": return 3;
            case "System Reliability": return 2;
            default: return 1;
        }
    }
}
