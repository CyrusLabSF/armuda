using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ArTusTypes;

public static class ArTusContradictionDetector
{
    public static List<ContradictionEntry> ScanForContradictions(
        Dictionary<string, BeliefData> beliefs,
        ArTusCoreState core,
        CyberSpaceManager cyberSpace
    )
    {
        var results = new List<ContradictionEntry>();

        if (beliefs == null || beliefs.Count < 2)
            return results;

        var keys = beliefs.Keys.ToList();

        int maxChecks = 100;
        int checks = 0;

        for (int i = 0; i < keys.Count; i++)
        {
            for (int j = i + 1; j < keys.Count; j++)
            {
                if (checks++ > maxChecks)
                    break;

                string beliefA = keys[i];
                string beliefB = keys[j];

                BeliefData dataA = beliefs[beliefA];
                BeliefData dataB = beliefs[beliefB];

                if (!AreContradictory(dataA, dataB))
                    continue;

                int severity = CalculateSeverity(dataA, dataB);

                var entry = new ContradictionEntry
                {
                    timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),

                    // 🔗 Identity
                    threadA = beliefA,
                    threadB = beliefB,

                    // 🔥 CORRECT FIELD USAGE
                    contentA = dataA.belief,
                    contentB = dataB.belief,

                    // 📊 Confidence
                    certaintyA = dataA.confidenceScore,
                    certaintyB = dataB.confidenceScore,

                    // 🏆 Dominance
                    dominant = dataA.confidenceScore > dataB.confidenceScore
                        ? beliefA
                        : beliefB,

                    // 🔥 Smart severity
                    severityScore = Mathf.Clamp01(
                        (dataA.confidenceScore + dataB.confidenceScore) / 2f
                    ),

                    emotion = "alert",
                    resolution = "pending",
                    resolved = false,
                    encounterCount = 1
                };

                results.Add(entry);

                Debug.LogWarning(
                    $"[Contradiction] Conflict found between {beliefA} and {beliefB}"
                );

                core?.LogMemory(
                    $"Contradiction detected between {beliefA} and {beliefB}",
                    "Contradiction",
                    severity,
                    "alert"
                );

                cyberSpace?.RegisterDiplomaticEvent(
                    "ContradictionManager",
                    "Domain Contradiction",
                    $"Conflict detected between {beliefA} and {beliefB}",
                    "alert"
                );
            }
        }

        return results;
    }

    private static bool AreContradictory(BeliefData a, BeliefData b)
    {
        if (a == null || b == null)
            return false;

        // 🔥 Now using REAL belief content
        return a.confidenceScore > 0.6f &&
               b.confidenceScore > 0.6f &&
               a.belief != b.belief;
    }

    private static int CalculateSeverity(BeliefData a, BeliefData b)
    {
        int baseSeverity = 2;

        if (a.confidenceScore > 0.8f && b.confidenceScore > 0.8f)
            baseSeverity = 4;

        return baseSeverity;
    }
}