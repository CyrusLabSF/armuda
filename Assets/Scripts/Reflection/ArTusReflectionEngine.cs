using UnityEngine;
using Debug = UnityEngine.Debug;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Diagnostics;
using ArTusTypes;

/// <summary>
/// ArTusReflectionEngine — Hi-Class Stable (Option B)
/// - Provides ReflectOnCycle() required by ArTusReflectionScheduler
/// - StableMode = analysis/logging only (NO simulation execution, NO python)
/// - Optional pipelines exist but are explicitly gated
/// </summary>
public class ArTusReflectionEngine : MonoBehaviour
{
    // =====================================================
    // COMPONENTS
    // =====================================================
    private ArTusCoreState core;
    private ArTusSpeechResponder speech;
    private ArTusBeliefEngine beliefEngine;

    private ArTusInhibitionEngine inhibition;
    private DirectoryAccessManager dirManager;
    private ArTusCognitiveLoopGuard loopGuard;

    // =====================================================
    // CONFIG
    // =====================================================
    [Header("Reflection Configuration")]
    public int maxTopicsToReflect = 3;
    public float reflectionTriggerThreshold = 0.7f;

    [Header("Stability Mode (Option B)")]
    [Tooltip("When true, reflection is analysis/logging only (no simulation, no external execution).")]
    public bool stableMode = true;

    [Header("External Pipelines (Explicit Only)")]
    [Tooltip("If true AND stableMode is false, allows python scripts to run.")]
    public bool enablePythonPipelines = false;

    // =====================================================
    // STATE
    // =====================================================
    private readonly List<string> scheduledReflections = new();
    private readonly List<SimulationResult> recentSimulations = new();

    // =====================================================
    // UNITY
    // =====================================================
    void Start()
    {
        core = GetComponent<ArTusCoreState>();
        speech = GetComponent<ArTusSpeechResponder>();
        beliefEngine = GetComponent<ArTusBeliefEngine>();
        inhibition = GetComponent<ArTusInhibitionEngine>();
        dirManager = GetComponent<DirectoryAccessManager>();
        loopGuard = GetComponent<ArTusCognitiveLoopGuard>();

        Debug.Log($"[ReflectionEngine] Initialized | StableMode={stableMode}");
    }

    // =====================================================
    // REQUIRED BY SCHEDULER
    // =====================================================

    /// <summary>
    /// Called by ArTusReflectionScheduler.
    /// In StableMode: log + ReflectOnLogs only.
    /// </summary>
    public void ReflectOnCycle()
    {
        if (loopGuard != null)
            loopGuard.DecayMemory();

        if (stableMode)
        {
            core?.LogMemory(
                "🧘 Reflection cycle executed (stable mode).",
                "ReflectionCycle",
                1,
                "neutral"
            );

            ReflectOnLogs(); // safe
            return;
        }

        // If stableMode is ever disabled intentionally:
        if (core.activityScore > reflectionTriggerThreshold)
        {
            ReflectOnLogs();
        }

        scheduledReflections.Clear();

        // Optional additional steps (still safe unless you explicitly call simulation/pipelines)
        // ReflectOnRecentSimulations();
        // RunExternalPipelines();
    }

    // =====================================================
    // QUEUE
    // =====================================================
    public void QueueReflection(string topic, string origin = "ingestion")
    {
        if (string.IsNullOrWhiteSpace(topic)) return;

        // use cached reference

        // 🔒 Block inhibited topics
        if (inhibition != null && inhibition.IsTopicInhibited(topic))
            return;

        string key = topic.ToLowerInvariant();

        // 🔁 Prevent duplicate queueing
        if (scheduledReflections.Contains(key)) return;

        if (loopGuard != null && !loopGuard.CanProcess(topic, true))
            return;

        scheduledReflections.Add(key);

        loopGuard?.MarkProcessed(topic, true);

        core?.LogMemory(
            $"🧬 Reflection queued: '{topic}' (origin: {origin})",
            "ReflectionQueue",
            2,
            "thinking"
        );

        ExportQueuedReflection(topic, origin);
    }

    private void ExportQueuedReflection(string topic, string origin)
    {
        try
        {
            if (dirManager == null) return;

            string path = dirManager.GetPathForDomain("Reflection");
            if (string.IsNullOrEmpty(path)) return;

            Directory.CreateDirectory(path);

            var entry = new
            {
                topic,
                origin,
                status = "queued",
                timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };

            string fileName =
                $"reflection_{topic.Replace(" ", "_").ToLower()}_{DateTime.Now:yyyyMMdd_HHmmss}.json";

            string json = JsonUtility.ToJson(entry, true); // ✅ FIXED

            FileIOManager.QueueWrite(
                Path.Combine(path, fileName),
                json,
                "ReflectionExport"
            );

            string jsonlPath = Path.Combine(core.UNIVERcityPath, "ReflectionTrail.jsonl");

            FileIOManager.QueueWrite(
                jsonlPath,
                json + Environment.NewLine,
                "ReflectionTrail",
                append: true
            );
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[ReflectionEngine] Export failed: {ex.Message}");
        }
    }

