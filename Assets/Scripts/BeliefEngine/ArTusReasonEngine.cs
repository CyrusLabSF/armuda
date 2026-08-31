using UnityEngine;

public class ArTusReasoningEngine : MonoBehaviour
{
    private ArTusCoreState core;
    private ArTusBeliefRefiner beliefRefiner;
    private ArTusBeliefEngine beliefEngine;

    void Awake()
    {
        core = GetComponent<ArTusCoreState>();
        beliefRefiner = GetComponent<ArTusBeliefRefiner>();
        beliefEngine = GetComponent<ArTusBeliefEngine>();
    }

    // =========================================================
    // CORE LOGGING
    // =========================================================

    private void LogReasoning(string content, string tag, int strength, string emotion)
    {
        Debug.Log($"[🧠 Reasoning] {content}");
        core?.LogMemory(content, tag, strength, emotion);
    }

    private void RegisterReasoningBelief(string topic, float confidence)
    {
        // Preferred path
        if (beliefRefiner != null)
        {
            beliefRefiner.AddBelief(topic, confidence);
            return;
        }

        // Fallback (safe)
        if (beliefEngine != null)
        {
            beliefEngine.RegisterBelief(
                topic,
                confidence,
                "reasoning",
                "curious",
                null
            );
        }
    }

    // =========================================================
    // 🔗 CONCEPT RELATION
    // =========================================================

    public void RelateConcepts(string conceptA, string conceptB)
    {
        if (string.IsNullOrWhiteSpace(conceptA) || string.IsNullOrWhiteSpace(conceptB))
            return;

        string msg = $"🔗 Connecting '{conceptA}' ↔ '{conceptB}'";
        LogReasoning(msg, "ConceptRelation", 2, "curious");

        string beliefKey = $"relation:{conceptA}<->{conceptB}";

        RegisterReasoningBelief(beliefKey, 0.7f);

        core?.QueueDeferredReflection(
            $"Relationship formed between {conceptA} and {conceptB}",
            "ReasoningRelation",
            0.4f
        );

        core?.TagBeliefTrail($"ReasonTrail_{conceptA}_{conceptB}");
    }

    // =========================================================
    // 🪞 ANALOGY
    // =========================================================

    public void GenerateAnalogy(string sourceConcept)
    {
        if (string.IsNullOrWhiteSpace(sourceConcept))
            return;

        string msg = $"🪞 Generating analogy from '{sourceConcept}'";
        LogReasoning(msg, "Analogy", 2, "imaginative");

        string beliefKey = $"analogy:{sourceConcept}";

        RegisterReasoningBelief(beliefKey, 0.6f);

        core?.QueueDeferredReflection(
            $"Analogy generated from '{sourceConcept}'",
            "ReasoningAnalogy",
            0.5f
        );

        Debug.Log($"[Analogy] Skipped simulation → analogy_sim:{sourceConcept}");
    }

    // =========================================================
    // ❓ WHY INQUIRY
    // =========================================================

    public void AskWhy(string topic)
    {
        if (string.IsNullOrWhiteSpace(topic))
            return;

        string msg = $"❓ Why does '{topic}' matter?";
        LogReasoning(msg, "ReflectiveInquiry", 3, "inquisitive");

        string beliefKey = $"question:why:{topic}";

        RegisterReasoningBelief(beliefKey, 0.8f);

        core?.QueueDeferredReflection(
            $"Why-question raised about '{topic}'",
            "ReasoningWhy",
            0.8f
        );

        core?.QueueDeferredReflection(
            $"Investigate importance of {topic}",
            "ReasoningWhyFollowup",
            0.7f
        );
    }
}