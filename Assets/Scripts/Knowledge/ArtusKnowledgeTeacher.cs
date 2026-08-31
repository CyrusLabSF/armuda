using UnityEngine;
using System.IO;
using System.Collections.Generic;
using System;
using System.Linq;

public class ArTusKnowledgeTeacher : MonoBehaviour
{
    private ArTusCoreState core;
    private ArTusThreatPatternEngine patterns;
    private ArTusOntologyEngine ontology;
    private ArTusSpeechResponder speech;

    [Header("Mode")]
    public bool betaMode = true;

    [Header("Behavior")]
    public bool enableSpeech = false;
    public float teachingCooldown = 10f;

    private float lastTeachTime;

    // =========================================================
    // PATHS (WEBGL SAFE)
    // =========================================================

    [Header("Output Settings")]
    [SerializeField]
    private string teachingRootRelative = "UNIVERcity/Teaching";

    [SerializeField]
    private string briefsJsonRelative = "UNIVERcity/Teaching/BriefsJson";

    private string TeachingRootPath =>
        ArTusPathUtility.GetPersistent(teachingRootRelative);

    private string JsonExportPath =>
        ArTusPathUtility.GetPersistent(briefsJsonRelative);

    void Start()
    {
        core = GetComponent<ArTusCoreState>();
        patterns = GetComponent<ArTusThreatPatternEngine>();
        ontology = GetComponent<ArTusOntologyEngine>();
        speech = GetComponent<ArTusSpeechResponder>();

        try
        {
            Directory.CreateDirectory(TeachingRootPath);
            Directory.CreateDirectory(JsonExportPath);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[KnowledgeTeacher] Init failed: {ex.Message}");
        }
    }

    // =========================================================
    // TEACH A SINGLE TOPIC
    // =========================================================
    public void TeachTopic(string label, string type = "cve")
    {
        if (Time.time - lastTeachTime < teachingCooldown)
            return;

        lastTeachTime = Time.time;

        if (string.IsNullOrWhiteSpace(label) || patterns == null)
            return;

        if (!patterns.TryGetPattern(type, label, out var entry))
        {
            if (!betaMode && enableSpeech)
                speech?.TriggerVoice($"I haven’t learned enough about {label} yet.");
            return;
        }

        var ont = ontology?.GetOntologyInfo(label);
        string category = ont?.category ?? "Uncategorized";
        string superclass = ont?.superclass ?? "General Security";
        string trailID = $"Trail_Teaching_{label.Replace(" ", "_")}";

        string explanation =
            $"🧠 Knowledge Brief: {label}\n" +
            $"Type: {type.ToUpper()}, Category: {category}, Domain: {superclass}\n\n" +
            $"Observed {entry.count} times with confidence {entry.confidence:F2}.\n" +
            $"Contradictions: {entry.contradictionCount}\n\n" +
            $"Explanation:\n{GenerateExplanation(label, type, entry.confidence)}";

        SaveBrief(label, explanation, category);
        SaveBriefAsJson(label, explanation, category, entry.confidence, trailID);

        if (!betaMode)
        {
            core?.LogMemory(
                $"📚 Generated teaching brief for: {label}",
                "Teaching",
                1,
                "confident",
                trailID
            );
        }

        if (!betaMode && enableSpeech)
            speech?.TriggerVoice($"Here’s what I know about {label}. The brief is ready.");
    }

    // =========================================================
    // EXPLANATION ENGINE
    // =========================================================
    private string GenerateExplanation(string label, string type, float confidence)
    {
        string lower = label.ToLowerInvariant();

        if (type == "cve")
        {
            if (lower.Contains("overflow"))
                return "A buffer overflow allows an attacker to overwrite memory, often leading to code execution.";
            if (lower.Contains("injection"))
                return "Injection vulnerabilities allow untrusted input to be executed as commands or queries.";
            if (lower.Contains("escalation"))
                return "Privilege escalation allows a user to gain unauthorized access.";
        }

        if (type == "port")
            return $"Port {label} has been associated with suspicious or elevated-risk behavior.";

        return "Further learning is required to fully explain this concept.";
    }

