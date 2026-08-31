using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System;
using System.IO;

public class ArTusPurposeLoop : MonoBehaviour
{
    private ArTusBeliefEngine beliefEngine;
    private ArTusCoreState core;

    [Header("Purpose Settings")]
    public float purposeCheckInterval = 60f;
    public string currentPurpose = "";

    private bool isChoosing = false;

    // 🧠 Soft memory of recent purposes
    private readonly Queue<string> recentPurposes = new();
    private const int MAX_RECENT_PURPOSES = 5;

    private float nextLogTime = 0f;
    private readonly List<string> buffer = new();
    private string csvPath;

    void Awake()
    {
        core = GetComponent<ArTusCoreState>();
        beliefEngine = GetComponent<ArTusBeliefEngine>();
        csvPath = ArTusPathUtility.GetSafePath("UNIVERcity/Exports/PurposeLoop.csv");
    }

    public void BeginLoop()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        Debug.Log("[PurposeLoop] Passive loop disabled on Magic Leap.");
        return;
#endif
        Debug.Log("[PurposeLoop] 🔁 Purpose loop initialized.");
        StartCoroutine(PurposeLoopCoroutine());
    }

    private IEnumerator PurposeLoopCoroutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(purposeCheckInterval);
            if (!isChoosing)
                StartCoroutine(SelectNewPurposeCoroutine());
        }
    }

    void Update()
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.P))
            StartCoroutine(SelectNewPurposeCoroutine());
#endif

        if (buffer.Count > 0 && Time.time > nextLogTime)
        {
            nextLogTime = Time.time + 120f;
            FlushBuffer();
        }
    }

    public IEnumerator SelectNewPurposeCoroutine()
    {
        isChoosing = true;

        if (beliefEngine == null || core == null || beliefEngine.beliefs.Count == 0)
        {
            isChoosing = false;
            yield break;
        }

        yield return null;

        // 🧠 Rank beliefs with SAFE emotional weighting
        var ranked = beliefEngine.beliefs
            .Where(b =>
                b.Value.confidenceScore >= 0.5f &&
                !recentPurposes.Contains(b.Key)
            )
            .Select(b =>
            {
                string emotion = b.Value.dominantEmotion ?? "neutral";

                float emotionWeight = 1f;

                // SAFE weighting (no dependency on other systems)
                switch (emotion)
                {
                    case "curious": emotionWeight = 1.5f; break;
                    case "focused": emotionWeight = 2f; break;
                    case "conflicted": emotionWeight = 0.8f; break;
                    case "idle": emotionWeight = 0.6f; break;
                }

                return new
                {
                    topic = b.Key,
                    confidence = b.Value.confidenceScore,
                    emotion,
                    emotionWeight,
                    score = b.Value.confidenceScore * Mathf.Clamp(emotionWeight, 1f, 5f)
                };
            })
            .OrderByDescending(b => b.score)
            .Take(5)
            .ToList();

        if (ranked.Count == 0)
        {
            isChoosing = false;
            yield break;
        }

        var chosen = ranked[UnityEngine.Random.Range(0, ranked.Count)];
        currentPurpose = chosen.topic;

        // 🧠 Track recent purposes
        recentPurposes.Enqueue(currentPurpose);
        while (recentPurposes.Count > MAX_RECENT_PURPOSES)
            recentPurposes.Dequeue();

        string logLine =
            $"Purpose selected: {currentPurpose} " +
            $"(Confidence: {chosen.confidence:F2}, EmotionWeight: {chosen.emotionWeight:F2})";

        core.LogMemory($"🎯 {logLine}", "PurposeSelection", 3, "growing");

        core.QueueDeferredReflection(
            $"Current purpose shifted to '{currentPurpose}'",
            "PurposeShift",
            Mathf.Clamp01(chosen.score / 10f)
        );

        Debug.Log($"[PurposeLoop] {logLine}");

        // CSV buffer
        buffer.Add(
            $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}," +
            $"{currentPurpose}," +
            $"{chosen.confidence:F2}," +
            $"{chosen.emotionWeight:F2}"
        );

        yield return null;
        isChoosing = false;
    }

    // ------------------------------------------------
    // FILE UTILITIES
    // ------------------------------------------------

    private void FlushBuffer()
    {
        try
        {
            EnsureCsv(csvPath, "Timestamp,Purpose,Confidence,EmotionWeight\n");
            File.AppendAllLines(csvPath, buffer);
            buffer.Clear();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[PurposeLoop] CSV flush skipped: {ex.Message}");
        }
    }

    private static void EnsureCsv(string filePath, string header)
    {
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        if (!File.Exists(filePath))
            File.WriteAllText(filePath, header);
    }
}