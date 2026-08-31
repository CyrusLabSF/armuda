using UnityEngine;
using System;
using System.IO;

[Serializable]
public class ArTusRuntimeStatus
{
    // Absolute totals (dashboard truth)
    public int memoryTotal;
    public int diskWritesTotal;
    public int diskQueueDepth;

    // Deltas (trend / speed)
    public int memoryDelta;
    public int diskDelta;

    // System health
    public int heartbeat;
    public float activityScore;

    // Metadata
    public string timestamp;
}

public class ArTusRuntimeReporter : MonoBehaviour
{
    // ===============================
    // Singleton
    // ===============================
    public static ArTusRuntimeReporter Instance;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ===============================
    // Counters (written by other systems)
    // ===============================
    [Header("Totals")]
    public int memoryLogCounter;
    public int diskWriteCounter;

    [Header("Queues")]
    public int diskQueueDepth;

    [Header("Heartbeat")]
    public int heartbeatCount;
    public float heartbeatInterval = 1f;

    [Header("Activity")]
    [Range(0f, 1f)]
    public float activityScore;

    // ===============================
    // Internals
    // ===============================
    private float lastHeartbeatTime;
    private float lastStatusWriteTime;

    private int lastMemoryCounter;
    private int lastDiskCounter;

    private const float STATUS_WRITE_INTERVAL = 1.0f;

    // ===============================
    // Update Loop (VERY LIGHT)
    // ===============================
    void Update()
    {
        // Heartbeat
        if (Time.time - lastHeartbeatTime >= heartbeatInterval)
        {
            heartbeatCount++;
            lastHeartbeatTime = Time.time;
        }

        WriteRuntimeStatusSnapshot();
    }

    // ===============================
    // External hooks (SAFE)
    // ===============================
    public void RegisterMemoryLog()
    {
        memoryLogCounter++;
    }

    public void RegisterDiskWrite()
    {
        diskWriteCounter++;
    }

    public void SetDiskQueueDepth(int depth)
    {
        diskQueueDepth = depth;
    }

    public void SetActivityScore(float value)
    {
        activityScore = Mathf.Clamp01(value);
    }

    // ===============================
    // Snapshot Writer (Web sync)
    // ===============================
    private void WriteRuntimeStatusSnapshot()
    {
        if (Time.time - lastStatusWriteTime < STATUS_WRITE_INTERVAL)
            return;

        lastStatusWriteTime = Time.time;

        int memDelta = memoryLogCounter - lastMemoryCounter;
        int diskDelta = diskWriteCounter - lastDiskCounter;

        lastMemoryCounter = memoryLogCounter;
        lastDiskCounter = diskWriteCounter;

        var status = new ArTusRuntimeStatus
        {
            memoryTotal = memoryLogCounter,
            diskWritesTotal = diskWriteCounter,
            diskQueueDepth = diskQueueDepth,

            memoryDelta = memDelta,
            diskDelta = diskDelta,

            heartbeat = heartbeatCount,
            activityScore = activityScore,

            timestamp = DateTime.UtcNow.ToString("HH:mm:ss")
        };

        try
        {
            string json = JsonUtility.ToJson(status, true);

            // 🔑 MUST match FastAPI static mount
            string dir = Path.Combine(
                Application.dataPath,
                "..",
                "Interfaces",
                "API",
                "static"
            );

            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            string finalPath = Path.Combine(dir, "artus_status.json");
            string tempPath = finalPath + ".tmp";

            // Atomic write (prevents partial reads)
            File.WriteAllText(tempPath, json);
            File.Copy(tempPath, finalPath, true);
            File.Delete(tempPath);
        }
        catch
        {
            // Silent fail — runtime dashboard must never stall Unity
        }
    }
}
