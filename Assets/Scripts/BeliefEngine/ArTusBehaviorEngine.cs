using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

public class ArTusBehaviorEngine : MonoBehaviour
{
    [Header("Behavior Settings")]
    public float decayRate = 0.01f;
    public float driftThreshold = 1.5f;

    [Header("Intelligence")]
    public float activationThreshold = 3f;

    private ArTusCoreState core;
    private ArTusEmotionVisualRouter visualRouter;
    private ArTusIngestor ingestor;

    private readonly Dictionary<string, BehaviorData> behaviors = new();
    private readonly Dictionary<string, float> behaviorPressure = new();

    void Awake()
    {
        core = GetComponent<ArTusCoreState>();
        visualRouter = GetComponent<ArTusEmotionVisualRouter>();
        ingestor = GetComponent<ArTusIngestor>();
    }

    // ------------------------------------------------
    // REGISTRATION & REINFORCEMENT
    // ------------------------------------------------

    public void RegisterBehavior(
        string name,
        string context,
        float confidence,
        string emotion = "neutral"
    )
    {
        confidence = Mathf.Clamp(confidence, 0.1f, 10f);

        if (!behaviors.ContainsKey(name))
        {
            var behavior = new BehaviorData
            {
                name = name,
                context = context,
                confidenceScore = confidence,
                dominantEmotion = emotion,
                createdAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                lastUpdated = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                reinforcementCount = 1
            };

            behaviors[name] = behavior;
            behaviorPressure[name] = confidence;

            core?.LogMemory(
                $"🧠 New behavior formed: {name} ({context})",
                "Behavior",
                2,
                emotion
            );

            core?.QueueDeferredReflection(
                $"New behavior formed: {name} in context {context}",
                "BehaviorFormation",
                Mathf.Clamp01(confidence / 10f)
            );
        }
        else
        {
            var behavior = behaviors[name];
            float previousConfidence = behavior.confidenceScore;

            behavior.confidenceScore = Mathf.Clamp(
                behavior.confidenceScore + confidence,
                0f,
                10f
            );

            behavior.reinforcementCount++;
            behavior.lastUpdated = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            behavior.dominantEmotion = emotion;

            // Pressure accumulation
            if (!behaviorPressure.ContainsKey(name))
                behaviorPressure[name] = 0f;

            behaviorPressure[name] += confidence;

            CheckBehaviorDrift(name, behavior, previousConfidence);

            // Activate behavior if pressure builds
            if (behaviorPressure[name] > activationThreshold)
            {
                ActivateBehavior(name, behavior);
                behaviorPressure[name] = 0f;
            }

            if (behavior.reinforcementCount % 3 == 0)
            {
                core?.QueueDeferredReflection(
                    $"Behavior '{name}' reinforced {behavior.reinforcementCount} times",
                    "BehaviorReinforcement",
                    Mathf.Clamp01(behavior.confidenceScore / 10f)
                );
            }
        }
    }

    // ------------------------------------------------
    // BEHAVIOR ACTIVATION (NEW)
    // ------------------------------------------------

    private void ActivateBehavior(string name, BehaviorData behavior)
    {
        core?.LogMemory(
            $"🔥 Behavior activated: {name}",
            "BehaviorActivation",
            3,
            behavior.dominantEmotion
        );

        core?.QueueDeferredReflection(
            $"Behavior '{name}' reached activation threshold",
            "BehaviorActivation",
            Mathf.Clamp01(behavior.confidenceScore / 10f)
        );

        // 🔗 Behavior → Ingestor bridge
        if (ingestor != null)
        {
            ingestor.IngestSmartTopic(
                behavior.context,
                "behavior-driven",
                Mathf.Clamp01(behavior.confidenceScore / 10f)
            );
        }
    }

    // ------------------------------------------------
    // DECAY & DRIFT
    // ------------------------------------------------

    public void DecayBehaviors()
    {
        foreach (var kvp in behaviors)
        {
            var behavior = kvp.Value;
            float original = behavior.confidenceScore;

            float decayAmount = decayRate * behavior.GetDecayFactor();
            behavior.confidenceScore = Mathf.Max(
                0f,
                behavior.confidenceScore - decayAmount
            );

            if (behavior.confidenceScore < 1f && original >= 1f)
            {
                core?.QueueDeferredReflection(
                    $"Behavior '{behavior.name}' is fading",
                    "BehaviorDecay",
                    0.5f
                );
            }
        }
    }

    private void CheckBehaviorDrift(
        string name,
        BehaviorData behavior,
        float previousConfidence
    )
    {
        float drop = previousConfidence - behavior.confidenceScore;

        if (drop > driftThreshold)
        {
            string emotion = behavior.dominantEmotion ?? "conflicted";

            core?.LogMemory(
                $"⚠️ Behavior drift: {name} (Δ-{drop:F2})",
                "BehaviorDrift",
                3,
                emotion
            );

            core?.QueueDeferredReflection(
                $"Behavior '{name}' shows instability",
                "BehaviorDrift",
                Mathf.Clamp01(drop / 5f)
            );
        }
    }

    // ------------------------------------------------
    // QUERY & EXPORT
    // ------------------------------------------------

    public BehaviorData GetBehavior(string name)
    {
        return behaviors.TryGetValue(name, out var b) ? b : null;
    }

    public List<BehaviorData> GetAllBehaviors()
    {
        return behaviors.Values.ToList();
    }

    public BehaviorData GetDominantBehavior()
    {
        if (behaviors.Count == 0) return null;

        return behaviors.Values
            .OrderByDescending(b => b.confidenceScore)
            .First();
    }

    public void ExportBehaviorSnapshot()
    {
        var wrapper = new BehaviorSnapshot
        {
            timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            behaviors = behaviors.Values.ToList()
        };

        string json = JsonUtility.ToJson(wrapper, true);
        FileIOHelper.SaveJson(
            "behavior_snapshots",
            $"BehaviorSnapshot_{DateTime.Now:yyyyMMdd_HHmm}",
            json,
            delay: 1f
        );
    }

    // ------------------------------------------------
    // DATA MODELS
    // ------------------------------------------------

    [Serializable]
    public class BehaviorData
    {
        public string name;
        public string context;
        public float confidenceScore;
        public string dominantEmotion;
        public string createdAt;
        public string lastUpdated;
        public int reinforcementCount;

        public float GetDecayFactor()
        {
            return Mathf.Clamp01(confidenceScore / 10f);
        }
    }

    [Serializable]
    public class BehaviorSnapshot
    {
        public string timestamp;
        public List<BehaviorData> behaviors;
    }
}