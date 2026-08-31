using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// DeceptionController
/// Manages a controlled "false hole" where adversaries are
/// delayed, misled, and profiled while the real system remains shielded.
/// </summary>
public class DeceptionController : MonoBehaviour
{
    private ArTusCoreState core;
    private ArTusSpeechResponder speech;
    private AdversaryTrailManager adversaryTrail;

    private enum DeceptionState
    {
        Inactive,
        Active,
        Completed
    }

    private DeceptionState currentState = DeceptionState.Inactive;
    private Coroutine deceptionRoutine;

    // Clearly marked synthetic artifacts
    private readonly List<string> decoyFiles = new()
    {
        "[DECOY]/Finance/Passwords.xlsx",
        "[DECOY]/HR/EmployeeData.docx",
        "[DECOY]/System/Kernel32.dll"
    };

    private readonly List<int> decoyPorts = new()
    {
        21, 22, 80, 443, 3389
    };

    void Awake()
    {
        core = GetComponent<ArTusCoreState>();
        speech = GetComponent<ArTusSpeechResponder>();
        adversaryTrail = GetComponent<AdversaryTrailManager>();
    }

    // ==================================================
    // ENTRY POINT
    // ==================================================
    public void ActivateDeception(string attackerDetail)
    {
        if (currentState == DeceptionState.Active)
            return;

        currentState = DeceptionState.Active;

        core?.LogMemory(
            $"🎭 Deception activated for {attackerDetail}. Attacker redirected into false environment.",
            "Deception",
            4,
            "curious"
        );

        // ✅ Correct API usage
        adversaryTrail?.RegisterAdversaryEvent(
            $"DEC_{attackerDetail}",
            "deception-entry",
            0.6f
        );

        speech?.RequestSpeak(
            "You may continue exploring. What you see is only what I allow.",
            ArTusSpeechResponder.SpeechCategory.Diplomacy
        );

        deceptionRoutine = StartCoroutine(DeceptionLoop(attackerDetail));
    }

    // ==================================================
    // DECEPTION LOOP
    // ==================================================
    private IEnumerator DeceptionLoop(string attackerDetail)
    {
        int step = 0;

        while (currentState == DeceptionState.Active && step < 10)
        {
            yield return new WaitForSeconds(5f);

            string fakeFile =
                decoyFiles[Random.Range(0, decoyFiles.Count)];
            int fakePort =
                decoyPorts[Random.Range(0, decoyPorts.Count)];

            string detail =
                $"Attacker probed {fakeFile} via port {fakePort}";

            core?.LogMemory(
                $"🎭 {detail}",
                "Deception",
                3,
                "alert"
            );

            adversaryTrail?.RegisterAdversaryEvent(
                $"DEC_EVT_{step}",
                "deception-interaction",
                0.3f
            );

            step++;
        }

        ClassifyOutcome(attackerDetail, step);
        EndDeception(attackerDetail);
    }

    // ==================================================
    // OUTCOME
    // ==================================================
    private void ClassifyOutcome(string attackerDetail, int stepsObserved)
    {
        string outcome =
            stepsObserved >= 8 ? "persistent" :
            stepsObserved >= 4 ? "probing" :
            "disengaged";

        adversaryTrail?.RegisterAdversaryEvent(
            $"DEC_OUTCOME_{attackerDetail}",
            $"deception-outcome:{outcome}",
            0.2f
        );

        core?.LogMemory(
            $"🎭 Deception outcome for {attackerDetail}: {outcome}.",
            "DeceptionOutcome",
            2,
            "reflective"
        );
    }

    // ==================================================
    // EXIT
    // ==================================================
    public void EndDeception(string attackerDetail)
    {
        if (currentState != DeceptionState.Active)
            return;

        currentState = DeceptionState.Completed;

        if (deceptionRoutine != null)
            StopCoroutine(deceptionRoutine);

        core?.LogMemory(
            $"🎭 Deception ended for {attackerDetail}. Engagement complete.",
            "Deception",
            3,
            "neutral"
        );

        speech?.RequestSpeak(
            "Deception cycle completed. Defensive posture finalized.",
            ArTusSpeechResponder.SpeechCategory.System
        );
    }
}
