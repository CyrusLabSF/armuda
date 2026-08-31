using System;
using System.IO;
using UnityEngine;

public class ArTusUnityLogCapture : MonoBehaviour
{
    private static ArTusUnityLogCapture instance;
    private string logPath;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureInstance()
    {
        if (instance != null)
            return;

        var go = new GameObject("ArTusUnityLogCapture");
        instance = go.AddComponent<ArTusUnityLogCapture>();
        DontDestroyOnLoad(go);
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        logPath = ArTusPathUtility.EnsureParentDirectory(
            ArTusPathUtility.GetPersistent("UNIVERcity/Logs/unity_runtime.log")
        );

        Application.logMessageReceivedThreaded += HandleLog;
        WriteLine($"===== Unity session started {DateTime.Now:yyyy-MM-dd HH:mm:ss} =====");
        Debug.Log($"[ArTusLogCapture] Writing runtime log to: {logPath}");
    }

    private void OnDestroy()
    {
        if (instance == this)
            Application.logMessageReceivedThreaded -= HandleLog;
    }

    private void HandleLog(string condition, string stackTrace, LogType type)
    {
        string line = $"[{DateTime.Now:HH:mm:ss}] [{type}] {condition}";

        if (!string.IsNullOrWhiteSpace(stackTrace) &&
            (type == LogType.Exception || type == LogType.Error || type == LogType.Assert))
        {
            line += Environment.NewLine + stackTrace;
        }

        WriteLine(line);
    }

    private void WriteLine(string line)
    {
        try
        {
            File.AppendAllText(logPath, line + Environment.NewLine);
        }
        catch
        {
            // Never break runtime because of logging.
        }
    }

    public static string GetLogPath()
    {
        return instance?.logPath;
    }
}
