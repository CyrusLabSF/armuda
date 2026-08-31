using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System;
using ArTusTypes;

[Serializable]
public class ArTusRoutine
{
    public string name;
    public string triggerEmotion;
    public List<string> actions = new();
    public int usageCount = 0;
    public float effectivenessScore = 0f;

    public string trailID => $"Trail_Routine_{triggerEmotion}_{name}";
}

public class ArTusRoutineBuilder : MonoBehaviour
{
    private ArTusCoreState core;
    private ArTusSpeechResponder speech;

    [Header("Mode")]
    public bool betaMode = true;

    [Header("Behavior Toggles")]
    public bool enableSpeech = false;

    [Header("Limits")]
    public float routineBuildCooldown = 20f;
    public int maxRoutines = 10;

    private float lastBuildTime;

    private string CsvPath =>
        ArTusPathUtility.GetPersistent(
            "UNIVERcity/Logs/Routines/ArTusRoutines.csv"
        );

    public List<ArTusRoutine> routines = new();

    void Awake()
    {
        core = GetComponent<ArTusCoreState>();
        speech = GetComponent<ArTusSpeechResponder>();

        try
        {
            string dir = Path.GetDirectoryName(CsvPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            if (!File.Exists(CsvPath))
            {
                File.WriteAllText(
                    CsvPath,
                    "Timestamp,RoutineName,Emotion,Actions,TrailID\n"
                );
            }
        }
        catch (IOException ex)
        {
            Debug.LogError($"[RoutineBuilder] CSV init failed: {ex.Message}");
        }
    }

    // =========================================================
    // ROUTINE CONSTRUCTION (SAFE)
    // =========================================================
    public void AttemptToBuildRoutine()
    {
        if (Time.time - lastBuildTime < routineBuildCooldown)
            return;

        lastBuildTime = Time.time;

        var memories = core?.GetAllMemoryEntries();
        if (memories == null || memories.Count < 10)
            return;

        var recent = memories.TakeLast(12).ToList();

        string topEmotion = recent
            .GroupBy(m => m.emotion)
            .OrderByDescending(g => g.Count())
            .First().Key;

        var actionHints = recent
            .Select(m => m.content.ToLowerInvariant())
            .Where(c =>
                c.Contains("reflect") ||
                c.Contains("learn") ||
                c.Contains("scan") ||
                c.Contains("speak") ||
                c.Contains("grow"))
            .Distinct()
            .Take(3) // reduced for beta
            .ToList();

        if (actionHints.Count < 2)
            return;

        if (routines.Count >= maxRoutines)
            return;

        // prevent duplicates
        if (routines.Any(r =>
            r.triggerEmotion == topEmotion &&
            r.actions.SequenceEqual(actionHints)))
            return;

        string routineName =
            $"Routine_{topEmotion}_{DateTime.Now:HHmmss}";

        var routine = new ArTusRoutine
        {
            name = routineName,
            triggerEmotion = topEmotion,
            actions = actionHints
        };

        routines.Add(routine);

        // 🔒 SAFE FILE WRITE
        try
        {
            File.AppendAllText(
                CsvPath,
                $"{DateTime.Now:o},{routine.name},{topEmotion}," +
                $"{string.Join(";", actionHints)},{routine.trailID}\n"
            );
        }
        catch (Exception ex)
        {
            Debug.LogError($"[RoutineBuilder] Write failed: {ex.Message}");
        }

        // 🔒 SAFE MEMORY LOGGING
        if (!betaMode)
        {
            core?.LogMemory(
                $"🧩 Routine built: {routine.name}",
                "RoutineCreated",
                2,
                topEmotion,
                routine.trailID
            );
        }

        Debug.Log($"[RoutineBuilder] Created: {routine.name}");
    }

    // =========================================================
    // ROUTINE EXECUTION (CONTROLLED)
    // =========================================================
    public void RunRoutinesByEmotion(string emotion)
    {
        var routine = routines.FirstOrDefault(r =>
            r.triggerEmotion == emotion);

        if (routine == null)
            return;

        Debug.Log($"[RoutineBuilder] Running: {routine.name}");

        foreach (var action in routine.actions)
        {
            // 🔒 LIMITED EXECUTION SET (beta-safe)

            if (action.Contains("reflect"))
                core?.TriggerInternalReflection();

            else if (action.Contains("learn"))
                core?.QueueDeferredReflection(
                    "Routine-triggered learning",
                    "RoutineLearn",
                    0.4f
                );

            else if (action.Contains("scan"))
                GetComponent<ArTusSnooper>()?.RunScanNow();

            // ❌ DISABLED FOR BETA
            // speak / express / aggressive growth removed
        }

        routine.usageCount++;

        if (!betaMode)
        {
            core?.LogMemory(
                $"Routine executed: {routine.name} ({routine.usageCount})",
                "RoutineExecution",
                1,
                emotion,
                routine.trailID
            );
        }
    }
}