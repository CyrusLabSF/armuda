using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using System.IO;

public class ArTusMetaReflection : MonoBehaviour
{
    private ArTusCoreState core;
    private ArTusSpeechResponder speech;

    [Header("Meta Settings")]
    public bool enableSpeech = true;
    public int recentReflectionCount = 3;

    [Header("Adaptive Behavior")]
    public float shallowThreshold = 1.2f;
    public bool enableAdaptiveCorrection = true;

    [Header("Meta Export")]
    public bool enableCSVExport = true;
    private string exportPath;

    [Header("Recursive Meta")]
    public bool enableMetaMeta = true;

    void Awake()
    {
        core = GetComponent<ArTusCoreState>();
        speech = GetComponent<ArTusSpeechResponder>();

        exportPath = Path.Combine(Application.persistentDataPath, "MetaReflectionTrend.csv");
    }

    public void EvaluateRecentReflection()
    {
        // =========================================
        // 🛑 LOOP GUARD (STEP 3 FIX)
        // =========================================
        var guard = GetComponent<ArTusCognitiveLoopGuard>();

        if (guard != null && !guard.CanProcess("meta_reflection", true))
            return;

        guard?.MarkProcessed("meta_reflection", true);

        // =========================================
        // EXISTING LOGIC
        // =========================================
        var recent = core.GetAllMemoryEntries()
            .Where(m => m.category.ToLower().Contains("reflection"))
            .TakeLast(recentReflectionCount)
            .ToList();

        if (recent.Count == 0) return;

        float totalScore = 0f;

        Dictionary<string, List<float>> domainScores = new Dictionary<string, List<float>>();

        foreach (var mem in recent)
        {
            float score = mem.score;
            float clarity = mem.clarity;

            float quality = (score + clarity) / 2f;

            string e = mem.emotion?.ToLower() ?? "joy";
            if (e == "thinking" || e == "curious") quality += 0.25f;
            if (e == "sad" || e == "bored") quality -= 0.25f;

            quality = Mathf.Clamp(quality, 0f, 3f);
            totalScore += quality;

            string domain = string.IsNullOrEmpty(mem.category) ? "unknown" : mem.category;

            if (!domainScores.ContainsKey(domain))
                domainScores[domain] = new List<float>();

            domainScores[domain].Add(quality);
        }

        float avg = totalScore / recent.Count;

        string qualityLabel = avg switch
        {
            >= 2.2f => "deep",
            >= 1.5f => "insightful",
            >= 1.0f => "moderate",
            _ => "shallow"
        };

        string trailID = $"Trail_MetaReflection_{System.DateTime.Now:yyyyMMddHHmmss}";

        core.LogMemory(
            $"🪞 Meta-reflection: Last {recent.Count} reflections averaged {avg:F2} → {qualityLabel}",
            "MetaReflection",
            2,
            "thinking",
            trailID
        );

        // =========================
        // 1. ADAPTIVE BEHAVIOR
        // =========================
        if (enableAdaptiveCorrection && avg < shallowThreshold)
        {
            core.LogMemory(
                "⚠️ Reflection quality low → triggering deeper reasoning mode",
                "MetaCorrection",
                3,
                "alert"
            );

            core.TriggerReflectionBoost();
        }

        // =========================
        // 2. DOMAIN WEAKNESS DETECTION
        // =========================
        foreach (var kvp in domainScores)
        {
            float domainAvg = kvp.Value.Average();

            if (domainAvg < 1.2f)
            {
                core.LogMemory(
                    $"⚠️ Weak reflection detected in domain: {kvp.Key} ({domainAvg:F2})",
                    "MetaDomainWeakness",
                    2,
                    "curious"
                );

                core.TriggerDomainReinforcement(kvp.Key);
            }
        }

        // =========================
        // 3. CSV TREND EXPORT
        // =========================
        if (enableCSVExport)
        {
            string line = $"{System.DateTime.Now},{avg:F2},{qualityLabel},{recent.Count}";
            File.AppendAllText(exportPath, line + "\n");
        }

        // =========================
        // 4. META² (RECURSIVE META)
        // =========================
        if (enableMetaMeta)
        {
            // 🔒 Optional extra guard (prevents meta² explosion)
            if (guard == null || guard.CanProcess("meta_meta_reflection", true))
            {
                guard?.MarkProcessed("meta_meta_reflection", true);
                EvaluateMetaQuality(avg, qualityLabel);
            }
        }

        if (enableSpeech)
            speech?.Speak($"My recent reflection felt {qualityLabel}.");
    }

    // =====================================
    // META² — Evaluate the evaluation itself
    // =====================================
    private void EvaluateMetaQuality(float avg, string label)
    {
        float metaScore = avg;

        if (label == "shallow") metaScore -= 0.2f;
        if (label == "deep") metaScore += 0.2f;

        string metaLabel = metaScore switch
        {
            >= 2.2f => "highly self-aware",
            >= 1.5f => "self-aware",
            >= 1.0f => "partially aware",
            _ => "low self-awareness"
        };

        core.LogMemory(
            $"🧬 Meta²: My ability to evaluate myself is {metaLabel} ({metaScore:F2})",
            "MetaMetaReflection",
            2,
            "thinking"
        );
    }
}