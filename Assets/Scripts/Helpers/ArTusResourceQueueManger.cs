using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

/// <summary>
/// Manages queued learning resources (topics, URLs, tasks).
/// Does NOT ingest — only queues, dequeues, persists.
/// </summary>
public class ArTusResourceQueueManager : MonoBehaviour
{
    [Header("Queue Settings")]
    [SerializeField] private bool enablePersistence = true;
    [SerializeField] private bool verboseLogs = false;

    private readonly Queue<string> resourceQueue = new();
    private string queueFilePath;

    private ArTusCoreState core;

    // --------------------------------------------------
    // Unity Lifecycle
    // --------------------------------------------------

    void Awake()
    {
        core = GetComponent<ArTusCoreState>();

        // ✅ Safe initialization
        queueFilePath = ArTusPathUtility.GetPersistent(
            "UNIVERcity/Queues/resource_queue.txt"
        );

        if (enablePersistence)
            LoadQueueFromDisk();
    }

    void OnDisable()
    {
        if (enablePersistence)
            SaveQueueToDisk();
    }

    // --------------------------------------------------
    // Public API
    // --------------------------------------------------

    /// <summary>
    /// Add a topic/resource to the queue
    /// </summary>
    public void Enqueue(string resource)
    {
        if (string.IsNullOrWhiteSpace(resource))
            return;

        resourceQueue.Enqueue(resource);

        if (verboseLogs)
            Debug.Log($"[ResourceQueue] + {resource}");

        core?.LogMemory(
            $"📥 Queued resource: {resource}",
            "ResourceQueue",
            1,
            "curious"
        );

        if (enablePersistence)
            SaveQueueToDisk();
    }

    /// <summary>
    /// Remove and return next resource (FIFO)
    /// </summary>
    public string Dequeue()
    {
        if (resourceQueue.Count == 0)
            return null;

        string next = resourceQueue.Dequeue();

        if (verboseLogs)
            Debug.Log($"[ResourceQueue] → {next}");

        if (enablePersistence)
            SaveQueueToDisk();

        return next;
    }

    /// <summary>
    /// Peek without removing
    /// </summary>
    public string Peek()
    {
        return resourceQueue.Count > 0
            ? resourceQueue.Peek()
            : null;
    }

    public int Count => resourceQueue.Count;

    public void Clear()
    {
        resourceQueue.Clear();

        if (enablePersistence)
            SaveQueueToDisk();
    }

    // --------------------------------------------------
    // Persistence
    // --------------------------------------------------

    private void LoadQueueFromDisk()
    {
        try
        {
            if (!File.Exists(queueFilePath))
                return;

            foreach (var line in File.ReadAllLines(queueFilePath))
            {
                string trimmed = line.Trim();
                if (!string.IsNullOrEmpty(trimmed))
                    resourceQueue.Enqueue(trimmed);
            }

            if (verboseLogs)
                Debug.Log($"[ResourceQueue] Loaded {resourceQueue.Count} items.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ResourceQueue] Load failed: {ex.Message}");
        }
    }

    private void SaveQueueToDisk()
    {
        try
        {
            string dir = Path.GetDirectoryName(queueFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllLines(queueFilePath, resourceQueue.ToArray());
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ResourceQueue] Save failed: {ex.Message}");
        }
    }
}
