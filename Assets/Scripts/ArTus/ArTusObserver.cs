using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ArTusObserver : MonoBehaviour
{
    public bool passiveLearningEnabled = true;
    public float logInterval = 10f;

    public bool logSceneChanges = true;
    public bool logTyping = true;
    public bool logActivity = true;

    private ArTusCoreState core;
    private string lastScene;
    private string typedBuffer = "";
    private float activityScore = 0f;

    // ✅ RESTORED INTELLIGENCE
    private float inactivityAccumulated = 0f;
    private string previousEmotion = "";

    private float contradictionTimer = 0f;
    private float trendTimer = 0f;

    private int sceneSwitches = 0;
    private List<float> activitySamples = new();

    public int maxActivitySamples = 200;

    private string csvPath;
    private float nextCsvFlush = 0f;
    private readonly List<string> csvBuffer = new();

    void Start()
    {
        if (core == null)
            core = GetComponent<ArTusCoreState>();

        lastScene = SceneManager.GetActiveScene().name;

        csvPath = ArTusPathUtility.GetSafePath(
            "UNIVERcity/Exports/ObserverLog.csv"
        );

        StartCoroutine(ObservationLoop());
    }

    void Update()
    {
        if (!passiveLearningEnabled) return;

        string input = Input.inputString;

        if (!string.IsNullOrEmpty(input))
        {
            typedBuffer += input;

            if (typedBuffer.Length > 200)
                typedBuffer = typedBuffer.Substring(typedBuffer.Length - 200);

            activityScore += input.Length * 0.5f;
        }

        // ✅ Activity decay (stability)
        activityScore = Mathf.Max(0f, activityScore - Time.deltaTime * 0.25f);

        // ✅ Timers
        contradictionTimer += Time.deltaTime;
        trendTimer += Time.deltaTime;

        if (contradictionTimer >= 120f)
        {
            contradictionTimer = 0f;
            core?.EvaluateContradictions();
        }

        if (trendTimer >= 3600f)
        {
            trendTimer = 0f;
            EmitObserverTrend();
        }

        // ✅ CSV flush
        if (Time.time > nextCsvFlush && csvBuffer.Count > 0)
        {
            nextCsvFlush = Time.time + 60f;
            FlushCsv();
        }
    }

    private IEnumerator ObservationLoop()
    {
        while (this != null && gameObject.activeInHierarchy)
        {
            if (!passiveLearningEnabled || core == null)
            {
                yield return new WaitForSeconds(logInterval);
                continue;
            }

            string scene = SceneManager.GetActiveScene().name;

            if (logSceneChanges && scene != lastScene)
            {
                Log("Scene changed to " + scene, "SceneSwitch", "alert", 2);
                lastScene = scene;
                sceneSwitches++;
            }

            if (logTyping && !string.IsNullOrEmpty(typedBuffer))
            {
                Log($"Typed keys: '{typedBuffer}'", "Typing", "thinking", 1);
                typedBuffer = "";
            }

            if (logActivity)
            {
                string dominant =
                    activityScore > 2f ? "curious" :
                    activityScore > 1f ? "thinking" :
                    activityScore > 0.2f ? "idle" : "rest";

                Log($"Activity score: {activityScore:F2}", "Activity", dominant, 1);

                // =====================================================
                // 🔥 EMOTION SHIFT DETECTION (RESTORED)
                // =====================================================
                if (!string.IsNullOrEmpty(previousEmotion) && dominant != previousEmotion)
                {
                    core.LogMemory(
                        $"Observer emotion shift {previousEmotion} → {dominant}",
                        "EmotionShift",
                        dominant == "alert" ? 3 : 1,
                        dominant
                    );
                }

                previousEmotion = dominant;

                // =====================================================
                // 💤 INACTIVITY INTELLIGENCE (RESTORED)
                // =====================================================
                if (activityScore < 0.15f)
                {
                    inactivityAccumulated += logInterval;

                    if (inactivityAccumulated >= 180f)
                    {
                        core?.QueueDeferredReflection(
                            "Persistent inactivity detected",
                            "Observer",
                            Mathf.Clamp01(inactivityAccumulated / 600f)
                        );

                        inactivityAccumulated = 0f;
                    }
                }
                else
                {
                    inactivityAccumulated = 0f;
                }

                // =====================================================
                // 📊 ACTIVITY TRACKING
                // =====================================================
                activitySamples.Add(activityScore);

                if (activitySamples.Count > maxActivitySamples)
                    activitySamples.RemoveAt(0);
            }

            yield return new WaitForSeconds(logInterval);
        }
    }

    private void EmitObserverTrend()
    {
        float avgActivity = activitySamples.Count > 0 ? activitySamples.Average() : 0f;
        float clarity = core.GetAverageMemoryClarity();
        string emotion = core.GetDominantEmotion();

        core.LogMemory(
            $"Observer trend | SceneSwitches={sceneSwitches}, AvgActivity={avgActivity:F2}, Clarity={clarity:F2}",
            "ObserverTrend",
            1,
            emotion
        );

        activitySamples.Clear();
        sceneSwitches = 0;
    }

    private void Log(string content, string category, string emotion, int weight)
    {
        string formatted = $"(observer) {content}";
        core.LogMemory(formatted, category, weight, emotion);
        QueueCsv(category, formatted, emotion);
    }

    private string Csv(string value)
    {
        if (string.IsNullOrEmpty(value)) return "\"\"";

        value = value.Replace("\"", "\"\"");

        if (value.Contains(",") || value.Contains("\n") || value.Contains("\r"))
            return $"\"{value}\"";

        return value;
    }

    private void QueueCsv(string category, string content, string emotion)
    {
        float clarity = core.GetAverageMemoryClarity();

        float confidence = 0f;
        if (core.beliefs != null && core.beliefs.Count > 0)
        {
            var last = core.beliefs.Last();
            confidence = last.Value != null ? last.Value.confidenceScore : 0f;
        }

        csvBuffer.Add(
            $"{Csv(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"))}," +
            $"{Csv(category)}," +
            $"{Csv(content)}," +
            $"{Csv(emotion)}," +
            $"{clarity:F2}," +
            $"{confidence:F2}"
        );
    }

    private void FlushCsv()
    {
        try
        {
            EnsureCsv(csvPath, "Timestamp,Category,Content,Emotion,Clarity,Confidence\n");
            File.AppendAllLines(csvPath, csvBuffer);
            csvBuffer.Clear();
        }
        catch { }
    }

    private static void EnsureCsv(string path, string header)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        if (!File.Exists(path))
            File.WriteAllText(path, header);
    }
}