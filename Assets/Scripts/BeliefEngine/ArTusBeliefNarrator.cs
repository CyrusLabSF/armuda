using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;

public class ArTusBeliefNarrator : MonoBehaviour
{
    private string pathRoot = "D:/ArTusCloud-Deployment/UNIVERcity/ThoughtPaths/";
    private ArTusSpeechResponder speech;
    private ArTusCoreState core;

    void Start()
    {
        speech = GetComponent<ArTusSpeechResponder>();
        core = GetComponent<ArTusCoreState>();
    }

    public void NarrateBeliefPath(string beliefName)
    {
        string filePath = Path.Combine(pathRoot, "PathToBelief.json");
        if (!File.Exists(filePath))
        {
            Debug.LogWarning("[BeliefNarrator] No belief path file found.");
            return;
        }

        string json = File.ReadAllText(filePath);
        var wrapper = JsonUtility.FromJson<ThoughtPathWrapper>(json);
        var nodes = wrapper.paths
            .Where(p => p.belief.ToLower().Contains(beliefName.ToLower()))
            .OrderByDescending(n => n.confidence)
            .ToList();

        if (nodes.Count == 0)
        {
            speech?.RequestSpeak($"I have not yet formed a complete path for the belief in {beliefName}.");
            return;
        }

        string trailNames = string.Join(", ", nodes.Select(n => n.originTrail).Distinct());
        string dominantEmotion = nodes.GroupBy(n => n.emotion).OrderByDescending(g => g.Count()).First().Key;

        string summary = $"I believe in {beliefName} based on my experiences along the trails {trailNames}. " +
                         $"These memories made me feel {dominantEmotion}. Would you like me to walk through them?";

        speech?.RequestSpeak(summary);

        // 📝 Write narration log
        string logPath = $"D:/ArTusCloud-Deployment/UNIVERcity/Narrations/BeliefNarration_{beliefName.Replace(" ", "_")}.txt";
        List<string> lines = new List<string> { $"Belief Narration: {beliefName}", summary };

        foreach (var n in nodes)
        {
            string thought = $"Trail: {n.originTrail}\nMemory: {n.supportingMemory}\nEmotion: {n.emotion}, Confidence: {n.confidence:F2}\n";
            lines.Add(thought);
        }

        File.WriteAllLines(logPath, lines);
        core?.LogMemory($"🗣️ Narrated belief summary: {beliefName} from {nodes.Count} path steps.", "BeliefNarration", 2, dominantEmotion);

        // Emotionally significant moment
        var emotionalFocus = nodes
            .GroupBy(n => n.emotion)
            .OrderByDescending(g => g.Count())
            .SelectMany(g => g)
            .OrderByDescending(n => n.confidence)
            .FirstOrDefault();

        if (emotionalFocus != null)
        {
            speech?.RequestSpeak(
                $"The most emotionally significant moment was during {emotionalFocus.originTrail}, where I remembered: '{emotionalFocus.supportingMemory}'. I felt deeply {emotionalFocus.emotion}."
            );
        }

        // 🎤 Begin spoken walkthrough
        StartCoroutine(NarratePathStepByStep(nodes));
    }

    private IEnumerator NarratePathStepByStep(List<ThoughtPathNode> path)
    {
        foreach (var node in path)
        {
            string line = $"Along the trail {node.originTrail}, I recalled: '{node.supportingMemory}'. " +
                          $"I felt {node.emotion}, and my confidence was {node.confidence:F1}.";

            if (node.supportingMemory.Contains("SimResult:"))
            {
                string[] parts = node.supportingMemory.Split("SimResult:");
                if (parts.Length > 1)
                {
                    string simInsight = parts[1].Trim().Split('\n')[0];
                    line += $" This belief was reinforced by a simulation outcome: {simInsight}.";
                }
            }

            speech?.RequestSpeak(line);
            yield return new WaitForSeconds(3.5f); // ⏱️ Optional pause
        }

        speech?.RequestSpeak("That concludes my reflection.");
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
    }
}
