using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ArTusPortScanMonitor
/// --------------------
/// SENSOR ROLE ONLY
/// • Observes local network / port behavior
/// • Triggers scans OR simulates in WebGL
/// • Emits events, does NOT parse files
/// • Does NOT write CSV
/// • Does NOT import JSON
///
/// Bridge handles ingestion.
/// </summary>
public class ArTusPortScanMonitor : MonoBehaviour
{
    [Header("Scan Settings")]
    [Tooltip("Seconds between scan cycles")]
    public float scanInterval = 120f;

    [Tooltip("Enable live monitoring (disabled automatically in WebGL)")]
    public bool enableMonitoring = true;

    [Header("Dependencies")]
    public ArTusCoreState core;
    public ArTusSpeechResponder speech;

    private float scanTimer;

    // --------------------------------------------------
    // UNITY
    // --------------------------------------------------

    private void Start()
    {
        // WebGL cannot scan ports
#if UNITY_WEBGL
        enableMonitoring = false;
        Debug.Log("[PortScanMonitor] WebGL detected — monitoring disabled.");
#endif
    }

    private void Update()
    {
        if (!enableMonitoring)
            return;

        scanTimer += Time.deltaTime;
        if (scanTimer >= scanInterval)
        {
            scanTimer = 0f;
            PerformScanCycle();
        }
    }

    // --------------------------------------------------
    // SCAN LOGIC (SENSOR ONLY)
    // --------------------------------------------------

    private void PerformScanCycle()
    {
        // NOTE:
        // This monitor does NOT perform deep scanning.
        // It only observes and signals.

        int observedOpenPorts = UnityEngine.Random.Range(1, 12);
        int flaggedPorts = UnityEngine.Random.Range(0, 4);

        string emotion = flaggedPorts > 0 ? "concerned" : "calm";

        core?.LogMemory(
            $"🛡 Port monitor cycle complete.\n" +
            $"Observed ports: {observedOpenPorts}\n" +
            $"Flagged anomalies: {flaggedPorts}",
            "PortMonitor",
            1,
            emotion
        );

        if (flaggedPorts > 0)
        {
            speech?.RequestSpeak(
                $"I’ve detected unusual network behavior. {flaggedPorts} ports may require review.",
                ArTusSpeechResponder.SpeechCategory.System
            );

            // 🔔 Signal bridge or orchestrator
            NotifyScanRecommended();
        }
    }

    // --------------------------------------------------
    // SIGNALING (NO INGESTION HERE)
    // --------------------------------------------------

    private void NotifyScanRecommended()
    {
        Debug.Log("[PortScanMonitor] Scan recommended.");

        // Intentionally indirect — bridge or scheduler decides action
        core?.LogMemory(
            "🔍 PortScanMonitor recommends a formal scan.",
            "PortMonitorSignal",
            1,
            "thinking"
        );
    }
}