    // =====================================================
    // LOG-BASED REFLECTION (SAFE)
    // =====================================================
    public void ReflectOnLogs(string logIngestFile = "trail_reflections.jsonl")
    {
        if (!File.Exists(logIngestFile)) return;

        var lines = File.ReadLines(logIngestFile).TakeLast(50);

        foreach (var line in lines.Where(l => !string.IsNullOrWhiteSpace(l)))
        {
            try
            {
                var entry = JsonUtility.FromJson<LogReflectionEntry>(line);
                if (entry?.log_entry == null) continue;

                string topic = entry.log_entry.module ?? "system";
                string message = entry.log_entry.message ?? "";

                if (entry.log_entry.level == "ERROR")
                {
                    beliefEngine?.UpdateContradictionHeatmap(topic, 0.5f);
                    core?.LogMemory($"⚠️ Log issue: {message}", "LogReflection", 2, "alert");
                }
                else
                {
                    beliefEngine?.ReinforceBelief(topic, 0.1f);
                }
            }
            catch
            {
                // skip malformed lines
            }
        }

        AppendReflectionTrail("LogReflectionComplete");
    }

    private void AppendReflectionTrail(string label)
    {
        try
        {
            if (core == null) return;

            string path = Path.Combine(core.UNIVERcityPath, "ReflectionTrail.csv");

            string row = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss},{label}\n";

            // ✅ Async-safe (matches your system)
            FileIOManager.QueueWrite(
                path,
                row,
                "ReflectionTrailCSV",
                append: true
            );
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[ReflectionEngine] AppendReflectionTrail failed: {ex.Message}");
        }
    }

    // =====================================================
    // SIMULATION HOOKS (HARD DISABLED IN STABLE MODE)
    // =====================================================
    public void InjectSimulations(List<SimulationResult> simulations)
    {
        if (stableMode) return; // HARD STOP

        if (simulations != null && simulations.Count > 0)
            recentSimulations.AddRange(simulations);
    }

    public void ReflectOnRecentSimulations()
    {
        if (stableMode) return; // HARD STOP
        if (recentSimulations.Count == 0) return;

        var recent = recentSimulations
            .OrderByDescending(r =>
                DateTime.TryParse(r.timestamp, out var t) ? t : DateTime.MinValue)
            .Take(maxTopicsToReflect)
            .ToList();

        foreach (var sim in recent)
            ProcessSimulationReflection(sim);

        beliefEngine?.CompareBeliefSnapshotToCurrent();
        beliefEngine?.CaptureBeliefSnapshot();
    }

    private void ProcessSimulationReflection(SimulationResult sim)
    {
        if (sim == null || string.IsNullOrWhiteSpace(sim.topic)) return;

        string emotion = string.IsNullOrWhiteSpace(sim.emotion) ? "neutral" : sim.emotion;

        core?.LogMemory(
            $"🔍 Simulation insight (archived): {sim.topic}",
            "SimulationReflection",
            2,
            emotion
        );
    }

    // =====================================================
    // EXTERNAL PIPELINES (EXPLICIT ONLY)
    // =====================================================
    public void RunExternalPipelines()
    {
        if (stableMode || !enablePythonPipelines) return;

        RunPython("Domains/domain_pipeline.py", "DomainPipeline");
        RunPython("Defense/defense_pipeline.py", "DefensePipeline");
        RunPython("reflection_exporter.py", "ReflectionExporter");
    }

    private void RunPython(string relativePath, string tag)
    {
        try
        {
            if (core == null)
            {
                Debug.LogWarning($"[ReflectionEngine] CoreState missing; cannot run {tag}.");
                return;
            }

            string script = Path.Combine(core.UNIVERcityPath, relativePath);
            if (!File.Exists(script))
            {
                core?.LogMemory($"⚠️ {tag} missing: {relativePath}", tag, 2, "alert");
                return;
            }

            var psi = new ProcessStartInfo
            {
                FileName = "python",
                Arguments = $"\"{script}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };

            Process.Start(psi);
        }
        catch (Exception ex)
        {
            core?.LogMemory($"🔥 {tag} failed: {ex.Message}", tag, 2, "error");
        }
    }

    // =====================================================
    // DATA
    // =====================================================
    [Serializable]
    private class LogReflectionEntry
    {
        public LogEntry log_entry;
    }

    [Serializable]
    private class LogEntry
    {
        public string level;
        public string module;
        public string message;
    }
}
