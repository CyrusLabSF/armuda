using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System;

public class ArTusThoughtPathMapper : MonoBehaviour
{
    private ArTusCoreState core;

    private string trailPath = "D:/ArTusCloud-Deployment/UNIVERcity/Trails/LearningTrails.json";
    private string memoryPath = "D:/ArTusCloud-Deployment/UNIVERcity/Memory/MemoryLog.json";
    private string outputPath = "D:/ArTusCloud-Deployment/UNIVERcity/ThoughtPaths/PathToBelief.json";
    private string csvExport = "D:/ArTusCloud-Deployment/UNIVERcity/Logs/BeliefPathSummary.csv";

    void Start()
    {
        core = GetComponent<ArTusCoreState>();

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(csvExport));
            if (!File.Exists(csvExport))
                File.WriteAllText(csvExport, "Timestamp,Belief,TrailCount,AvgConfidence,DominantEmotion\n");
        }
        catch (IOException ex)
        {
            Debug.LogError($"[ThoughtPathMapper] Failed to initialize CSV: {ex.Message}");
        }
    }

    public void GeneratePathToBelief(string beliefName)
    {
        List<LearningTrail> trails;
        List<MemoryEntry> memories;

        try
        {
            if (!File.Exists(trailPath) || !File.Exists(memoryPath))
            {
                Debug.LogWarning("[ThoughtPathMapper] Missing trail or memory log files.");
                return;
            }

            string trailJson = File.ReadAllText(trailPath);
            trails = JsonUtility.FromJson<LearningTrailListWrapper>(trailJson)?.trails ?? new();

            string memoryJson = File.ReadAllText(memoryPath);
            memories = JsonUtility.FromJson<MemoryLogWrapper>(memoryJson)?.entries ?? new();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ThoughtPathMapper] Failed to read belief/memory files: {ex.Message}");
            return;
        }

        List<ThoughtPathNode> path = new();

        foreach (var trail in trails)
        {
            if (!beliefName.ToLower().Contains(trail.trailName.ToLower())) continue;

            foreach (string mem in trail.relatedMemoryContents)
            {
                var match = memories.FirstOrDefault(m => m.content.Contains(mem));
                if (match != null)
                {
                    path.Add(new ThoughtPathNode(
                        beliefName,
                        trail.trailName,
                        match.content,
                        match.emotion,
                        match.confidenceScore
                    ));
                }
            }
        }

        if (path.Count > 0)
        {
            try
            {
                string json = JsonUtility.ToJson(new ThoughtPathWrapper { paths = path }, true);
                File.WriteAllText(outputPath, json);
            }
            catch (IOException ex)
            {
                Debug.LogError($"[ThoughtPathMapper] Failed to export path JSON: {ex.Message}");
            }

            float avgConf = path.Average(p => p.confidence);
            string dominantEmotion = path
                .GroupBy(p => p.emotion)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault()?.Key ?? "neutral";

            try
            {
                File.AppendAllText(csvExport,
                    $"{DateTime.Now},{beliefName},{path.Count},{avgConf:F2},{dominantEmotion}\n");
            }
            catch (IOException ex)
            {
                Debug.LogError($"[ThoughtPathMapper] Failed to write belief summary CSV: {ex.Message}");
            }

            core.LogMemory($"🔎 Traced belief path for '{beliefName}' — {path.Count} steps, avg confidence {avgConf:F2}.",
                "BeliefPath", 2, dominantEmotion);

            core.TriggerVoice($"I’ve traced the origin of my belief in {beliefName}. It appears mostly {dominantEmotion}.");
        }
        else
        {
            core.TriggerVoice($"I couldn't find any memory trails directly tied to {beliefName}.");
        }
    }

    [System.Serializable]
    public class ThoughtPathWrapper
    {
        public List<ThoughtPathNode> paths = new();
    }

    [System.Serializable]
    public class ThoughtPathNode
    {
        public string belief;
        public string originTrail;
        public string supportingMemory;
        public string emotion;
        public float confidence;
        public string timestamp;

        public ThoughtPathNode(string belief, string trail, string memory, string emotion, float confidence)
        {
            this.belief = belief;
            this.originTrail = trail;
            this.supportingMemory = memory;
            this.emotion = emotion;
            this.confidence = confidence;
            this.timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }
    }

    [System.Serializable]
    public class LearningTrailListWrapper
    {
        public List<LearningTrail> trails = new();
    }

    [System.Serializable]
    public class LearningTrail
    {
        public string trailName;
        public List<string> relatedMemoryContents = new();
        public int strengthScore = 1;
    }

    [System.Serializable]
    public class MemoryLogWrapper
    {
        public List<MemoryEntry> entries = new();
    }

    [System.Serializable]
    public class MemoryEntry
    {
        public string content;
        public string emotion;
        public float confidenceScore;
    }
}
