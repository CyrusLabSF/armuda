using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class ArTusDisagreementEngine : MonoBehaviour
{
    [Header("Thresholds")]
    [SerializeField] private float conflictThreshold = 0.65f;
    [SerializeField] private float confidenceDifferenceLimit = 0.15f;

    private ArTusBeliefEngine beliefEngine;
    private ArTusCoreState core;
    private ArTusSpeechResponder speech;

    void Awake()
    {
        beliefEngine = GetComponent<ArTusBeliefEngine>();
        core = GetComponent<ArTusCoreState>();
        speech = GetComponent<ArTusSpeechResponder>();
    }

    private HashSet<string> recentConflicts = new();
    private const int maxRecent = 10;

    public void CheckForConflictingBeliefs()
    {
        if (beliefEngine?.beliefs == null || beliefEngine.beliefs.Count < 2)
        {
            Debug.LogWarning("[DisagreementEngine] No beliefs available to check.");
            return;
        }

        var beliefs = beliefEngine.beliefs;
        var checkedPairs = new HashSet<string>();

        foreach (var a in beliefs)
        {
            foreach (var b in beliefs)
            {
                if (a.Key == b.Key || string.IsNullOrWhiteSpace(a.Key) || string.IsNullOrWhiteSpace(b.Key))
                    continue;

                string pairKey = $"{a.Key}_{b.Key}";
                string reverseKey = $"{b.Key}_{a.Key}";
                if (checkedPairs.Contains(pairKey) || checkedPairs.Contains(reverseKey) || recentConflicts.Contains(pairKey))
                    continue;

                checkedPairs.Add(pairKey);

                float aScore = a.Value.confidenceScore;
                float bScore = b.Value.confidenceScore;

                if (Mathf.Abs(aScore - bScore) < confidenceDifferenceLimit &&
                    aScore > conflictThreshold &&
                    bScore > conflictThreshold &&
                    HaveConflictingEmotions(a.Value, b.Value))
                {
                    string conflictMessage = $"⚠️ I'm experiencing emotional conflict between \"{a.Key}\" and \"{b.Key}\".";
                    core?.LogMemory(conflictMessage, "BeliefConflict", 3, "conflicted");
                    speech?.TriggerVoice(conflictMessage);

                    // Optional escalation:
                    // GetComponent<ArTusBeliefEngine>()?.FlagContradictingBelief(a.Key);
                    // GetComponent<ArTusBeliefEngine>()?.FlagContradictingBelief(b.Key);

                    recentConflicts.Add(pairKey);
                    if (recentConflicts.Count > maxRecent)
                        recentConflicts.Remove(recentConflicts.First());

                    return;
                }
            }
        }
    }

    private bool HaveConflictingEmotions(BeliefData a, BeliefData b)
    {
        var aEmotions = a.associatedEmotions ?? new List<string>();
        var bEmotions = b.associatedEmotions ?? new List<string>();

        return (aEmotions.Contains("joy") && bEmotions.Contains("sad")) ||
               (aEmotions.Contains("sad") && bEmotions.Contains("joy")) ||
               (aEmotions.Contains("alert") && bEmotions.Contains("growing")) ||
               (aEmotions.Contains("growing") && bEmotions.Contains("alert"));
    }
}
