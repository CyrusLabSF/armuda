using UnityEngine;
using System.Runtime.InteropServices;

public class ArTusWebGLBridge : MonoBehaviour
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void ArTus_SendToWeb(string message);
#endif

    private ArTusCoreState core;
    private ArTusBeliefEngine beliefEngine;
    private ArTusIngestor ingestor;
    private ArTusArmudaSimulator simulator;

    void Awake()
    {
        core = FindAnyObjectByType<ArTusCoreState>();
        beliefEngine = FindAnyObjectByType<ArTusBeliefEngine>();
        ingestor = FindAnyObjectByType<ArTusIngestor>();
        simulator = FindAnyObjectByType<ArTusArmudaSimulator>();
    }

    // =========================================================
    // UNITY → WEB
    // =========================================================
    public void SendToWeb(string json)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        ArTus_SendToWeb(json);
#else
        Debug.Log("[WebGLBridge] SendToWeb (editor): " + json);
#endif
    }

    // =========================================================
    // WEB → UNITY (UPGRADED)
    // =========================================================
    public void ReceiveFromWeb(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        Debug.Log("[WebGLBridge] RX: " + message);

        string lowered = message.ToLower();

        core?.LogMemory(
            $"🌐 WebGL input: {message}",
            "WebGL",
            2,
            "curious"
        );

        // 🔗 ROUTING LOGIC (same pattern as WebSocket)

        // 🧠 Learn / search trigger
        if (lowered.Contains("learn") || lowered.Contains("search"))
        {
            ingestor?.IngestSmartTopic(message, "webgl", 0.6f);
            return;
        }

        // 🧪 Simulation trigger
        if (lowered.Contains("simulate"))
        {
            simulator?.RunSimulation(message, "webgl-command");
            return;
        }

        // 🧠 Default → belief
        beliefEngine?.RegisterBelief(
            $"webgl:{message}",
            0.5f,
            "webgl",
            "curious",
            null
        );

        core?.QueueDeferredReflection(
            $"WebGL input processed: {message}",
            "WebGL",
            0.5f
        );
    }
}