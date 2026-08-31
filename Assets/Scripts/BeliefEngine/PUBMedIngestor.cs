using UnityEngine;
using System;
using System.Net.Http;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;

public class PubMedIngestor : MonoBehaviour
{
    private ArTusCoreState core;
    private ArTusSpeechResponder speech;

    void Start()
    {
        core = GetComponent<ArTusCoreState>();
        speech = GetComponent<ArTusSpeechResponder>();
    }

    public async void IngestFromPubMed(string topic)
    {
        if (string.IsNullOrWhiteSpace(topic)) return;

        string url = $"http://127.0.0.1:8000/pubmed/search?q={Uri.EscapeDataString(topic)}";
        using HttpClient client = new();

        try
        {
            string json = await client.GetStringAsync(url);
            PubMedResponse data = JsonUtility.FromJson<PubMedResponse>(json);

            if (string.IsNullOrWhiteSpace(data?.summary))
            {
                speech?.TriggerVoice($"I couldn’t find a useful PubMed entry for {topic}.");
                core?.LogMemory($"❌ PubMed entry missing or empty for topic: '{topic}'", "PubMed", 1, "neutral");
                return;
            }

            string content = $"🧬 PubMed Research Summary ({topic}): {data.summary}";
            string emotion = string.IsNullOrWhiteSpace(data.emotion) ? "neutral" : data.emotion;
            string domain = InferMedicalDomain(topic); // 🧠 Dynamic domain tagging

            core?.LogMemory(content, domain, data.importance, emotion);

            SaveSummaryToFile(topic, data);
            GetComponent<ArTusBeliefEngine>()?.LogTopicBelief(topic, emotion);

            // 🧠 Trigger reflection if uncertain
            if (data.confidence < 0.6f)
                core?.ScheduleReflection(topic, emotion);  // ✅ Replaced deprecated method
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[🧬 PubMed] Ingestion failed for '{topic}': {e.Message}");
            speech?.TriggerVoice($"Something went wrong while learning about {topic}.");
        }
    }

    // 📂 Save result to disk
    private void SaveSummaryToFile(string topic, PubMedResponse data)
    {
        string folder = "D:/ArTusCloud-Deployment/UNIVERcity/ExternalSummaries/PubMed/";
        Directory.CreateDirectory(folder);

        string safe = topic.Replace(" ", "_").Replace("/", "_");
        string path = Path.Combine(folder, $"{safe}_{DateTime.Now:yyyyMMdd_HHmmss}.json");

        File.WriteAllText(path, JsonUtility.ToJson(data, true));
        Debug.Log($"[🧬 PubMed] Saved research snippet for '{topic}'.");
    }

    // 🧠 Infer medical domain from keyword
    private string InferMedicalDomain(string topic)
    {
        topic = topic.ToLowerInvariant();

        if (topic.Contains("cancer") || topic.Contains("tumor")) return "Oncology";
        if (topic.Contains("gene") || topic.Contains("genome")) return "Genetics";
        if (topic.Contains("brain") || topic.Contains("neuro")) return "Neuroscience";
        if (topic.Contains("heart") || topic.Contains("cardio")) return "Cardiology";
        if (topic.Contains("virus") || topic.Contains("pathogen")) return "Virology";
        if (topic.Contains("cell") || topic.Contains("microscope")) return "Cell Biology";
        if (topic.Contains("immune")) return "Immunology";

        return "MedicalResearch";
    }

    [Serializable]
    public class PubMedResponse
    {
        public string topic;
        public string summary;
        public string source;
        public float confidence;
        public int importance;
        public string[] related_beliefs;
        public string last_updated;
        public string emotion;
    }
}
