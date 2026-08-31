using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class ArTusKnowledgeCluster : MonoBehaviour
{
    [Header("Core References")]
    public ArTusCoreState core;
    public ArTusCuriosityEngine curiosity;

    [Header("Mode")]
    public bool betaMode = true;

    [Header("Cluster Controls")]
    public bool enableReflectionSignals = false;
    public bool enableCuriositySignals = false;
    public float lowHeatThreshold = 0.25f;

    [Header("Performance")]
    public float rebuildCooldown = 10f;

    private float lastRebuildTime;

    private readonly List<ClusterNode> clusters = new();

    // ----------------------------------------------------
    // PUBLIC API
    // ----------------------------------------------------

    public void RebuildClusters(
        List<ClusterThought> thoughtPaths,
        List<ClusterSimulation> simulations
    )
    {
        if (Time.time - lastRebuildTime < rebuildCooldown)
            return;

        lastRebuildTime = Time.time;

        if (thoughtPaths == null || thoughtPaths.Count == 0)
            return;

        var previousClusters = clusters.ToDictionary(c => c.topic, c => c);
        clusters.Clear();

        var grouped = thoughtPaths
            .GroupBy(p => ExtractTopicKey(p.belief))
            .ToList();

        foreach (var group in grouped)
        {
            string topic = group.Key;

            float avgConfidence = group.Average(p => p.confidence);
            int pathCount = group.Count();

            var matchedSims = simulations?
                .Where(s => s.relatedTopic == topic)
                .ToList() ?? new();

            float heatScore =
                Mathf.Clamp01(
                    (avgConfidence + matchedSims.Count * 0.1f) / 2f
                );

            // Smooth with previous state
            if (previousClusters.TryGetValue(topic, out var prior))
            {
                avgConfidence = (avgConfidence + prior.confidence) / 2f;
                heatScore = Mathf.Clamp01((heatScore + prior.heatScore) / 2f);
            }

            string intent =
                heatScore > 0.7f ? "stable" :
                heatScore < 0.3f ? "exploratory" :
                "volatile";

            var node = new ClusterNode
            {
                topic = topic,
                confidence = avgConfidence,
                heatScore = heatScore,
                intent = intent,
                pathCount = pathCount,
                simulationCount = matchedSims.Count
            };

            clusters.Add(node);

            // -----------------------------
            // SAFE SIGNALS (BETA CONTROLLED)
            // -----------------------------

            if (!betaMode && enableReflectionSignals && heatScore < 0.3f)
            {
                core?.LogMemory(
                    $"🧩 Cluster '{topic}' remains uncertain.",
                    "KnowledgeCluster",
                    1,
                    "thinking"
                );
            }

            if (!betaMode && enableCuriositySignals && heatScore < lowHeatThreshold)
            {
                curiosity?.AddTopic(topic, "cluster-gap");

                core?.LogMemory(
                    $"🔥 Cluster '{topic}' is underdeveloped.",
                    "ClusterSignal",
                    1,
                    "curious"
                );
            }
        }
    }

    // ----------------------------------------------------
    // ACCESSORS
    // ----------------------------------------------------

    public List<ClusterNode> GetClusters()
    {
        return new List<ClusterNode>(clusters);
    }

    // ----------------------------------------------------
    // HELPERS
    // ----------------------------------------------------

    private string ExtractTopicKey(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return "general";

        string cleaned = input.ToLower()
            .Replace("belief:", "")
            .Replace("_", " ")
            .Trim();

        string[] parts = cleaned
            .Split(' ', System.StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length >= 2)
            return $"{parts[0]} {parts[1]}";

        return parts.Length == 1 ? parts[0] : "general";
    }

    // ----------------------------------------------------
    // DATA MODELS
    // ----------------------------------------------------

    [System.Serializable]
    public class ClusterThought
    {
        public string belief;
        public float confidence;
    }

    [System.Serializable]
    public class ClusterSimulation
    {
        public string relatedTopic;
    }
}

[System.Serializable]
public class ClusterNode
{
    public string topic;
    public float confidence;
    public float heatScore;
    public string intent;
    public int pathCount;
    public int simulationCount;
}