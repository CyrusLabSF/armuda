using UnityEngine;
using System.Linq;
using System.IO;
using System;

public class ArTusDomainGrowthReporter : MonoBehaviour
{
    private ArTusCoreState core;

    private string csvLogPath = "D:/ArTusCloud-Deployment/UNIVERcity/GrowthLogs/GrowthReport.csv";

    void Start()
    {
        core = GetComponent<ArTusCoreState>();

        if (!File.Exists(csvLogPath))
            File.WriteAllText(csvLogPath, "Timestamp,Domain,Entries,Clarity,HeatRating\n");
    }

    public void ReportGrowth(string domain)
    {
        int count = core.memoryLog.Count(m => m.category == domain);
        float avgClarity = core.memoryLog
            .Where(m => m.category == domain)
            .Select(m => m.clarity)
            .DefaultIfEmpty(0f)
            .Average();

        string heat = GetHeatRating(count, avgClarity);
        string msg = $"📈 {domain} now has {count} entries. Avg clarity: {avgClarity:F2} | Heat: {heat}";

        core.LogMemory(msg, "GrowthReport", 3, "focused");
        Debug.Log(msg);

        File.AppendAllText(csvLogPath,
            $"{DateTime.Now},{domain},{count},{avgClarity:F2},{heat}\n");
    }

    private string GetHeatRating(int count, float clarity)
    {
        float score = count * clarity;

        if (score > 300) return "high";
        if (score > 150) return "medium";
        return "low";
    }
}
