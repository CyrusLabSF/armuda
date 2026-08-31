using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

/// <summary>
/// ArTus Thought Path Logger — Open, Extensible, Hi-Class
/// -----------------------------------------------------
/// Responsibility:
/// • Record how ArTus arrived at beliefs
/// • Preserve reasoning steps without influencing them
/// • Remain open for future ArTus-driven extensions
///
/// ❌ No belief mutation
/// ❌ No emotion forcing
/// ❌ No decision logic
/// </summary>
public class ArTusThoughtPathLogger : MonoBehaviour
{
    // =====================================================
    // DATA MODELS
    // =====================================================

    [Serializable]
    public class ThoughtStep
    {
        public string stage;
        public string detail;
        public string timestamp;
        public Dictionary<string, string> meta;
    }

    [Serializable]
    public class ThoughtPath
    {
        public string pathId;
        public string topic;
        public string category;
        public string emotion;
        public Dictionary<string, string> meta;
        public List<ThoughtStep> steps = new();
    }

    // =====================================================
    // STATE
    // =====================================================

    private readonly Dictionary<string, ThoughtPath> activePaths = new();

    // Limits
    [Header("Limits")]
    public int maxActivePaths = 50;
    public int maxStepsPerPath = 50;
    public bool enableSpeechOnComplete = false;

    // ❗ Paths resolved at runtime (WebGL-safe)
    private string exportFolderPath;
    private string csvSummaryPath;

    private ArTusCoreState core;
    private ArTusSpeechResponder speech;

    // =====================================================
    // LIFECYCLE
    // =====================================================

    private void Awake()
    {
        core = GetComponent<ArTusCoreState>();
        speech = GetComponent<ArTusSpeechResponder>();

        exportFolderPath =
            ArTusPathUtility.GetPersistent("UNIVERcity/ThoughtPaths");

        csvSummaryPath =
            ArTusPathUtility.GetPersistent("UNIVERcity/Logs/ThoughtPathSummary.csv");
    }

    private void Start()
    {
        PrepareStorage();
    }

    private void PrepareStorage()
    {
        try
        {
            Directory.CreateDirectory(exportFolderPath);

            string csvDir = Path.GetDirectoryName(csvSummaryPath);
            if (!string.IsNullOrEmpty(csvDir))
                Directory.CreateDirectory(csvDir);

            if (!File.Exists(csvSummaryPath))
            {
                File.WriteAllText(
                    csvSummaryPath,
                    "Timestamp,PathId,Topic,Category,Emotion,StepCount,BeliefSummary\n"
                );
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ThoughtPathLogger] Init failed: {ex.Message}");
        }
    }

    // =====================================================
    // PUBLIC API — OPEN FOR ARTUS
    // =====================================================

    public string StartThoughtPath(string topic, string category, string trigger)
    {
        if (activePaths.Count >= maxActivePaths)
        {
            var oldest = activePaths.Keys.First();
            activePaths.Remove(oldest);
        }

        string pathId = Guid.NewGuid().ToString("N");

        var path = new ThoughtPath
        {
            pathId = pathId,
            topic = topic,
            category = category,
            emotion = "",
            meta = new Dictionary<string, string>(),
            steps = new List<ThoughtStep>
            {
                new ThoughtStep
                {
                    stage = "Trigger",
                    detail = trigger,
                    timestamp = Timestamp(),
                    meta = new Dictionary<string, string>()
                }
            }
        };

        activePaths[pathId] = path;

        Debug.Log($"[ThoughtPath] Started path {pathId} for topic '{topic}'");

        return pathId;
    }

    public void AddThoughtStep(
        string pathId,
        string stage,
        string detail,
        Dictionary<string, string> meta = null
    )
    {
        if (!activePaths.TryGetValue(pathId, out var path))
            return;

        if (path.steps.Count >= maxStepsPerPath)
            return;

        path.steps.Add(new ThoughtStep
        {
            stage = stage,
            detail = detail,
            timestamp = Timestamp(),
            meta = meta ?? new Dictionary<string, string>()
        });
    }

    public void AddPathMeta(string pathId, string key, string value)
    {
        if (!activePaths.TryGetValue(pathId, out var path))
            return;

        path.meta[key] = value;
    }

    public void FinalizeThoughtPath(
        string pathId,
        string emotion,
        string beliefSummary
    )
    {
        if (!activePaths.TryGetValue(pathId, out var path))
            return;

        path.emotion = emotion;

        path.steps.Add(new ThoughtStep
        {
            stage = "BeliefFormed",
            detail = beliefSummary,
            timestamp = Timestamp(),
            meta = new Dictionary<string, string>()
        });

        ExportPath(path, beliefSummary);

        activePaths.Remove(pathId);
    }

    // =====================================================
    // EXPORTS
    // =====================================================

    private void ExportPath(ThoughtPath path, string beliefSummary)
    {
        string safeTopic = MakeSafeFileName(path.topic);

        string filename =
            $"ThoughtPath_{safeTopic}_{DateTime.Now:yyyyMMdd_HHmmss}.json";

        string jsonPath = Path.Combine(exportFolderPath, filename);

        try
        {
            string json = JsonUtility.ToJson(path, true);
            File.WriteAllText(jsonPath, json);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ThoughtPath] JSON export failed: {ex.Message}");
        }

        try
        {
            File.AppendAllText(
                csvSummaryPath,
                $"{Timestamp()}," +
                $"{path.pathId}," +
                $"{Csv(path.topic)}," +
                $"{Csv(path.category)}," +
                $"{Csv(path.emotion)}," +
                $"{path.steps.Count}," +
                $"{Csv(beliefSummary)}\n"
            );
        }
        catch { /* non-fatal */ }

        core?.LogMemory(
            $"🧭 Thought path completed for '{path.topic}' ({path.steps.Count} steps).",
            "ThoughtPath",
            2,
            path.emotion,
            path.topic
        );

        if (enableSpeechOnComplete)
        {
            speech?.RequestSpeak(
                $"I’ve completed a thought path about {path.topic}.",
                ArTusSpeechResponder.SpeechCategory.Reflection
            );
        }
    }

    // =====================================================
    // UTIL
    // =====================================================

    private string Timestamp()
        => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

    private string MakeSafeFileName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "unknown";

        foreach (char c in Path.GetInvalidFileNameChars())
            value = value.Replace(c, '_');

        return value.Replace(" ", "_");
    }

    private string Csv(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "\"\"";

        value = value.Replace("\"", "\"\"");

        if (value.Contains(",") || value.Contains("\n") || value.Contains("\r"))
            return $"\"{value}\"";

        return value;
    }
}