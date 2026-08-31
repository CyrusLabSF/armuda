using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using ArTusTypes; // ✅ Ensure BeliefMemoryEntry is recognized

public class ArTusConceptFusionEngine : MonoBehaviour
{
    private ArTusCoreState core;
    private ArTusUNIVERcityIndexer indexer;
    private ArTusSpeechResponder speech;

    [Header("Fusion Settings")]
    [Tooltip("Minimum clarity to include in fusion candidates")]
    [SerializeField] private float clarityThreshold = 0.4f;

    void Awake()
    {
        core = GetComponent<ArTusCoreState>();
        indexer = GetComponent<ArTusUNIVERcityIndexer>();
        speech = GetComponent<ArTusSpeechResponder>();
    }

    public void GenerateFusionInsight()
    {
        if (indexer == null || indexer.index.Count < 2)
        {
            Debug.LogWarning("[ConceptFusion] Not enough concepts to fuse.");
            return;
        }

        var candidates = indexer.index
            .Where(c => !string.IsNullOrWhiteSpace(c.topic) &&
                        !string.IsNullOrWhiteSpace(c.category) &&
                        c.clarity >= clarityThreshold) // ✅ Ensure clarity is a float
            .ToList();

        if (candidates.Count < 2) return;

        var a = candidates[Random.Range(0, candidates.Count)];
        var b = candidates[Random.Range(0, candidates.Count)];

        // ✅ Ensure different topic/category
        int safeguard = 5;
        while ((a.topic == b.topic || a.category == b.category) && safeguard-- > 0)
            b = candidates[Random.Range(0, candidates.Count)];

        string emotionBridge = a.emotion == b.emotion ? a.emotion : "curious";

        string fusionInsight = $"🧠 Although '{a.topic}' comes from {a.category} and '{b.topic}' from {b.category}, I feel they connect through {emotionBridge}.";

        speech?.Speak(fusionInsight);
        core?.LogMemory($"Fusion Insight: {fusionInsight}", "ConceptFusion", 3, emotionBridge);

        core?.PromoteBelief(new BeliefMemoryEntry
        {
            topic = $"Connection between '{a.topic}' and '{b.topic}'",
            confidence = 0.55f,
            description = fusionInsight,
            domain = "ConceptFusion",
            origin = "fusion-engine",
            dominantEmotion = emotionBridge,
            supportingTrail = $"Fusion_{a.topic}_{b.topic}"
        });

        Debug.Log($"[ConceptFusion] ✅ {fusionInsight}");
    }
}
