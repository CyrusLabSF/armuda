using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class ArTusApiScheduler : MonoBehaviour
{
    private ArTusCoreState core;
    private ArTusBeliefEngine beliefEngine;
    private ArTusPurposeLoop purposeLoop;
    private ArTusApiManager apiManager;

    [Header("Scheduler Timing")]
    public float checkInterval = 20f;

    [Header("Triggers")]
    public float lowConfidenceThreshold = 0.4f;
    public float curiosityThreshold = 0.6f;

    private float curiosityPressure = 0f;
    private bool isRunning = false;

    void Awake()
    {
        core = GetComponent<ArTusCoreState>() ?? FindAnyObjectByType<ArTusCoreState>();
        beliefEngine = GetComponent<ArTusBeliefEngine>() ?? FindAnyObjectByType<ArTusBeliefEngine>();
        purposeLoop = GetComponent<ArTusPurposeLoop>() ?? FindAnyObjectByType<ArTusPurposeLoop>();
        apiManager = GetComponent<ArTusApiManager>() ?? FindAnyObjectByType<ArTusApiManager>();
    }

    void Start()
    {
        StartCoroutine(SchedulerLoop());
    }

    private IEnumerator SchedulerLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(checkInterval);

            if (!isRunning)
                EvaluateAndTrigger();
        }
    }

    // ------------------------------------------------
    // CORE DECISION LOGIC
    // ------------------------------------------------
    private void EvaluateAndTrigger()
    {
        if (beliefEngine == null || apiManager == null)
            return;

        // 🔍 Get weak beliefs (SAFE access)
        var weakBeliefs = beliefEngine.beliefs
            .Where(b => b.Value != null && b.Value.confidenceScore < lowConfidenceThreshold)
            .Take(3)
            .Select(b => b.Key) // 🔥 Only keep keys (no type dependency)
            .ToList();

        // 🎯 Current purpose
        string currentPurpose = purposeLoop != null
            ? purposeLoop.currentPurpose
            : "";

        // 🔍 Check if purpose needs support
        bool purposeNeedsSupport = false;

        if (!string.IsNullOrEmpty(currentPurpose) &&
            beliefEngine.beliefs.ContainsKey(currentPurpose))
        {
            var belief = beliefEngine.beliefs[currentPurpose];

            if (belief != null && belief.confidenceScore < 0.5f)
                purposeNeedsSupport = true;
        }

        // 🧠 Curiosity pressure
        curiosityPressure += weakBeliefs.Count * 0.2f;

        if (purposeNeedsSupport)
            curiosityPressure += 0.4f;

        // ------------------------------------------------
        // DECISION
        // ------------------------------------------------
        if (curiosityPressure < curiosityThreshold)
            return;

        curiosityPressure = 0f;

        StartCoroutine(ExecuteApiBurst(weakBeliefs, currentPurpose));
    }

    // ------------------------------------------------
    // EXECUTION
    // ------------------------------------------------
    private IEnumerator ExecuteApiBurst(
        List<string> weakBeliefKeys,
        string purpose)
    {
        isRunning = true;

        core?.LogMemory(
            $"🌐 API Scheduler triggered (purpose: {purpose})",
            "ApiScheduler",
            3,
            "curious"
        );

        // 🎯 PRIORITY: Purpose first
        if (!string.IsNullOrEmpty(purpose))
        {
            apiManager.RunRelevantStageForTopic(purpose);
            yield return new WaitForSeconds(3f);
        }

        // 🔍 Then weak beliefs
        foreach (var key in weakBeliefKeys)
        {
            apiManager.RunRelevantStageForTopic(key);
            yield return new WaitForSeconds(2f);
        }

        isRunning = false;
    }
}
