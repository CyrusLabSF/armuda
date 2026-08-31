using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class DirectoryAccessManager : MonoBehaviour
{
    public string univercityRoot = "D:/ArTusCloud-Deployment/UNIVERcity";
    public Dictionary<string, string> domainPaths = new();
    public List<string> missingDirectories = new();

    void Awake()
    {
        ScanAndGrantAccess();
    }

    public void ScanAndGrantAccess()
    {
        if (!Directory.Exists(univercityRoot))
        {
            Debug.LogError("[DirectoryAccessManager] UNIVERcity root folder is missing.");
            return;
        }

        string[] subDirs = Directory.GetDirectories(univercityRoot);
        domainPaths.Clear();
        missingDirectories.Clear();

        foreach (string dir in subDirs)
        {
            string name = Path.GetFileName(dir);
            if (!domainPaths.ContainsKey(name))
                domainPaths.Add(name.ToLower(), dir);
        }

        // List of required domains for ArTus to function at Hi-Class
        string[] required = new string[]
        {
            "Beliefs", "Culture", "Defense", "Domains", "Emotion", "Exports", "General",
            "Identity", "Logs", "Memory", "Metaphysics", "Philosophy", "Reflection",
            "ResourceQueue", "SandboxLogs", "Science", "Security", "SessionMemory",
            "Stimuli", "System", "Teaching", "Technology", "ThoughtPaths", "Trails", "UserPatterns"
        };

        foreach (string requiredName in required)
        {
            string fullPath = Path.Combine(univercityRoot, requiredName);
            if (!Directory.Exists(fullPath))
            {
                Debug.LogWarning($"[DirectoryAccessManager] Creating missing folder: {requiredName}");
                Directory.CreateDirectory(fullPath);
                missingDirectories.Add(requiredName);
            }
            if (!domainPaths.ContainsKey(requiredName.ToLower()))
            {
                domainPaths[requiredName.ToLower()] = fullPath;
            }
        }

        Debug.Log($"[DirectoryAccessManager] Access granted to {domainPaths.Count} domains.");
    }

    public void EnsureTopicDirectory(string topic, string domain)
    {
        string basePath = "D:/ArTusCloud-Deployment/UNIVERcity/";
        string domainPath = Path.Combine(basePath, domain);
        string topicPath = Path.Combine(domainPath, topic);

        if (!Directory.Exists(topicPath))
        {
            Directory.CreateDirectory(topicPath);
            Debug.Log($"[DirectoryAccess] Created topic directory: {topicPath}");
        }
        else
        {
            Debug.Log($"[DirectoryAccess] Topic directory already exists: {topicPath}");
        }
    }

    public string GetPathForDomain(string domain)
    {
        string key = domain.ToLower();
        return domainPaths.ContainsKey(key) ? domainPaths[key] : null;
    }

    public bool IsDomainAccessible(string domain)
    {
        return domainPaths.ContainsKey(domain.ToLower());
    }

    public List<string> GetAllDomains()
    {
        return new List<string>(domainPaths.Keys);
    }
}
