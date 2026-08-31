using UnityEngine;
using System;

public class ArTusThoughtSystem : MonoBehaviour
{
    [Header("Dependencies")]
    public ArTusSemanticSearch semanticSearch;
    public ArTusCertaintyModel certaintyModel;
    public ArTusRecursiveRefiner recursiveRefiner;

    private ArTusCoreState core;
    private ArTusGoalController goalController;

    [Header("Mode")]
    public bool betaMode = true;

    [Header("Behavior")]
    public bool enableSpeech = false;

    [Header("Thresholds")]
    public float highConfidence = 0.7f;
    public float mediumConfidence = 0.4f;

    void Awake()
    {
        core = GetComponent<ArTusCoreState>();
        goalController = GetComponent<ArTusGoalController>();
    }

    // --------------------------------------------------
    // MAIN ENTRY (THIS IS THE BRAIN)
    // --------------------------------------------------
    public string ProcessThought(string userInput)
    {
        if (string.IsNullOrWhiteSpace(userInput))
            return "I didn’t catch that.";

        // -----------------------------
        // STEP 1: SEMANTIC SEARCH
        // -----------------------------
        var semantic = semanticSearch?.Query(userInput);

        // -----------------------------
        // STEP 2: CERTAINTY ESTIMATION
        // -----------------------------
        var certainty = certaintyModel != null
            ? certaintyModel.Estimate(userInput)
            : default;

        float confidence = semantic != null && semantic.success
            ? semantic.confidence
            : certainty.value;

        string response;

        // -----------------------------
        // STEP 3: DECISION LOGIC
        // -----------------------------
        if (semantic != null && semantic.success && confidence >= highConfidence)
        {
            TriggerVerificationFollowup(userInput, semantic);
            response = semantic.answer;
        }
        else if (confidence >= mediumConfidence)
        {
            if (semantic != null && semantic.success && semantic.verificationState == "conflicted")
            {
                TriggerVerificationFollowup(userInput, semantic);
                response =
                    "I found multiple evidence-backed sources, but they conflict. " +
                    "I should verify this further before giving a definitive answer.";
            }
            else if (semantic != null && semantic.success && semantic.verificationState == "single_source")
            {
                TriggerVerificationFollowup(userInput, semantic);
                response =
                    "I found one promising source, but I need more evidence before I treat it as verified knowledge.";
            }
            else if (semantic != null && semantic.success && semantic.isEvidenceBacked)
            {
                response =
                    $"I found evidence about {semantic.topic}: {semantic.evidenceSummary}";

                if (!string.IsNullOrWhiteSpace(semantic.sourceUrl))
                    response += $" (source: {semantic.sourceUrl})";

                if (semantic.supportingEvidenceCount > 1)
                    response += $" [{semantic.supportingEvidenceCount} supporting sources]";
            }
            else
            {
                response =
                    $"I believe this relates to {semantic?.topic ?? "that topic"}, " +
                    $"but I’m not fully certain yet.";
            }
        }
        else
        {
            response =
                "I’m not confident enough in my understanding yet. Let me think deeper.";

            // 🔁 Trigger deeper thinking (SAFE)
            recursiveRefiner?.TryRecursiveRefinement(userInput);
        }

        // -----------------------------
        // STEP 4: SAFE MEMORY LOG
        // -----------------------------
        if (!betaMode)
        {
            core?.LogMemory(
                $"Thought processed: {userInput} → {response}",
                "Thought",
                1,
                "thinking"
            );
        }

        // -----------------------------
        // STEP 5: OPTIONAL SPEECH
        // -----------------------------
        if (!betaMode && enableSpeech)
        {
            core?.TriggerVoice(response);
        }

        return response;
    }

    private void TriggerVerificationFollowup(string userInput, ArTusSemanticSearch.SemanticResult semantic)
    {
        if (semantic == null || !semantic.success || goalController == null)
            return;

        if (semantic.verificationState != "conflicted" && semantic.verificationState != "single_source")
            return;

        bool queued = goalController.TryQueueVerificationGoal(
            semantic.topic,
            semantic.domain,
            semantic.verificationState,
            userInput,
            semantic.confidence,
            semantic.citations
        );

        if (queued && !betaMode)
        {
            core?.LogMemory(
                $"Verification follow-up created for '{semantic.topic}' from {semantic.verificationState} evidence.",
                "ThoughtVerification",
                2,
                semantic.verificationState == "conflicted" ? "conflicted" : "curious"
            );
        }
    }
}
