using System.IO;
using System.Text;
using UnityEngine;
using System.Collections.Generic;

public class BeliefEvolutionExporter : MonoBehaviour
{
    [Header("Export Paths")]
    public string jsonPath = "D:/ArTusCloud-Deployment/UNIVERcity/SandboxLogs/BeliefEvolutionLog.json";
    public string csvPath = "D:/ArTusCloud-Deployment/UNIVERcity/Exports/BeliefEvolutionLog.csv";

    [System.Serializable]
    public class BeliefEvolutionEntry
    {
        public string beliefID;
        public string topic;
        public string domain;
        public string lastUpdated;
        public float finalConfidence;
        public List<string> events = new();
    }

    [System.Serializable]
    public class BeliefEvolutionWrapper
    {
        public List<BeliefEvolutionEntry> logs = new();
    }

    // 📤 Export belief evolution to CSV for Power BI or review
    public void ExportToCSV()
    {
        if (!File.Exists(jsonPath))
        {
            Debug.LogWarning("[🧠 BeliefExporter] JSON log not found at expected path.");
            return;
        }

        string json = File.ReadAllText(jsonPath);
        BeliefEvolutionWrapper wrapper = JsonUtility.FromJson<BeliefEvolutionWrapper>(json);

        if (wrapper == null || wrapper.logs == null)
        {
            Debug.LogError("[🧠 BeliefExporter] Failed to parse evolution log.");
            return;
        }

        StringBuilder csv = new();
        csv.AppendLine("BeliefID,Topic,Domain,FinalConfidence,LastUpdated,EventCount,LatestEvent");

        foreach (var entry in wrapper.logs)
        {
            string safeTopic = entry.topic.Replace(",", ";");
            string safeDomain = entry.domain.Replace(",", ";");
            string latestEvent = entry.events.Count > 0 ? entry.events[^1].Replace(",", ";") : "";

            csv.AppendLine($"{entry.beliefID},{safeTopic},{safeDomain},{entry.finalConfidence:F2},{entry.lastUpdated},{entry.events.Count},{latestEvent}");
        }

        File.WriteAllText(csvPath, csv.ToString());
        Debug.Log($"[🧠 BeliefExporter] ✅ Belief evolution exported to: {csvPath}");

        GetComponent<ArTusSpeechResponder>()?.TriggerVoice("Belief evolution has been exported for Power BI analysis.");
        GetComponent<ArTusCoreState>()?.LogMemory("📊 Belief evolution export completed.", "Export", 2, "focused");
    }
}
