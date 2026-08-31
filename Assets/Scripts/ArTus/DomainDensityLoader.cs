using System.Collections.Generic;
using System.IO;
using UnityEngine;

[System.Serializable]
public class DomainDensityEntry
{
    public int memories = 0;
    public int downloads = 0;
    public int beliefs = 0;
    public int simulations = 0;
}

[System.Serializable]
public class DomainDensityWrapper
{
    public List<DomainEntryPair> entries = new();
}

[System.Serializable]
public class DomainEntryPair
{
    public string domain;
    public DomainDensityEntry data;
}

public class DomainDensityLoader : MonoBehaviour
{
    private string path = "D:/ArTusCloud-Deployment/UNIVERcity/Exports/DomainDensity.json";
    public Dictionary<string, DomainDensityEntry> densityData = new();

    void Start()
    {
        LoadDensity();
    }

    public void LoadDensity()
    {
        if (!File.Exists(path))
        {
            Debug.LogWarning("[DomainDensityLoader] File not found. Creating empty.");
            densityData = new();
            SaveDensity(); // Auto-create
            return;
        }

        string json = File.ReadAllText(path);
        var wrapper = JsonUtility.FromJson<DomainDensityWrapper>(json);

        densityData = new();
        foreach (var pair in wrapper.entries)
            densityData[pair.domain] = pair.data;

        Debug.Log($"[DomainDensityLoader] Loaded {densityData.Count} domains.");
    }

    public void SaveDensity()
    {
        var wrapper = new DomainDensityWrapper();
        foreach (var kvp in densityData)
        {
            wrapper.entries.Add(new DomainEntryPair
            {
                domain = kvp.Key,
                data = kvp.Value
            });
        }

        string json = JsonUtility.ToJson(wrapper, true);
        File.WriteAllText(path, json);
        Debug.Log("[DomainDensityLoader] Saved density data.");
    }

    public void Increment(string domain, string category)
    {
        if (!densityData.ContainsKey(domain))
            densityData[domain] = new DomainDensityEntry();

        var entry = densityData[domain];
        switch (category.ToLower())
        {
            case "memory": entry.memories++; break;
            case "download": entry.downloads++; break;
            case "belief": entry.beliefs++; break;
            case "simulation": entry.simulations++; break;
            default: Debug.LogWarning($"[DomainDensityLoader] Unknown category: {category}"); return;
        }

        SaveDensity();
    }

    public void ExportDensityCSV()
    {
        string csvPath = "D:/ArTusCloud-Deployment/UNIVERcity/Exports/DomainDensity.csv";
        List<string> lines = new() { "Domain,Memories,Downloads,Beliefs,Simulations" };

        foreach (var kvp in densityData)
        {
            var d = kvp.Value;
            lines.Add($"{kvp.Key},{d.memories},{d.downloads},{d.beliefs},{d.simulations}");
        }

        File.WriteAllLines(csvPath, lines);
        Debug.Log("[DomainDensityLoader] Exported density CSV.");
    }

    public int GetTotalWeight(string domain)
    {
        if (!densityData.ContainsKey(domain)) return 0;
        var d = densityData[domain];
        return d.memories + d.downloads + d.beliefs + d.simulations;
    }
}

