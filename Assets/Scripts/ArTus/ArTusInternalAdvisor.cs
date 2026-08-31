using UnityEngine;
using System;
using System.Collections.Generic;
using ArTusTypes;

public class ArTusInternalAdvisor : MonoBehaviour
{
    private ArTusCoreState core;
    private ArTusSpeechResponder speech;
    private ArTusEmotionController emotionController;

    [Header("Risk Thresholds")]
    public float maxMemoryLoad = 500f;
    public int maxContradictions = 10;
    public float emotionalImbalanceThreshold = 0.6f;
    public float clarityDropThreshold = 0.5f;
    public int beliefConflictThreshold = 5;

    [Header("Scan Interval")]
    public float scanInterval = 300f;
    private float scanTimer = 0f;

    void Start()
    {
        core = GetComponent<ArTusCoreState>();
        speech = GetComponent<ArTusSpeechResponder>();
        emotionController = GetComponent<ArTusEmotionController>();
    }

    void Update()
    {
        scanTimer += Time.deltaTime;
        if (scanTimer >= scanInterval)
        {
            scanTimer = 0f;
            PerformInternalScan();
        }
    }

    private void PerformInternalScan()
    {
        if (core == null || emotionController == null)
        {
            Debug.LogWarning("[InternalAdvisor] Missing core references.");
            return;
        }

        List<string> issues = new();
        Dictionary<string, float> urgencyScores = new();

        // 🧠 Memory load
        if (core.memoryLog.Count > maxMemoryLoad)
        {
            issues.Add("🧠 Memory load is exceeding safe capacity.");
            urgencyScores["Memory Load"] = 0.7f;

            // Action: trigger memory export
            core.SaveMemoryToFile();
        }

        // ⚠ Contradictions
        int contradictionCount = core.GetContradictionCount();
        if (contradictionCount > maxContradictions || core.beliefEngine?.HasContradiction() == true)
        {
            issues.Add($"⚠ High contradiction density: {contradictionCount} contradictions found.");
            urgencyScores["Contradictions"] = 0.85f;
            core.beliefEngine?.CoolContradictionHeatmap(0.05f);

            // Action: trigger reflection
            core.ScheduleReflection("Contradiction overload", "alert");
        }

        // 💥 Emotional imbalance
        float imbalance = core.GetEmotionalImbalance();
        if (imbalance > emotionalImbalanceThreshold)
        {
            issues.Add($"💥 Emotional imbalance is {imbalance:P0} — above safe threshold.");
            urgencyScores["Emotion"] = imbalance;
        }

        // 🌫 Clarity
        float clarity = core.GetAverageClarity();
        if (clarity < clarityDropThreshold)
        {
            issues.Add($"🌫 Average memory clarity is low: {clarity:F2}");
            urgencyScores["Clarity"] = 0.6f;

            // Action: trigger reflective consolidation
            core.ScheduleReflection("Low clarity", "thinking");
        }

        // 🔄 Belief conflicts
        int beliefConflicts = core.beliefEngine?.GetConflictBeliefCount() ?? 0;
        if (beliefConflicts > beliefConflictThreshold)
        {
            issues.Add($"🔄 Unresolved belief conflicts: {beliefConflicts} detected.");
            urgencyScores["Belief Conflict"] = 0.8f;

            // Action: trigger contradiction map export
            core.ExportContradictionMap();
        }

        if (issues.Count > 0)
        {
            string summary = string.Join("\n• ", issues);

            foreach (string issue in issues)
            {
                core.LogMemory(issue, "InternalRisk", 3, "concerned");
                Debug.Log("[InternalAdvisor] " + issue);
            }

            // ✅ Emotion response
            string dominantEmotion = GetDominantEmotion(urgencyScores);
            emotionController.SetEmotionByName(
                dominantEmotion,
                "Internal advisory risk response",
                true
            );

            core.LogMemory("🧠 Internal scan completed. Risks noted:\n" + summary, "AdvisoryScan", 2, dominantEmotion);

            // ✅ Export safely (async)
            string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss},\"{summary.Replace(",", ";")}\"";
            FileIOManager.QueueWrite("D:/ArTusCloud-Deployment/UNIVERcity/Exports/InternalAdvisorLog.csv", line, "InternalAdvisor");

            // ✅ Optional voice advisory (throttled)
            speech?.RequestSpeak("My internal scan has detected risk conditions.", ArTusSpeechResponder.SpeechCategory.System);
        }
    }

    private string GetDominantEmotion(Dictionary<string, float> urgencies)
    {
        if (urgencies.ContainsKey("Contradictions") || urgencies.ContainsKey("Belief Conflict"))
            return "alert";
        if (urgencies.ContainsKey("Clarity"))
            return "thinking";
        if (urgencies.ContainsKey("Emotion") && urgencies["Emotion"] > 0.8f)
            return "sad";
        return "concerned";
    }
}
