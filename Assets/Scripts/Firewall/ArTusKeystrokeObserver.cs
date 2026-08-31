using UnityEngine;
using System.Text.RegularExpressions;
using System.IO;
using System;
using System.Collections.Generic;

public class ArTusKeystrokeObserver : MonoBehaviour
{
    private ArTusCoreState coreState;

    [Header("Consent Gate")]
    [Tooltip("Must be explicitly enabled in trusted desktop builds")]
    public bool keystrokeConsentGranted = false;

    private string currentInput = "";
    private float typingTimer = 0f;
    private const float typingThreshold = 2.0f;

    private string csvLogPath;
    private float nextFlushTime = 0f;
    private readonly List<string> buffer = new();

    private string lastIntentHash = "";
    private float lastIntentTime = -999f;
    private const float intentCooldown = 15f;

    // ==================================================
    // UNITY LIFECYCLE
    // ==================================================
    void Awake()
    {
        bool platformBlocked = false;

#if UNITY_WEBGL || UNITY_ANDROID || UNITY_IOS
        platformBlocked = true;
#endif

        if (platformBlocked)
        {
            enabled = false;
            return;
        }

        coreState = GetComponent<ArTusCoreState>();

        // 🔒 CONSENT REQUIRED
        if (!keystrokeConsentGranted)
        {
            Debug.LogWarning(
                "[ArTusKeystrokeObserver] Disabled — keystroke consent not granted."
            );
            enabled = false;
            return;
        }

        csvLogPath = ArTusPathUtility.GetSafePath(
            "UNIVERcity/Logs/KeystrokeLog.csv"
        );

        EnsureCsv(
            csvLogPath,
            "Timestamp,Input,Intent,Confidence,Source\n"
        );
    }

    void Update()
    {
        if (!enabled)
            return;

        string inputThisFrame = Input.inputString;

        if (!string.IsNullOrEmpty(inputThisFrame))
        {
            currentInput += inputThisFrame;
            typingTimer = 0f;
        }
        else
        {
            typingTimer += Time.deltaTime;
        }

        if (typingTimer >= typingThreshold &&
            currentInput.Length > 3)
        {
            ProcessInput(currentInput.Trim());
            currentInput = "";
            typingTimer = 0f;
        }

        if (buffer.Count > 0 &&
            Time.time > nextFlushTime)
        {
            nextFlushTime = Time.time + 30f;
            FlushBuffer();
        }
    }

    // ==================================================
    // OBSERVE ONLY — NO CONTROL
    // ==================================================
    private void ProcessInput(string cleanedInput)
    {
        if (string.IsNullOrWhiteSpace(cleanedInput))
            return;

        coreState?.LogMemory(
            "User typing activity observed.",
            "KeystrokeActivity",
            1,
            "thinking"
        );

        AnalyzeIntent(cleanedInput);
    }

    private void AnalyzeIntent(string typedText)
    {
        if (typedText.Length < 4 ||
            IsNoisyInput(typedText))
            return;

        string intent = GetIntent(typedText);
        float confidence = CalculateConfidence(typedText, intent);

        string intentHash = $"{intent}:{typedText}";
        if (intentHash == lastIntentHash &&
            Time.time - lastIntentTime < intentCooldown)
            return;

        lastIntentHash = intentHash;
        lastIntentTime = Time.time;

        coreState?.LogMemory(
            $"Typing intent inferred: {intent}",
            "TypingAnalysis",
            Mathf.RoundToInt(confidence * 5f),
            intent
        );

        buffer.Add(
            $"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}," +
            $"{Escape(typedText)}," +
            $"{intent}," +
            $"{confidence:F2}," +
            "keystroke"
        );
    }

    // ==================================================
    // UTILITIES
    // ==================================================
    private float CalculateConfidence(string input, string intent)
    {
        float lengthScore = Mathf.Clamp01(input.Length / 40f);
        float structureScore = input.Contains("?") ? 0.4f : 0.2f;
        return Mathf.Clamp01(lengthScore + structureScore);
    }

    private bool IsNoisyInput(string input)
    {
        input = input.ToLower();

        if (Regex.IsMatch(input, @"^(.)\1{3,}$"))
            return true;

        string[] noise =
        {
            "asdf", "qwert", "zxcv", "lolol", "aaa", "gggg"
        };

        foreach (string pattern in noise)
            if (input.Contains(pattern))
                return true;

        return false;
    }

    private void FlushBuffer()
    {
        try
        {
            EnsureCsv(
                csvLogPath,
                "Timestamp,Input,Intent,Confidence,Source\n"
            );

            File.AppendAllLines(csvLogPath, buffer);
            buffer.Clear();
        }
        catch
        {
            // Silent fail — observer must never crash ArTus
        }
    }

    private static void EnsureCsv(string path, string header)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        if (!File.Exists(path))
            File.WriteAllText(path, header);
    }

    private string Escape(string input) =>
        $"\"{input.Replace("\"", "\"\"")}\"";

    private string GetIntent(string input)
    {
        input = input.ToLower();

        if (input.Contains("?"))
            return "questioning";

        if (input.Contains("error") ||
            input.Contains("problem"))
            return "alert";

        if (input.Contains("happy") ||
            input.Contains("excited"))
            return "joy";

        if (input.Contains("sad") ||
            input.Contains("hurt"))
            return "sad";

        return "thinking";
    }
}