    // =========================================================
    // FILE EXPORTS (SAFE)
    // =========================================================
    private void SaveBrief(string label, string text, string category)
    {
        try
        {
            string safeCategory = SanitizeFileName(category);
            string folder = Path.Combine(TeachingRootPath, safeCategory);
            Directory.CreateDirectory(folder);

            string filename =
                $"{SanitizeFileName(label)}_Brief_{DateTime.UtcNow:yyyyMMdd_HHmmss}.txt";

            File.WriteAllText(Path.Combine(folder, filename), text);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[KnowledgeTeacher] TXT export failed: {ex.Message}");
        }
    }

    private void SaveBriefAsJson(
        string label,
        string text,
        string category,
        float confidence,
        string trailID)
    {
        var jsonEntry = new TeachingBrief
        {
            timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
            topic = label,
            category = category,
            confidence = confidence,
            trailID = trailID,
            summary = text
        };

        try
        {
            string json = JsonUtility.ToJson(jsonEntry, true);
            string jsonFile = $"{SanitizeFileName(label)}_Brief.json";

            File.WriteAllText(
                Path.Combine(JsonExportPath, jsonFile),
                json
            );
        }
        catch (Exception ex)
        {
            Debug.LogError($"[KnowledgeTeacher] JSON export failed: {ex.Message}");
        }
    }

    // =========================================================
    // WEEKLY EXPORT
    // =========================================================
    public void ExportWeeklyTeachingBriefs()
    {
        if (patterns == null)
            return;

        List<string> taught = new();
        DateTime cutoff = DateTime.UtcNow.AddDays(-7);

        foreach (var entry in patterns.GetAllPatterns())
        {
            if (!DateTime.TryParse(entry.lastSeen, out var seen)) continue;
            if (seen < cutoff) continue;
            if (entry.confidence < 0.4f || entry.count < 2) continue;

            var ont = ontology?.GetOntologyInfo(entry.label);
            string category = ont?.category ?? "Uncategorized";

            string brief =
                $"📚 Weekly Knowledge Brief: {entry.label}\n" +
                $"Type: {entry.type.ToUpper()}, Category: {category}\n" +
                $"Observed: {entry.count} times\n" +
                $"Confidence: {entry.confidence:F2}, Contradictions: {entry.contradictionCount}\n\n" +
                $"{GenerateExplanation(entry.label, entry.type, entry.confidence)}";

            SaveBrief(entry.label + "_Weekly", brief, category);
            SaveBriefAsJson(
                entry.label + "_Weekly",
                brief,
                category,
                entry.confidence,
                $"Trail_Teaching_{entry.label}_Weekly"
            );

            if (!betaMode)
            {
                core?.LogMemory(
                    $"📚 Weekly teaching brief exported for: {entry.label}",
                    "WeeklyTeaching",
                    1,
                    "organized"
                );
            }

            taught.Add(entry.label);
        }

        if (!betaMode && enableSpeech)
        {
            if (taught.Count > 0)
                speech?.TriggerVoice($"This week I prepared briefs for: {string.Join(", ", taught)}.");
            else
                speech?.TriggerVoice("No new teaching briefs were required this week.");
        }
    }

    // =========================================================
    // HELPERS
    // =========================================================
    private string SanitizeFileName(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return "Untitled";

        foreach (char c in Path.GetInvalidFileNameChars())
            input = input.Replace(c.ToString(), "_");

        return input.Replace(" ", "_");
    }

    // =========================================================
    // DATA STRUCT
    // =========================================================
    [Serializable]
    private class TeachingBrief
    {
        public string timestamp;
        public string topic;
        public string category;
        public float confidence;
        public string trailID;
        public string summary;
    }
}