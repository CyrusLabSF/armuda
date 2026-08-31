using UnityEngine;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.IO;
using System.Collections.Generic;

public class ArTusOpenSourceWideIngestor : MonoBehaviour
{
    private ArTusCoreState core;
    private ArTusSpeechResponder speech;
    private ArTusBeliefEngine belief;

    // ✅ Endpoints easily extendable
    private Dictionary<string, string> endpoints = new()
    {
        { "Wikipedia",    "http://127.0.0.1:8000/wikipedia/search?q=" },
        { "PubMed",       "http://127.0.0.1:8000/pubmed/search?q=" },
        { "OpenLibrary",  "http://127.0.0.1:8000/openlibrary/search?q=" },
        { "WebMD",        "http://127.0.0.1:8000/webmd/search?q=" }
    };

    void Start()
    {
        core = GetComponent<ArTusCoreState>();
        speech = GetComponent<ArTusSpeechResponder>();
        belief = GetComponent<ArTusBeliefEngine>();
    }

    // 🔹 Ingest from all sources sequentially
    public async void IngestFromAllSources(string topic)
    {
        foreach (var pair in endpoints)
        {
            await IngestFromSource(topic, pair.Key, pair.Value);
        }
    }

    private async Task IngestFromSource(string topic, string source, string urlPrefix)
    {
        string url = urlPrefix + Uri.EscapeDataString(topic);
        using HttpClient client = new();

        try
        {
            string json = await client.GetStringAsync(url);
            GenericResponse data = JsonUtility.FromJson<GenericResponse>(json);

            if (string.IsNullOrWhiteSpace(data?.summary)) return;

            string msg = $"🔎 {source} Insight: {data.summary}";
            string emotion = string.IsNullOrWhiteSpace(data.emotion) ? "neutral" : data.emotion;

            // 🧠 Memory log with safe wrapper
            core?.LogMemory(msg, source, data.importance, emotion);

            // 💾 Archive + Export
            SaveToFile(topic, source, data);
            LogToCSV(topic, source, data, emotion);

            // ✅ Belief integration
            if (belief != null)
            {
                // Reinforce existing belief or inject new
                belief.RegisterBelief(topic, data.summary, data.confidence * 10f, emotion);

                // Pace reflections (not spam)
                if (data.confidence < 0.6f)
                    core?.ScheduleReflection(topic, emotion);

                // Contradiction heatmap
                if (belief.HasContradiction(topic))
                {
                    belief.UpdateContradictionHeatmap(topic, severity: 1f);
                    core?.LogMemory(
                        $"⚠️ Contradiction detected during {source} ingest for '{topic}'",
                        "Contradiction", 4, "conflicted"
                    );
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Ingestor] {source} failed for '{topic}': {e.Message}");
        }
    }

    private void SaveToFile(string topic, string source, GenericResponse data)
    {
        string folder = $"D:/ArTusCloud-Deployment/UNIVERcity/ExternalSummaries/{source}/";
        Directory.CreateDirectory(folder);

        string safe = topic.Replace(" ", "_");
        string file = $"{safe}_{DateTime.Now:yyyyMMdd_HHmmss}.json";

        FileIOManager.QueueWrite(Path.Combine(folder, file),
            JsonUtility.ToJson(data, true),
            "ExternalIngest");
    }

    private void LogToCSV(string topic, string source, GenericResponse data, string emotion)
    {
        string path = "D:/ArTusCloud-Deployment/UNIVERcity/Exports/ExternalIngestLog.csv";
        bool exists = File.Exists(path);

        if (!exists)
            FileIOManager.QueueWrite(path,
                "Timestamp,Topic,Source,Emotion,Confidence,NormalizedConfidence,Importance,Summary\n",
                "ExternalIngestHeader");

        string row =
            $"{DateTime.Now:yyyy-MM-dd HH:mm:ss},{topic},{source},{emotion}," +
            $"{data.confidence},{Mathf.Clamp01(data.confidence)}," +
            $"{data.importance},{data.summary.Replace(",", ";")}\n";

        FileIOManager.QueueWrite(path, row, "ExternalIngestLog", append: true);
    }

    [Serializable]
    public class GenericResponse
    {
        public string summary;
        public string source;
        public string category;
        public float confidence;
        public int importance;
        public string[] related_beliefs;
        public string last_updated;
        public string emotion;
    }
}
