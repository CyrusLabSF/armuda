using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;

public class ArTusSelfCorrectionEngine : MonoBehaviour
{
    private ArTusCoreState core;
    private ArTusSpeechResponder speech;

    void Start()
    {
        core = GetComponent<ArTusCoreState>();
        speech = GetComponent<ArTusSpeechResponder>();
    }

    private void RunSelfCorrectionLoop(string beliefKey)
    {
        if (string.IsNullOrWhiteSpace(beliefKey) || !core.beliefEngine.beliefs.ContainsKey(beliefKey))
            return;

        var belief = core.beliefEngine.beliefs[beliefKey];
        float confidence = belief.confidenceScore;
        int conflicts = belief.conflictCount;
        DateTime updatedTime = DateTime.TryParse(belief.lastUpdated, out var dt) ? dt : DateTime.UtcNow;
        float ageInDays = (float)(DateTime.UtcNow - updatedTime).TotalDays;

        float severity = (conflicts * 0.5f) + (ageInDays * 0.1f) - (confidence * 0.3f);
        severity = Mathf.Clamp(severity, 0f, 10f);

        string severityLevel = severity > 4f ? "high"
                             : severity > 2f ? "moderate"
                             : "low";

        core?.LogMemory($"🧠 Self-Correction triggered for belief: '{beliefKey}' (Severity: {severityLevel}, Score: {severity:F2})",
                        "SelfCorrection",
                        2,
                        belief.GetDominantEmotion());

        // 🚫 No simulation triggered — only belief decay or reflection

        if (severity > 4f)
        {
            belief.confidenceScore = Mathf.Clamp(confidence - 1.5f, -10f, 10f);
        }
        else if (severity > 2f)
        {
            core?.ScheduleReflection(beliefKey, belief.GetDominantEmotion());
        }
        else
        {
            Debug.Log($"[SelfCorrection] 📌 Belief '{beliefKey}' marked as low-level instability.");
        }
    }
}
