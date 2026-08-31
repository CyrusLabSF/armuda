using System.IO;
using UnityEngine;

/// <summary>
/// Centralized, platform-safe path resolver for ArTus.
/// - Persistent: writable at runtime (WebGL safe)
/// - Streaming: read-only packaged data
/// - Dev: editor-only absolute paths (never required at runtime)
/// </summary>
public static class ArTusPathUtility
{
    private static readonly string[] StandardPersistentDirectories =
    {
        "UNIVERcity",
        "UNIVERcity/Configs",
        "UNIVERcity/Knowledge",
        "UNIVERcity/Knowledge/External",
        "UNIVERcity/Knowledge/ConceptDiscovery",
        "UNIVERcity/Knowledge/ShapeKnowledge",
        "UNIVERcity/Knowledge/ShapeDescriptors",
        "UNIVERcity/Knowledge/ShapeDescriptorImports",
        "UNIVERcity/Knowledge/GeometryLibraryImports",
        "UNIVERcity/Verification",
        "UNIVERcity/Exports",
        "UNIVERcity/Exports/PowerBI",
        "UNIVERcity/Exports/BinaryAssets",
        "UNIVERcity/Staging",
        "UNIVERcity/Staging/API_Requests",
        "UNIVERcity/Queues",
        "UNIVERcity/Resource",
        "UNIVERcity/System",
        "UNIVERcity/Logs",
        "UNIVERcity/ThoughtPaths",
        "UNIVERcity/Stimuli",
        "UNIVERcity/Library",
        "UNIVERcity/UserPatterns",
        "UNIVERcity/Trails",
        "Ingestion",
        "IngestedTopics",
        "Logs",
        "PowerBI"
    };

    /// <summary>
    /// Writable, platform-safe path.
    /// Use for logs, memory, beliefs, exports, CVE data, etc.
    /// </summary>
    public static string GetPersistent(string relative)
    {
        return Path.Combine(Application.persistentDataPath, relative);
    }

    /// <summary>
    /// Read-only packaged data.
    /// Use for seed files, defaults, templates.
    /// </summary>
    public static string GetStreaming(string relative)
    {
        return Path.Combine(Application.streamingAssetsPath, relative);
    }

    /// <summary>
    /// Development-only absolute path.
    /// NEVER required at runtime.
    /// Automatically falls back to persistent path outside editor/standalone.
    /// </summary>
    public static string GetDev(string relative)
    {
#if UNITY_EDITOR || UNITY_STANDALONE
        return Path.Combine("D:/ArTusCloud-Deployment", relative);
#else
        return GetPersistent(relative);
#endif
    }

    /// <summary>
    /// Backwards-compatible alias.
    /// Exists to avoid breaking older scripts.
    /// </summary>
    public static string GetSafePath(string relative)
    {
        return GetPersistent(relative);
    }

    public static string EnsurePersistentDirectory(string relative)
    {
        string path = GetPersistent(relative);
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);
        return path;
    }

    public static string EnsureParentDirectory(string path)
    {
        string dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        return path;
    }

    public static void EnsureStandardRuntimeFolders()
    {
        foreach (string relative in StandardPersistentDirectories)
            EnsurePersistentDirectory(relative);

        string ingestionQueue = EnsureParentDirectory(GetPersistent("Ingestion/topics.txt"));
        if (!File.Exists(ingestionQueue))
            File.WriteAllText(ingestionQueue, string.Empty);

        string unansweredQuestions = EnsureParentDirectory(
            GetPersistent("UNIVERcity/UserPatterns/UnansweredQuestions.json")
        );
        if (!File.Exists(unansweredQuestions))
            File.WriteAllText(unansweredQuestions, string.Empty);

        string learningTrails = EnsureParentDirectory(
            GetPersistent("UNIVERcity/Trails/LearningTrails.json")
        );
        if (!File.Exists(learningTrails))
            File.WriteAllText(learningTrails, string.Empty);
    }
}
