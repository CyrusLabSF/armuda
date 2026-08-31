using UnityEngine;
using System;
using System.IO;
using System.Collections;

using UnityDebug = UnityEngine.Debug;

public class ArTusServiceReflection : MonoBehaviour
{
    [Header("Mode")]
    public bool betaMode = true;

    [Header("Reflection Settings")]
    [SerializeField] private float reflectionIntervalSeconds = 120f; // slower for beta
    [SerializeField] private bool enableReflection = true;

    private string reflectionLogPath;

    private ArTusCoreState core;
    private bool isReflecting;

    void Awake()
    {
        core = GetComponent<ArTusCoreState>();

        reflectionLogPath =
            ArTusPathUtility.GetPersistent("UNIVERcity/Services/ServiceReflection.log");

        string dir = Path.GetDirectoryName(reflectionLogPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
    }

    void Start()
    {
        if (enableReflection)
            StartCoroutine(ReflectionLoop());
    }

    private IEnumerator ReflectionLoop()
    {
        while (enableReflection)
        {
            yield return new WaitForSeconds(reflectionIntervalSeconds);

            if (!isReflecting)
                RunServiceReflection();
        }
    }

    public void RunServiceReflection()
    {
        if (isReflecting)
            return;

        isReflecting = true;

        try
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            string reflection =
                $"[{timestamp}] Service reflection executed.";

            // -----------------------------
            // SAFE MEMORY LOGGING
            // -----------------------------
            if (!betaMode)
            {
                core?.LogMemory(
                    "Completed service-level reflection cycle.",
                    "ServiceReflection",
                    1,
                    "thinking",
                    speaker: "ArTus",
                    threadID: "service-reflection"
                );
            }

            // -----------------------------
            // SAFE DISK WRITE
            // -----------------------------
            File.AppendAllText(
                reflectionLogPath,
                reflection + Environment.NewLine
            );

            UnityDebug.Log("[ServiceReflection] Reflection completed.");
        }
        catch (Exception ex)
        {
            UnityDebug.LogError($"[ServiceReflection] Failed: {ex.Message}");
        }
        finally
        {
            isReflecting = false;
        }
    }

#if UNITY_EDITOR
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F10))
        {
            UnityDebug.Log("[ServiceReflection] Manual trigger.");
            RunServiceReflection();
        }
    }
#endif
}