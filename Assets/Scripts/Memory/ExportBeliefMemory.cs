using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System;
using ArTusTypes;

public class ExportBeliefMemory : MonoBehaviour
{
    private ArTusCoreState core;

    [Header("Export Paths (Relative)")]
    [SerializeField]
    private string beliefExportRelative = "sandbox_memory.json";

    [SerializeField]
    private string trailExportRelative = "UNIVERcity/Exports/MemoryTrail.json";

    // Resolved at runtime
    private string beliefExportPath;
    private string trailExportPath;

    // --------------------------------------------------
    // UNITY LIFECYCLE
    // --------------------------------------------------
    void Awake()
    {
        // Resolve paths only — no CoreState dependency here
        beliefExportPath = ArTusPathUtility.GetPersistent(beliefExportRelative);
        trailExportPath = ArTusPathUtility.GetPersistent(trailExportRelative);
    }

    // --------------------------------------------------
    // BELIEF SNAPSHOT EXPORT
    // --------------------------------------------------
    [ContextMenu("Export Belief Memory Snapshot")]
    public void ExportSnapshot()
    {
        if (!TryResolveCore() || core.beliefs == null || core.beliefs.Count == 0)
            return;

        var export = new BeliefMemoryWrapper
        {
            entries = new List<BeliefMemoryEntry>(core.beliefs.Count)
        };

        foreach (var pair in core.beliefs)
        {
            var data = pair.Value;

            export.entries.Add(new BeliefMemoryEntry
            {
                topic = pair.Key,
                confidence = data.confidenceScore,
                origin = Normalize(data.origin, "Unknown"),
                dominantEmotion = Normalize(data.dominantEmotion, "neutral"),
                supportingTrail = data.relatedTrails.Count > 0 ? data.relatedTrails[0] : "",
                domain = Normalize(data.domain, "general"),
                description = Normalize(
                    data.description,
                    $"Belief from {Normalize(data.origin, "Unknown")}"
                )
            });
        }

        SafeWriteJson(beliefExportPath, export, "[Belief Export]");
    }

    // --------------------------------------------------
    // MEMORY TRAIL EXPORT
    // --------------------------------------------------
    [ContextMenu("Export Memory Trail Visual")]
    public void ExportMemoryTrail()
    {
        if (!TryResolveCore() || core.beliefs == null || core.beliefs.Count == 0)
            return;

        var export = new MemoryTrailWrapper
        {
            trails = new List<MemoryTrailEntry>(core.beliefs.Count)
        };

        foreach (var pair in core.beliefs)
        {
            var data = pair.Value;
            float confidence = data.confidenceScore;

            export.trails.Add(new MemoryTrailEntry
            {
                id = StableId(pair.Key),
                belief = pair.Key,
                confidence = confidence,
                emotion = Normalize(data.dominantEmotion, "neutral"),
                trail = data.relatedTrails.Count > 0
                    ? string.Join(", ", data.relatedTrails)
                    : "none",
                domain = Normalize(data.domain, "general"),
                type = confidence < 0.6f ? "contradiction" : "belief"
            });
        }

        SafeWriteJson(trailExportPath, export, "[MemoryTrail Export]");
    }

    // --------------------------------------------------
    // CORE RESOLUTION (LAZY & SAFE)
    // --------------------------------------------------
    private bool TryResolveCore()
    {
        if (core != null)
            return true;

        core = GetComponent<ArTusCoreState>();
        return core != null;
    }

    // --------------------------------------------------
    // HELPERS
    // --------------------------------------------------
    private static string Normalize(string value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private void SafeWriteJson(string path, object data, string logPrefix)
    {
        try
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            string tempPath = path + ".tmp";
            string json = JsonUtility.ToJson(data, true);

            File.WriteAllText(tempPath, json);
            File.Copy(tempPath, path, true);
            File.Delete(tempPath);

            Debug.Log($"{logPrefix} ✅ Saved to: {path}");
        }
        catch (IOException ex)
        {
            Debug.LogError($"{logPrefix} ❌ Failed to write file: {ex.Message}");
        }
    }

    // Stable, deterministic ID (cross-platform safe)
    private string StableId(string input)
    {
        unchecked
        {
            int hash = 23;
            for (int i = 0; i < input.Length; i++)
                hash = hash * 31 + input[i];

            return $"Belief_{Mathf.Abs(hash)}";
        }
    }

    // --------------------------------------------------
    // DATA MODELS
    // --------------------------------------------------
    [Serializable]
    public class BeliefMemoryWrapper
    {
        public List<BeliefMemoryEntry> entries = new();
    }

    [Serializable]
    public class MemoryTrailWrapper
    {
        public List<MemoryTrailEntry> trails = new();
    }

    [Serializable]
    public class MemoryTrailEntry
    {
        public string id;
        public string belief;
        public float confidence;
        public string emotion;
        public string trail;
        public string domain;
        public string type;
    }
}
