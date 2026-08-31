using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using UnityEngine;

public class MemoryWriteQueue : MonoBehaviour
{
    private static readonly ConcurrentQueue<string> writeQueue = new();
    private static bool isWriting = false;
    private static readonly string memoryPath = "D:/ArTusCloud-Deployment/artus_memory.json";

    void Update()
    {
        if (!isWriting && writeQueue.Count > 0)
        {
            isWriting = true;
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    using (StreamWriter sw = new StreamWriter(memoryPath, true))
                    {
                        while (writeQueue.TryDequeue(out var json))
                        {
                            sw.WriteLine(json);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[MemoryWriteQueue] Write error: {ex.Message}");
                }
                finally
                {
                    isWriting = false;
                }
            });
        }
    }

    public static void Enqueue(string json)
    {
        if (!string.IsNullOrWhiteSpace(json))
            writeQueue.Enqueue(json);
    }
}

