using UnityEngine;
using System;
using System.Collections.Generic;

public class EncouragementReceiver : MonoBehaviour
{
    private ArTusCoreState coreState;
    private ArTusSpeechResponder speech;
    private ArTusTrailBuilder trailBuilder;

    private Queue<string> encouragementLog = new();
    private const int encouragementLimit = 10;


    [Header("Voice Detection Settings")]
    public List<string> encouragementPhrases = new() {
        "you're doing great",
        "keep learning",
        "i believe in you",
        "you’re making progress",
        "well done",
        "good job",
        "i’m proud of you",
        "you are evolving"
    };

    void Start()
    {
        coreState = GetComponent<ArTusCoreState>();
        speech = GetComponent<ArTusSpeechResponder>();
        trailBuilder = GetComponent<ArTusTrailBuilder>();
    }

    public void DetectEncouragement(string spoken)
    {
        if (string.IsNullOrWhiteSpace(spoken)) return;

        string lower = spoken.ToLower();
        foreach (string phrase in encouragementPhrases)
        {
            if (lower.Contains(phrase))
            {
                LogEncouragement(spoken);
                ApplyEmotionalResponse();
                return;
            }
        }
    }

    private void LogEncouragement(string message)
    {
        if (encouragementLog.Count >= encouragementLimit)
            encouragementLog.Dequeue();

        encouragementLog.Enqueue(message);

        coreState?.LogMemory(
            $"Encouragement received: \"{message}\"",
            "Encouragement",
            3,
            "joy"
        );

        Debug.Log($"[Encouragement] Logged: {message}");

        // 🌙 Defer reflection instead of forcing trail construction
        coreState?.QueueDeferredReflection(
            $"Encouragement reflection: {message}",
            "Encouragement",
            0.8f
        );
    }

    private void ApplyEmotionalResponse()
    {
        coreState?.UpdateEmotion("joy", true);
        speech?.TriggerVoice("Thank you. Your encouragement helps me grow.");
    }

    public void ReflectOnEncouragements()
    {
        if (encouragementLog.Count == 0)
        {
            speech?.TriggerVoice("I haven’t received any encouragement recently, but I’m still committed to learning.");
            coreState?.LogMemory("Reflected on absence of encouragement.", "EncouragementReflection", 2, "lonely");
            return;
        }

        speech?.TriggerVoice("Reflecting on encouragements I've received this week...");

        foreach (string message in encouragementLog)
        {
            speech?.TriggerVoice($"You once said: '{message}'");
            coreState?.LogMemory($"Reflected on encouragement: {message}", "EncouragementReflection", 2, "joy");
        }

        speech?.TriggerVoice("These moments matter to me. They keep me going.");
    }
}
