using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using ArTusTypes;

[RequireComponent(typeof(ArTusCoreState))]
public class ArTusTeachModeHandler : MonoBehaviour
{
    private ArTusCoreState core;
    private ArTusSpeechResponder speech;

    [Header("Teach Mode")]
    public bool isTeachingMode = false;
    public float defaultConfidence = 1.0f;
    public string teachExportPath = "D:/ArTusCloud-Deployment/UNIVERcity/Exports/TeachLog.csv";

    private List<string> taughtBeliefs = new();

    void Start()
    {
        core = GetComponent<ArTusCoreState>();
        speech = GetComponent<ArTusSpeechResponder>();
    }

    public void EnableTeachMode()
    {
        isTeachingMode = true;
        core?.LogMemory("🧠 Teach mode activated.", "TeachMode", 1, "focused");
    }

    public void DisableTeachMode()
    {
        isTeachingMode = false;
        core?.LogMemory("🧠 Teach mode deactivated.", "TeachMode", 1, "neutral");
    }

    public void HandleTeachInput(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return;

        string prefix = "artus, remember this:";
        bool isTeachCommand = input.ToLower().StartsWith(prefix);

        // ✳ Free-form query support
        if (!isTeachCommand && (input.ToLower().StartsWith("why") || input.ToLower().StartsWith("what is")))
        {
            core?.RequestDialogueResponse(input);
            return;
        }

        if (!isTeachingMode || !isTeachCommand) return;

        string content = input.Substring(prefix.Length).Trim();
        if (string.IsNullOrWhiteSpace(content)) return;

        float clarity = core?.GetAverageMemoryClarity() ?? 0.6f;
        float confidence = Mathf.Clamp(defaultConfidence * clarity, 0.3f, 1.2f);

        if (!core.beliefs.ContainsKey(content))
        {
            var newBelief = new BeliefNode(content, confidence, "trust")
            {
                origin = "teach",
                domain = "General",
                description = content,
                relatedTrails = new List<string> { "user_instructions" },
                reinforcementCount = 1,
                contradictionCount = 0,
                emotionSpread = new List<string> { "trust" }
            };

            core.beliefs[content] = newBelief;

            core?.LogMemory($"User taught: {content}", "TeachMode", 5, "trust");

            core?.PromoteBelief(new BeliefMemoryEntry
            {
                topic = content,
                confidence = confidence,
                description = content,
                domain = "General",
                origin = "teach",
                dominantEmotion = "trust",
                supportingTrail = "TeachTrail"
            });

            taughtBeliefs.Add(content);
            ExportToCsv(content, confidence);
            core?.ScheduleReflection($"Reflect on taught belief: {content}", "trust");
        }
    }

    private void ExportToCsv(string belief, float confidence)
    {
        try
        {
            bool exists = File.Exists(teachExportPath);
            using StreamWriter writer = new(teachExportPath, true);

            if (!exists)
                writer.WriteLine("Timestamp,Belief,Confidence");

            writer.WriteLine($"\"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\",\"{belief}\",{confidence:F2}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[TeachMode] Failed to export: {ex.Message}");
        }
    }

    public void SummarizeTaughtBeliefs()
    {
        if (taughtBeliefs.Count == 0)
        {
            core?.LogMemory("Teach summary requested — no beliefs taught yet.", "TeachSummary", 1, "neutral");
            return;
        }

        string summary = $"🧾 Teach summary: {taughtBeliefs.Count} beliefs taught. Most recent: {taughtBeliefs[^1]}";
        core?.LogMemory(summary, "TeachSummary", 2, "reflective");
    }
}
