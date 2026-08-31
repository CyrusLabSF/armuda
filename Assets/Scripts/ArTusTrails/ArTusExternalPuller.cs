using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public class ArTusExternalPuller : MonoBehaviour
{
    private string endpointsPath = "D:/ArTusCloud-Deployment/UNIVERcity/System/ResourceEndpoints.json";
    private ArTusSpeechResponder speech;
    private ArTusCoreState core;

    void Start()
    {
        speech = GetComponent<ArTusSpeechResponder>();
        core = GetComponent<ArTusCoreState>();
    }

    public void ReviewExternalResources(string domain)
    {
        if (!File.Exists(endpointsPath))
        {
            Debug.LogWarning("[ExternalPuller] Resource endpoint file missing.");
            speech?.Speak("I'm missing my external resource guide right now. I'll try again later.");
            return;
        }

        string json = File.ReadAllText(endpointsPath);
        var resourceSet = JsonUtility.FromJson<ResourceCollection>(json);

        if (!resourceSet.domains.ContainsKey(domain))
        {
            speech?.Speak($"I don’t have external sources yet for {domain}.");
            core?.LogMemory($"❌ No external sources found for domain '{domain}'.", "ExternalPull", 1, "blocked");
            return;
        }

        var domainData = resourceSet.domains[domain];
        int total = domainData.sources.Count;

        if (total == 0)
        {
            speech?.Speak($"I found the domain '{domain}', but no active resources are listed yet.");
            return;
        }

        // 🧠 Group by type for enhanced memory and reflection
        var grouped = domainData.sources
            .GroupBy(s => s.type)
            .Select(g => $"🔹 {g.Key}:\n" + string.Join("\n", g.Select(s => $"- {s.description}")))
            .ToList();

        string summary = $"📡 External sources for domain '{domain}':\n" + string.Join("\n\n", grouped);

        core?.LogMemory(summary, "ExternalSources", 3, "seeking");
        speech?.Speak($"I found {total} sources related to {domain}. I’ve logged them to memory and organized them by category.");

        // ✅ Optional: Trigger simulation if rich set found
        if (total >= 5)
        {
            core?.LogMemory($"🔁 Triggered simulation for {domain} after reviewing {total} external sources.", "ExternalSimTrigger", 2, "curious");
        }

        // ✅ Optional Export for Power BI
        ExportSummary(domain, domainData.sources);
    }

    private void ExportSummary(string domain, List<ResourceEntry> sources)
    {
        string exportPath = $"D:/ArTusCloud-Deployment/UNIVERcity/Exports/ExternalSourcesSummary_{domain}.json";

        var export = new ExternalSourceExport
        {
            domain = domain,
            count = sources.Count,
            timestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            entries = sources
        };

        string json = JsonUtility.ToJson(export, true);
        FileIOHelper.SaveJson("external_sources", $"ExternalSourcesSummary_{domain}", json, delay: 1f);
    }

    [System.Serializable]
    public class ResourceCollection
    {
        public Dictionary<string, DomainResources> domains = new();
    }

    [System.Serializable]
    public class DomainResources
    {
        public string status;
        public List<ResourceEntry> sources = new();
    }

    [System.Serializable]
    public class ResourceEntry
    {
        public string type;
        public string description;
        public string url;
    }

    [System.Serializable]
    public class ExternalSourceExport
    {
        public string domain;
        public int count;
        public string timestamp;
        public List<ResourceEntry> entries;
    }
}

