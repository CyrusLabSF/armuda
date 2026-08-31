using System;
using System.IO;
using UnityEngine;

public class ReflectiveVoiceNarrator : MonoBehaviour
{
    private ArTusSpeechResponder speech;
    private ArTusCoreState core;

    [Header("Manual Narration Settings")]
    [Tooltip("Relative path inside persistent data (resolved in Awake)")]
    [SerializeField]
    private string summaryRelativePath = "UNIVERcity/Exports/ReflectionSummary.json";

    private string summaryPath; // resolved at runtime

    private void Awake()
    {
        speech = GetComponent<ArTusSpeechResponder>();
        core = GetComponent<ArTusCoreState>();

        // ✅ SAFE: resolve persistent path here
        summaryPath = ArTusPathUtility.GetPersistent(summaryRelativePath);
    }

    // ==========================================================
    // MANUAL ENTRY POINT ONLY
    // ==========================================================
    public void NarrateReflectionSummary()
    {
        // 🧠 Explicit manual invocation only
        if (string.IsNullOrEmpty(summaryPath) || !File.Exists(summaryPath))
        {
            Debug.Log("[ReflectiveNarrator] No reflection summary present (manual mode).");
            return;
        }

        try
        {
            string json = File.ReadAllText(summaryPath);
            ReflectionSummary summary = JsonUtility.FromJson<ReflectionSummary>(json);

            if (summary == null || string.IsNullOrWhiteSpace(summary.text))
                return;

            string spoken = summary.text.Trim();

            // 🗣 Speak only — NO belief mutation, NO chaining
            speech?.RequestSpeak(
                spoken,
                ArTusSpeechResponder.SpeechCategory.Reflection
            );

            Debug.Log("[ReflectiveNarrator] Reflection narrated on explicit request.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ReflectiveNarrator] Failed to narrate reflection: {ex.Message}");
        }
    }

    // ==========================================================
    // DATA MODEL
    // ==========================================================
    [Serializable]
    public class ReflectionSummary
    {
        public string text;
        public string timestamp;
    }
}
