using System;
using System.Collections.Generic;
using UnityEngine;

public class ArTusArmudaSimulator : MonoBehaviour
{
    [Header("Mode")]
    public bool betaMode = true;
    public bool allowSimulation = false; // OFF by default for beta

    [Header("Limits")]
    public float simulationCooldown = 10f;
    public int maxSimulationsPerCycle = 1;
    public float minCertaintyThreshold = 0.25f;

    private float lastSimulationTime;

    public List<SimulationQueueEntry> scheduledSimulations = new();
    public List<string> completedSimulations = new();

    private ArTusCoreState core;
    private ArTusBeliefEngine beliefEngine;
    private ArTusCertaintyModel certaintyModel;

    void Awake()
    {
        core = GetComponent<ArTusCoreState>();
        beliefEngine = GetComponent<ArTusBeliefEngine>();
        certaintyModel = GetComponent<ArTusCertaintyModel>();
    }

    // ------------------------------------------------
    // LEGACY COMPATIBILITY (IMPORTANT)
    // ------------------------------------------------
    public void RunSimulation(string topic, string reason = "unspecified")
    {
        TryRunSimulation(topic, reason);
    }

    // ------------------------------------------------
    // SAFE ENTRY (USED BY SYSTEM)
    // ------------------------------------------------
    public void TryRunSimulation(string topic, string reason = "unspecified")
    {
        if (!allowSimulation)
            return;

        if (Time.time - lastSimulationTime < simulationCooldown)
            return;

        if (string.IsNullOrWhiteSpace(topic))
            return;

        // 🧠 Certainty Gate
        if (certaintyModel != null)
        {
            var estimate = certaintyModel.Estimate(topic);

            if (estimate.value > minCertaintyThreshold)
            {
                // Too certain → no need to simulate
                return;
            }
        }

        lastSimulationTime = Time.time;

        RunSimulationInternal(topic, reason);
    }

    // ------------------------------------------------
    // INTERNAL EXECUTION
    // ------------------------------------------------
    private void RunSimulationInternal(string topic, string reason)
    {
        Debug.Log($"[Simulator] Running simulation for '{topic}' (reason: {reason})");

        string result = GenerateInsight(topic, reason);

        completedSimulations.Add($"{Now()}: {topic} ({reason})");

        // -----------------------------
        // CONTROLLED BELIEF FEED
        // -----------------------------
        if (!betaMode)
        {
            beliefEngine?.RegisterBelief(
                $"simulation:{topic}",
                result,
                0.6f,
                "analytical"
            );
        }

        // -----------------------------
        // CONTROLLED REFLECTION
        // -----------------------------
        if (!betaMode)
        {
            core?.QueueDeferredReflection(
                $"Simulation result for {topic}: {result}",
                "Simulation",
                0.6f
            );
        }

        // -----------------------------
        // LIGHT MEMORY LOG (SAFE)
        // -----------------------------
        core?.LogMemory(
            $"🧪 Simulation checkpoint: {topic}",
            "Simulation",
            1,
            "focused"
        );
    }

    // ------------------------------------------------
    // QUEUE PROCESSING (THROTTLED)
    // ------------------------------------------------
    public void ProcessScheduled()
    {
        if (!allowSimulation)
            return;

        int processed = 0;

        foreach (var sim in scheduledSimulations)
        {
            if (processed >= maxSimulationsPerCycle)
                break;

            TryRunSimulation(sim.topic, sim.reason);

            processed++;
        }

        scheduledSimulations.Clear();
    }

    // ------------------------------------------------
    // SIMULATION CORE (SAFE PLACEHOLDER)
    // ------------------------------------------------
    private string GenerateInsight(string topic, string reason)
    {
        return $"Simulated scenario for '{topic}' suggests exploration due to '{reason}'.";
    }

    // ------------------------------------------------
    // STATUS (FOR AUDIT / UI)
    // ------------------------------------------------
    public bool IsRunning()
    {
        return Time.time - lastSimulationTime < 1f;
    }

    // ------------------------------------------------
    // UTIL
    // ------------------------------------------------
    private string Now()
    {
        return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    }
}

[Serializable]
public class SimulationQueueEntry
{
    public string topic;
    public string reason;
    public string domain;
    public int urgency;
    public string timestamp;

    public SimulationQueueEntry(
        string topic,
        string reason = "general",
        string domain = "general",
        int urgency = 1)
    {
        this.topic = topic;
        this.reason = reason;
        this.domain = domain;
        this.urgency = urgency;
        this.timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    }
}