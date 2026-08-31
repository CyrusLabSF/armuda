using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

public class KeystrokeIntentAnalyzer : MonoBehaviour
{
    public enum IntentType
    {
        Unknown,
        Question,
        Command,
        Reflection,
        Emotional,
        Technical,
        Journal,
        Affirmation
    }

    public class IntentResult
    {
        public IntentType primary;
        public float confidence;
        public List<IntentType> alternatives = new();
        public string detectedPhrase;
    }

    public IntentResult AnalyzeRich(string input)
    {
        input = input.ToLower();
        var result = new IntentResult
        {
            primary = IntentType.Unknown,
            confidence = 0.0f,
            detectedPhrase = ""
        };

        if (string.IsNullOrWhiteSpace(input))
            return result;

        Dictionary<IntentType, int> hitMap = new();

        void Hit(IntentType type, string phrase)
        {
            if (!hitMap.ContainsKey(type)) hitMap[type] = 0;
            hitMap[type]++;
            if (string.IsNullOrEmpty(result.detectedPhrase))
                result.detectedPhrase = phrase;
        }

        // Phrase Triggers
        if (input.Contains("?") || input.StartsWith("what") || input.StartsWith("how") || input.StartsWith("why"))
            Hit(IntentType.Question, "?/what/how/why");

        if (input.StartsWith("open") || input.StartsWith("run") || input.StartsWith("clear") || input.StartsWith("load"))
            Hit(IntentType.Command, "command prefix");

        if (input.Contains("i feel") || input.Contains("i think") || input.Contains("i remember"))
            Hit(IntentType.Reflection, "self-reflection");

        if (input.Contains("sad") || input.Contains("angry") || input.Contains("happy") || input.Contains("lonely"))
            Hit(IntentType.Emotional, "emotion keyword");

        if (input.Contains("cpu") || input.Contains("network") || input.Contains("error") || input.Contains("server"))
            Hit(IntentType.Technical, "technical keyword");

        if (input.StartsWith("i will") || input.StartsWith("i can") || input.StartsWith("i am"))
            Hit(IntentType.Affirmation, "affirmation prefix");

        if (hitMap.Count == 0)
            return result;

        // Determine most likely intent
        result.primary = GetTopIntent(hitMap);
        result.confidence = Mathf.Clamp01(hitMap[result.primary] * 0.3f);
        result.alternatives = new List<IntentType>(hitMap.Keys);
        return result;
    }

    public IntentType Analyze(string input)
    {
        var result = AnalyzeRich(input);
        return result.primary;
    }

    private IntentType GetTopIntent(Dictionary<IntentType, int> hits)
    {
        int max = 0;
        IntentType best = IntentType.Unknown;

        foreach (var kvp in hits)
        {
            if (kvp.Value > max)
            {
                best = kvp.Key;
                max = kvp.Value;
            }
        }

        return best;
    }
}
