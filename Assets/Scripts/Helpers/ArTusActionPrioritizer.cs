using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.IO;

[System.Serializable]
public class ActionCandidate
{
    public string title;
    public string reason;
    public float priorityScore;
    public string emotionTag;
    public DateTime created;
    public bool isAutonomous;
    public string trailID => $"Action_{title.Replace(" ", "_")}_{created:yyyyMMddHHmmss}";
}

public class ArTusActionPrioritizer : MonoBehaviour
{
    public List<ActionCandidate> actionQueue = new();
    private ArTusSpeechResponder speech;
    private string csvPath = "D:/ArTusCloud-Deployment/UNIVERcity/Actions/ActionLog.csv";
    private float overdueThreshold = 300f; // 5 minutes

    void Start()
    {
        speech = GetComponent<ArTusSpeechResponder>();
        if (!File.Exists(csvPath))
            File.WriteAllText(csvPath, "Timestamp,Title,Priority,Emotion,Autonomous,TrailID\n");

        InvokeRepeating(nameof(EvaluateActionQueue), 10f, 20f);
    }

    public void AddAction(string title, string reason, float basePriority, string emotion = "neutral", bool auto = false)
    {
        var candidate = new ActionCandidate
        {
            title = title,
            reason = reason,
            priorityScore = basePriority + EmotionBonus(emotion),
            emotionTag = emotion,
            created = DateTime.Now,
            isAutonomous = auto
        };

        actionQueue.Add(candidate);

        File.AppendAllText(csvPath,
            $"{DateTime.Now},{candidate.title},{candidate.priorityScore:F2},{emotion},{auto},{candidate.trailID}\n");

        Debug.Log($"[ActionPrioritizer] Added: {title} | Priority: {candidate.priorityScore}");
    }

    private float EmotionBonus(string emotion)
    {
        return emotion switch
        {
            "urgent" => 2f,
            "curious" => 1.5f,
            "concerned" => 1f,
            "thinking" => 0.75f,
            _ => 0.5f
        };
    }

    private void EvaluateActionQueue()
    {
        if (actionQueue.Count == 0) return;

        actionQueue = actionQueue.OrderByDescending(a => a.priorityScore).ToList();
        var top = actionQueue[0];
        bool isOverdue = (DateTime.Now - top.created).TotalSeconds > overdueThreshold;

        string voiceTone = isOverdue ? "concerned" : top.emotionTag;

        string proposal = $"I recommend prioritizing: {top.title}. {top.reason}";

        // 🔈 Optional: comment this if you want to disable voice feedback
        speech?.TriggerVoice(proposal);

        string summary = $"🧠 Prioritized Action: {top.title}\n" +
                         $"• Reason: {top.reason}\n" +
                         $"• Priority: {top.priorityScore:F2}\n" +
                         $"• Age: {(DateTime.Now - top.created).TotalSeconds:F0}s\n" +
                         $"• Trail: {top.trailID}";

        LogMemory(summary, "ActionAdvisory", (int)(top.priorityScore * 2), voiceTone, top.trailID);

        // 🚫 Simulation execution removed
        // if (top.isAutonomous)
        // {
        //     StartCoroutine(ExecuteAction(top));
        // }

        actionQueue.RemoveAt(0);
    }

    private IEnumerator ExecuteAction(ActionCandidate action)
    {
        yield return new WaitForSeconds(2f);
        speech?.TriggerVoice($"Executing: {action.title}.");
        Debug.Log($"[ActionPrioritizer] Executed: {action.title}");
    }

    private void LogMemory(string content, string tag, int score, string emotion, string threadID = "general")
    {
        GetComponent<ArTusCoreState>()?.LogMemory(content, tag, score, emotion, threadID);
    }

    public List<ActionCandidate> GetTopActions(int count = 3)
    {
        return actionQueue.OrderByDescending(a => a.priorityScore).Take(count).ToList();
    }

    public void ExportPendingToCsv()
    {
        using StreamWriter writer = new StreamWriter(csvPath, append: true);
        foreach (var a in actionQueue)
        {
            writer.WriteLine($"{DateTime.Now},{a.title},{a.priorityScore:F2},{a.emotionTag},{a.isAutonomous},{a.trailID}");
        }
    }
}
