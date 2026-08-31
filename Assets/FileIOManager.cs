using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading;
using UnityEngine;

/// <summary>
/// Unified FileIO system for ArTus.
/// Contains FileIOManager, FileIOHelper, and FileSystemHelper.
/// </summary>
public class FileIOManager : MonoBehaviour
{
    private class FileWriteTask
    {
        public string path;
        public string content;
        public bool append;
        public bool compress;
        public float delaySeconds;
        public string originTag;
        public int retriesLeft = 3;
        public Action onComplete;
        public DateTime scheduledTime;
        public WritePriority priority = WritePriority.Normal;
    }

    public enum WritePriority { Low = 0, Normal = 1, High = 2 }

    private static FileIOManager instance;
    public static FileIOManager Instance => instance;
    public static bool IsReady => instance != null;

    private readonly ConcurrentQueue<FileWriteTask> writeQueue = new();
    private readonly List<FileWriteTask> delayQueue = new();
    private static readonly Queue<FileWriteTask> preInitQueue = new();
    private Thread writerThread;
    private volatile bool running = true;

    private int totalWrites = 0;
    private int failedWrites = 0;

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        // Flush any queued writes before init
        lock (preInitQueue)
        {
            while (preInitQueue.Count > 0)
                writeQueue.Enqueue(preInitQueue.Dequeue());
        }

        writerThread = new Thread(WriterLoop) { IsBackground = true };
        writerThread.Start();

        if (Application.isPlaying)
            Debug.Log("[FileIOManager] ✅ Initialized and running.");
    }

    public static void QueueWrite(
        string path,
        string content,
        string originTag = "unspecified",
        bool append = false,
        bool compress = false,
        float delaySeconds = 0f,
        Action onComplete = null,
        WritePriority priority = WritePriority.Normal)
    {
        EnqueueWrite(path, content, append, compress, delaySeconds, originTag, onComplete, priority);
    }

    public static void EnqueueWrite(
        string path,
        string content,
        bool append = false,
        bool compress = false,
        float delaySeconds = 0f,
        string originTag = "unspecified",
        Action onComplete = null,
        WritePriority priority = WritePriority.Normal)
    {
        var task = new FileWriteTask
        {
            path = path,
            content = content,
            append = append,
            compress = compress,
            delaySeconds = delaySeconds,
            originTag = originTag,
            onComplete = onComplete,
            scheduledTime = DateTime.UtcNow.AddSeconds(delaySeconds),
            priority = priority
        };

        if (!IsReady)
        {
            lock (preInitQueue) preInitQueue.Enqueue(task);
            return;
        }

        if (delaySeconds > 0f)
        {
            lock (instance.delayQueue)
                instance.delayQueue.Add(task);
        }
        else
        {
            instance.writeQueue.Enqueue(task);
        }
    }

    private FileWriteTask DequeueHighestPriority()
    {
        FileWriteTask selected = null;
        var tempList = new List<FileWriteTask>();

        while (writeQueue.TryDequeue(out var t))
            tempList.Add(t);

        if (tempList.Count == 0) return null;

        tempList.Sort((a, b) => b.priority.CompareTo(a.priority));
        selected = tempList[0];

        for (int i = 1; i < tempList.Count; i++)
            writeQueue.Enqueue(tempList[i]);

        return selected;
    }

    private void WriterLoop()
    {
        while (running)
        {
            // Promote delayed writes
            lock (delayQueue)
            {
                DateTime now = DateTime.UtcNow;
                for (int i = delayQueue.Count - 1; i >= 0; i--)
                {
                    if (delayQueue[i].scheduledTime <= now)
                    {
                        writeQueue.Enqueue(delayQueue[i]);
                        delayQueue.RemoveAt(i);
                    }
                }
            }

            var task = DequeueHighestPriority();
            if (task != null)
            {
                try
                {
                    string dir = Path.GetDirectoryName(task.path);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                        Directory.CreateDirectory(dir);

                    if (task.compress)
                    {
                        using var fs = new FileStream(task.path + ".gz", FileMode.Create, FileAccess.Write);
                        using var gz = new GZipStream(fs, CompressionMode.Compress);
                        using var writer = new StreamWriter(gz);
                        writer.Write(task.content);
                    }
                    else if (task.append)
                    {
                        File.AppendAllText(task.path, task.content);
                    }
                    else
                    {
                        File.WriteAllText(task.path, task.content);
                    }

                    totalWrites++;
                    task.onComplete?.Invoke();
                }
                catch (IOException ioEx)
                {
                    if (task.retriesLeft-- > 0)
                    {
                        Thread.Sleep(30);
                        writeQueue.Enqueue(task);
                    }
                    else
                    {
                        failedWrites++;
                        Debug.LogError($"[FileIOManager] IO Error ({task.originTag}) ❌: {ioEx.Message}");
                    }
                }
                catch (Exception ex)
                {
                    failedWrites++;
                    Debug.LogError($"[FileIOManager] Write failed ({task.originTag}) ❌: {ex.Message}");
                }
            }
            else
            {
                Thread.Sleep(5);
            }
        }
    }

    private void OnApplicationQuit()
    {
        running = false;
        Debug.Log("[FileIOManager] 🛑 Flushing pending writes...");
        if (writerThread != null && writerThread.IsAlive)
            writerThread.Join(500);
    }

    public static int GetPendingWriteCount() => instance?.writeQueue.Count ?? 0;
    public static int GetTotalWrites() => instance?.totalWrites ?? 0;
    public static int GetFailedWrites() => instance?.failedWrites ?? 0;
}

