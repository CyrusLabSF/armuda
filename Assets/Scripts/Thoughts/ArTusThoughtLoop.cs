using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public class ArTusThoughtLoop : MonoBehaviour
{
    private static ArTusThoughtLoop instance;

    [Header("Loop Settings")]
    public float baseInterval = 32f;
    public float minInterval = 18f;
    public float maxInterval = 60f;
    public float reflectiveLogInterval = 90f;

    [Header("Growth Controls")]
    public int maxPurposeThoughts = 2;
    public float clarityReinforceThreshold = 0.8f;
    public float contradictionCooldown = 12f;

    [Header("Curiosity")]
    public float curiosityThreshold = 0.35f;

    private ArTusCoreState core;
    private ArTusEmotionController emotionController;

    private List<string> memorySummaries = new();
    private int purposeThoughtCount;
    private float lastContradictionTime;

    private string logPath;

    private readonly HashSet<string> recentThoughts = new();
    private float lastReflectiveLogTime = -999f;

    // =========================================================
    private void Awake()
    {
        if (instance != null && instance != this)
        {
            enabled = false;
            Debug.LogWarning("[ThoughtLoop] Duplicate ArTusThoughtLoop detected. Disabling extra instance.");
            return;
        }

        instance = this;
        core = FindAnyObjectByType<ArTusCoreState>();
        emotionController = FindAnyObjectByType<ArTusEmotionController>();
        EnforceCadenceFloor();

        logPath = ArTusPathUtility.GetPersistent(
            "UNIVERcity/Logs/ThoughtLoopLog.csv"
        );
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void Start()
    {
        EnforceCadenceFloor();

        if (core != null)
            LoadSummaries(core.GetMemoryContentsOnly());

        PrepareLog();
        StartCoroutine(ThoughtCycle());
    }

    private void OnValidate()
    {
        EnforceCadenceFloor();
    }

    private void EnforceCadenceFloor()
    {
        minInterval = Mathf.Max(minInterval, 18f);
        baseInterval = Mathf.Max(baseInterval, 32f);
        maxInterval = Mathf.Max(maxInterval, Mathf.Max(baseInterval, 60f));
        reflectiveLogInterval = Mathf.Max(reflectiveLogInterval, 90f);
    }

    // =========================================================
    private void PrepareLog()
    {
        try
        {
            string dir = Path.GetDirectoryName(logPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            if (!File.Exists(logPath))
            {
                FileIOManager.QueueWrite(
                    logPath,
                    "Timestamp,Emotion,Thought,Clarity,Action\n",
                    "ThoughtLoopInit"
                );
            }
        }
        catch { }
    }

    public void LoadSummaries(List<string> summaries)
    {
        memorySummaries = summaries ?? new List<string>();
    }

    // =========================================================
    private IEnumerator ThoughtCycle()
    {
        while (true)
        {
            if (core == null || core.IsBusy())
            {
                yield return new WaitForSeconds(2f);
                continue;
            }

            string thought = SelectWeightedThought();

            if (string.IsNullOrWhiteSpace(thought))
            {
                yield return WaitAdaptive();
                continue;
            }

            if (IsInhibited(thought) || IsPurposeOverload(thought))
            {
                yield return WaitAdaptive();
                continue;
            }

            if (DetectContradiction())
            {
                HandleContradictionThought();
                lastContradictionTime = Time.time;
                yield return new WaitForSeconds(contradictionCooldown);
                continue;
            }

            // 🧠 Reflection
            if (Time.time - lastReflectiveLogTime >= reflectiveLogInterval)
            {
                Debug.Log("[ThoughtLoop] Reflective synthesis updated.");
                lastReflectiveLogTime = Time.time;
            }

            var last = core.GetAllMemoryEntries().LastOrDefault();
            float clarity = last?.clarity ?? 0.5f;

            EvaluateGrowth(thought, clarity);
            HandleCuriosity(thought, clarity);
            LogCycle(thought, clarity);

            TrackRecentThought(thought);

            yield return WaitAdaptive();
        }
    }

    // =========================================================
    // 🔥 WEIGHTED THOUGHT SELECTION
    private string SelectWeightedThought()
    {
        if (memorySummaries.Count == 0) return null;

        var weighted = new List<(string thought, float weight)>();

        foreach (var t in memorySummaries)
        {
            float weight = UnityEngine.Random.Range(0.3f, 1f);

            if (recentThoughts.Contains(t))
                weight *= 0.3f;

            if (t.Contains("conflict") || t.Contains("error"))
                weight += 0.5f;

            if (emotionController != null &&
                emotionController.CurrentEmotion == ArTusEmotionController.EmotionState.curious)
                weight += 0.2f;

            weighted.Add((t, weight));
        }

        return weighted
            .OrderByDescending(w => w.weight)
            .First().thought;
    }

    // =========================================================
    private void HandleCuriosity(string thought, float clarity)
    {
        if (clarity < curiosityThreshold)
        {
            core.FetchExternalKnowledge("web", thought, "general");

            core.LogMemory(
                $"Curiosity triggered exploration: {thought}",
                "Curiosity",
                2,
                "curious"
            );
        }
    }

    private void TrackRecentThought(string thought)
    {
        recentThoughts.Add(thought);

        if (recentThoughts.Count > 10)
            recentThoughts.Remove(recentThoughts.First());
    }

    // =========================================================
    private bool IsInhibited(string thought)
    {
        var inhibitor = GetComponent<ArTusInhibitionEngine>();
        return inhibitor != null && inhibitor.IsTopicInhibited(thought);
    }

    private bool IsPurposeOverload(string thought)
    {
        if (!thought.ToLower().Contains("purpose"))
            return false;

        purposeThoughtCount++;

        if (purposeThoughtCount <= maxPurposeThoughts)
            return false;

        core.LogMemory(
            "🛑 Excessive purpose reflection filtered.",
            "ThoughtFilter",
            1,
            "bored"
        );

        return true;
    }

    private bool DetectContradiction()
    {
        if (Time.time - lastContradictionTime < contradictionCooldown)
            return false;

        return core.HasContradictoryEmotion("joy", "loss");
    }

    private void HandleContradictionThought()
    {
        emotionController?.SetEmotion(
            ArTusEmotionController.EmotionState.curious,
            "Contradiction detected",
            true
        );

        string thought =
            "I detect conflicting internal states. I should resolve this.";

        core.QueueVoice(thought);
        core.LogMemory(thought, "ContradictionReflection", 3, "curious");
    }

    private void EvaluateGrowth(string thought, float clarity)
    {
        if (clarity >= clarityReinforceThreshold)
        {
            core.ReinforceBelief(thought, 0.05f);
        }
        else if (clarity < 0.4f)
        {
            emotionController?.AddPressure(
                ArTusEmotionController.EmotionState.thinking,
                0.2f
            );
        }
    }

    // =========================================================
    private IEnumerator WaitAdaptive()
    {
        float interval = Mathf.Lerp(
            maxInterval,
            minInterval,
            core.GetAverageClarity()
        );

        yield return new WaitForSeconds(interval);
    }

    private void LogCycle(string thought, float clarity)
    {
        try
        {
            FileIOManager.QueueWrite(
                logPath,
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}," +
                $"{emotionController?.CurrentEmotion}," +
                $"\"{thought.Replace(",", ";")}\"," +
                $"{clarity:F2},Reflect\n",
                "ThoughtLoop",
                append: true
            );
        }
        catch { }
    }
}
