using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class AwarenessLayer : MonoBehaviour
{
    [Header("Emotion Awareness Settings")]
    public int memoryWindowSize = 30;
    public float dominanceThreshold = 0.45f;
    public float intensityTriggerThreshold = 0.6f;

    [Header("Reflection Control")]
    public float evaluationCooldown = 20f;
    public float reflectionCooldown = 120f;

    [Header("Momentum")]
    [Range(0.05f, 1f)]
    public float dominanceSmoothing = 0.3f;

    [Header("Awareness Outputs")]
    public ArTusEmotionController.EmotionState dominantEmotion = ArTusEmotionController.EmotionState.idle;
    public float dominancePercent = 0f;
    public float dominanceMomentum = 0f;
    public float emotionalIntensity = 0f;

    private readonly Queue<ArTusEmotionController.EmotionState> recentEmotions = new();
    private readonly Dictionary<ArTusEmotionController.EmotionState, int> emotionCounts = new();
    private readonly Dictionary<ArTusEmotionController.EmotionState, float> trendVelocity = new();
    private readonly Dictionary<string, float> lastReflectionTime = new();

    private ArTusEmotionController.EmotionState previousDominant = ArTusEmotionController.EmotionState.idle;
    private ArTusCoreState core;

    private float lastEvaluationTime = 0f;

    void Start()
    {
        core = GetComponent<ArTusCoreState>();
    }

    public void RegisterEmotion(ArTusEmotionController.EmotionState emotion)
    {
        recentEmotions.Enqueue(emotion);

        while (recentEmotions.Count > memoryWindowSize)
            recentEmotions.Dequeue();

        if (Time.time - lastEvaluationTime >= evaluationCooldown)
        {
            lastEvaluationTime = Time.time;
            EvaluateEmotionalState();
        }
    }

    private void EvaluateEmotionalState()
    {
        emotionCounts.Clear();

        foreach (var e in recentEmotions)
        {
            if (!emotionCounts.ContainsKey(e))
                emotionCounts[e] = 0;

            emotionCounts[e]++;
        }

        if (emotionCounts.Count == 0 || recentEmotions.Count == 0)
            return;

        var top = emotionCounts.OrderByDescending(kv => kv.Value).First();

        dominantEmotion = top.Key;
        dominancePercent = (float)top.Value / recentEmotions.Count;
        dominanceMomentum = Mathf.Lerp(dominanceMomentum, dominancePercent, dominanceSmoothing);

        UpdateTrendVelocity();
        emotionalIntensity = CalculateIntensity();

        if (dominantEmotion != previousDominant &&
            dominanceMomentum >= dominanceThreshold)
        {
            core?.LogMemory(
                $"Awareness shift: dominant emotion became {dominantEmotion} ({dominanceMomentum:P0})",
                "AwarenessShift",
                2,
                dominantEmotion.ToString()
            );

            if (CanReflect("shift"))
            {
                string context =
                    dominanceMomentum > 0.7f ? "deeply sustained" :
                    dominanceMomentum > 0.5f ? "stabilizing" :
                    "emerging";

                core?.QueueDeferredReflection(
                    $"Emotional pattern {context}: {dominantEmotion}",
                    "Awareness",
                    dominanceMomentum
                );
            }

            if (CanCreateGoal("regulation"))
            {
                core?.GoalController?.AddGoal(
                    $"Stabilize emotional state: {dominantEmotion}",
                    "self-regulation",
                    "awareness",
                    dominantEmotion.ToString(),
                    dominanceMomentum
                );
            }
        }

        if (emotionalIntensity >= intensityTriggerThreshold && CanReflect("intensity"))
        {
            string intensityType =
                emotionalIntensity > 0.8f ? "surge" :
                emotionalIntensity > 0.65f ? "build-up" :
                "fluctuation";

            core?.QueueDeferredReflection(
                $"Emotional {intensityType} detected ({emotionalIntensity:F2})",
                "Awareness-Intensity",
                emotionalIntensity
            );
        }

        if (CanReflect("trend"))
        {
            var rising = trendVelocity
                .OrderByDescending(kv => kv.Value)
                .FirstOrDefault();

            if (!EqualityComparer<ArTusEmotionController.EmotionState>.Default.Equals(rising.Key, default) &&
                rising.Value > 0.35f)
            {
                core?.QueueDeferredReflection(
                    $"Emotion trend rising: {rising.Key} ({rising.Value:F2})",
                    "Awareness-Trend",
                    rising.Value
                );
            }
        }

        previousDominant = dominantEmotion;
    }

    private void UpdateTrendVelocity()
    {
        foreach (var kv in emotionCounts)
        {
            float currentShare = kv.Value / (float)recentEmotions.Count;

            if (!trendVelocity.ContainsKey(kv.Key))
                trendVelocity[kv.Key] = 0f;

            trendVelocity[kv.Key] = Mathf.Lerp(trendVelocity[kv.Key], currentShare, 0.25f);
        }

        var enumValues = System.Enum.GetValues(typeof(ArTusEmotionController.EmotionState));
        foreach (ArTusEmotionController.EmotionState state in enumValues)
        {
            if (!emotionCounts.ContainsKey(state))
            {
                if (!trendVelocity.ContainsKey(state))
                    trendVelocity[state] = 0f;

                trendVelocity[state] = Mathf.Lerp(trendVelocity[state], 0f, 0.25f);
            }
        }
    }

    private float CalculateIntensity()
    {
        if (recentEmotions.Count == 0)
            return 0f;

        float totalStates =
            System.Enum.GetValues(typeof(ArTusEmotionController.EmotionState)).Length;

        float diversity = emotionCounts.Count / totalStates;

        float averageShare = 1f / Mathf.Max(1, emotionCounts.Count);
        float variance = 0f;

        foreach (var kv in emotionCounts)
        {
            float proportion = kv.Value / (float)recentEmotions.Count;
            variance += Mathf.Abs(proportion - averageShare);
        }

        return Mathf.Clamp01((variance + dominanceMomentum) * 0.5f);
    }

    private bool CanReflect(string key)
    {
        if (!lastReflectionTime.ContainsKey(key))
        {
            lastReflectionTime[key] = Time.time;
            return true;
        }

        if (Time.time - lastReflectionTime[key] >= reflectionCooldown)
        {
            lastReflectionTime[key] = Time.time;
            return true;
        }

        return false;
    }

    private bool CanCreateGoal(string key)
    {
        string goalKey = "goal_" + key;
        return CanReflect(goalKey);
    }

    public void ResetTrend()
    {
        recentEmotions.Clear();
        emotionCounts.Clear();
        trendVelocity.Clear();
        lastReflectionTime.Clear();

        dominantEmotion = ArTusEmotionController.EmotionState.idle;
        dominancePercent = 0f;
        dominanceMomentum = 0f;
        emotionalIntensity = 0f;
        previousDominant = ArTusEmotionController.EmotionState.idle;
        lastEvaluationTime = 0f;
    }
}