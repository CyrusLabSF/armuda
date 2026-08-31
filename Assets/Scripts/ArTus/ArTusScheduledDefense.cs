using UnityEngine;
using System;
using System.Collections;

using UnityDebug = UnityEngine.Debug;

public class ArTusScheduledDefense : MonoBehaviour
{
    [Header("Mode")]
    public bool betaMode = true;

    [Header("Defense Schedule")]
    [SerializeField] private float scanIntervalSeconds = 180f; // slower for beta
    [SerializeField] private bool enableAutoScan = false; // OFF by default for beta

    private bool isScanning;
    private float lastScanTime;

    // Cached references
    private ArTusCoreState core;
    private ArTusFirewall firewall;
    private ArTusSpeechResponder speech;

    void Awake()
    {
        core = GetComponent<ArTusCoreState>();
        firewall = GetComponent<ArTusFirewall>();
        speech = GetComponent<ArTusSpeechResponder>();
    }

    void Start()
    {
        lastScanTime = Time.time;

        if (enableAutoScan && !betaMode)
            StartCoroutine(DefenseLoop());
    }

    private IEnumerator DefenseLoop()
    {
        while (enableAutoScan)
        {
            yield return new WaitForSeconds(scanIntervalSeconds);

            if (!isScanning)
                RunScheduledDefense();
        }
    }

    public void RunScheduledDefense()
    {
        if (isScanning)
            return;

        isScanning = true;
        lastScanTime = Time.time;

        UnityDebug.Log("[Defense] Passive defense scan executed.");

        try
        {
            // -----------------------------
            // PASSIVE CHECK ONLY
            // -----------------------------
            bool firewallActive = firewall != null && firewall.enabled;

            if (!betaMode && firewallActive)
            {
                HandlePotentialThreat();
            }
            else
            {
                SafeLog(
                    "Security scan completed. System stable.",
                    "SecurityCheck",
                    1,
                    "calm"
                );
            }
        }
        catch (Exception ex)
        {
            UnityDebug.LogError($"[Defense] Exception during defense scan: {ex.Message}");
        }
        finally
        {
            isScanning = false;
        }
    }

    private void HandlePotentialThreat()
    {
        string message = "Potential condition observed. Monitoring.";

        UnityDebug.Log($"[Defense] {message}");

        SafeLog(
            message,
            "SecurityAlert",
            2,
            "alert"
        );

        // 🔇 NO speech in beta
        if (!betaMode)
        {
            speech?.TriggerVoice(
                "Monitoring system integrity. No action required."
            );
        }
    }

    private void SafeLog(string content, string category, float score, string emotion)
    {
        if (!betaMode)
        {
            core?.LogMemory(
                content,
                category,
                score,
                emotion,
                speaker: "ArTus",
                threadID: "defense"
            );
        }
    }

#if UNITY_EDITOR
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F12))
        {
            UnityDebug.Log("[Defense] Manual defense scan triggered.");
            RunScheduledDefense();
        }
    }
#endif
}