/// <summary>
/// Helper wrapper for categorized saving into UNIVERcity.
/// </summary>
public static class FileIOHelper
{
    private static readonly string basePath = "D:/ArTusCloud-Deployment/UNIVERcity";

    public static void SaveJson(
        string category, string fileName, string jsonContent,
        bool append = false, bool compress = false, float delay = 0f,
        Action onComplete = null, FileIOManager.WritePriority priority = FileIOManager.WritePriority.Normal,
        bool prettyPrint = false)
    {
        if (prettyPrint)
        {
            try
            {
                jsonContent = JsonUtility.ToJson(JsonUtility.FromJson<object>(jsonContent), true);
            }
            catch { }
        }

        string path = BuildPath(category, fileName, ".json");
        FileIOManager.EnqueueWrite(path, jsonContent, append, compress, delay, category, onComplete, priority);
    }

    public static void SaveText(
        string category, string fileName, string content,
        bool append = false, bool compress = false, float delay = 0f,
        Action onComplete = null, FileIOManager.WritePriority priority = FileIOManager.WritePriority.Normal)
    {
        string path = BuildPath(category, fileName, ".txt");
        FileIOManager.EnqueueWrite(path, content, append, compress, delay, category, onComplete, priority);
    }

    public static void SaveCSV(
        string category, string fileName, string csvContent,
        bool append = false, float delay = 0f,
        Action onComplete = null, FileIOManager.WritePriority priority = FileIOManager.WritePriority.Normal,
        string header = null)
    {
        string path = BuildPath(category, fileName, ".csv");
        if (!append && header != null && !File.Exists(path))
            csvContent = header + "\n" + csvContent;

        FileIOManager.EnqueueWrite(path, csvContent, append, false, delay, category, onComplete, priority);
    }

    private static string BuildPath(string category, string fileName, string extension)
    {
        if (!fileName.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            fileName += extension;

        return Path.Combine(basePath, category, fileName);
    }
}

/// <summary>
/// File system helper for safe deletes, moves, and fixes.
/// </summary>
public static class FileSystemHelper
{
    public static void DirectoryMove(string sourceDirectoryPath, string destDirectoryPath)
    {
        try
        {
            if (!Directory.Exists(destDirectoryPath))
            {
                Directory.Move(sourceDirectoryPath, destDirectoryPath);
                return;
            }

            foreach (var file in Directory.EnumerateFiles(sourceDirectoryPath))
            {
                var newFilePath = Path.Combine(destDirectoryPath, Path.GetFileName(file));
                try
                {
                    if (File.Exists(newFilePath)) File.Delete(newFilePath);
                    File.Move(file, newFilePath);
                }
                catch (Exception e)
                {
                    FileIOManager.QueueWrite("Logs/SystemOps_Errors.txt",
                        $"Move failed {file} → {newFilePath}: {e}\n", "FileSystem", append: true);
                }
            }

            foreach (var directory in Directory.EnumerateDirectories(sourceDirectoryPath))
            {
                var newDirectoryPath = Path.Combine(destDirectoryPath, Path.GetFileName(directory));
                try
                {
                    if (Directory.Exists(newDirectoryPath)) Directory.Delete(newDirectoryPath, true);
                    Directory.Move(directory, newDirectoryPath);
                }
                catch (Exception e)
                {
                    FileIOManager.QueueWrite("Logs/SystemOps_Errors.txt",
                        $"Move failed {directory} → {newDirectoryPath}: {e}\n", "FileSystem", append: true);
                }
            }
        }
        catch (Exception ex)
        {
            FileIOManager.QueueWrite("Logs/SystemOps_Errors.txt",
                $"DirectoryMove failed {sourceDirectoryPath} → {destDirectoryPath}: {ex}\n", "FileSystem", append: true);
        }
    }

    public static void SafeDeleteDirectory(string directoryPath, string origin = "unspecified")
    {
        try
        {
            if (!Directory.Exists(directoryPath)) return;
            Directory.Delete(directoryPath, true);
            FileIOManager.QueueWrite("Logs/SystemOps.txt",
                $"Deleted {directoryPath} at {DateTime.UtcNow}\n", origin, append: true);
        }
        catch (Exception ex)
        {
            FileIOManager.QueueWrite("Logs/SystemOps_Errors.txt",
                $"Failed delete {directoryPath}: {ex}\n", origin, append: true);
        }
    }

    public static void DeleteFile(string filePath, string origin = "unspecified")
    {
        try
        {
            if (!File.Exists(filePath)) return;
            File.SetAttributes(filePath, FileAttributes.Normal);
            File.Delete(filePath);
            FileIOManager.QueueWrite("Logs/SystemOps.txt",
                $"Deleted file {filePath} at {DateTime.UtcNow}\n", origin, append: true);
        }
        catch (Exception ex)
        {
            FileIOManager.QueueWrite("Logs/SystemOps_Errors.txt",
                $"Failed delete {filePath}: {ex}\n", origin, append: true);
        }
    }

    public static void MoveFile(string sourceFilePath, string destFilePath, string origin = "unspecified")
    {
        try
        {
            if (!File.Exists(sourceFilePath))
                throw new FileNotFoundException($"Source missing: {sourceFilePath}");
            if (File.Exists(destFilePath)) File.Delete(destFilePath);
            File.Move(sourceFilePath, destFilePath);
        }
        catch (Exception ex)
        {
            FileIOManager.QueueWrite("Logs/SystemOps_Errors.txt",
                $"Failed move {sourceFilePath} → {destFilePath}: {ex}\n", origin, append: true);
        }
    }
}
