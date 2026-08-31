using UnityEngine;
using System.IO;
using System.Collections.Generic;

public class ReflectOnExternalLearnings : MonoBehaviour
{
    [Header("External Learning Settings")]
    [SerializeField] private string externalFolderRelative = "UNIVERcity/ExternalLearnings";
    [SerializeField] private bool verboseLogs = false;

    private string externalFolderPath;
    private ArTusCoreState core;

    void Awake()
    {
        core = GetComponent<ArTusCoreState>();

        // ✅ Resolve paths ONLY at runtime (WebGL-safe)
        externalFolderPath = ArTusPathUtility.GetPersistent(externalFolderRelative);

        if (!Directory.Exists(externalFolderPath))
            Directory.CreateDirectory(externalFolderPath);
    }

    /// <summary>
    /// Scan external learning artifacts and reflect on them.
    /// </summary>
    public void Reflect()
    {
        if (!Directory.Exists(externalFolderPath))
            return;

        string[] files = Directory.GetFiles(externalFolderPath, "*.json");

        if (files.Length == 0)
        {
            if (verboseLogs)
                Debug.Log("[ExternalLearnings] No external learnings found.");
            return;
        }

        foreach (string filePath in files)
        {
            TryReflectOnFile(filePath);
        }
    }

    private void TryReflectOnFile(string filePath)
    {
        try
        {
            string json = File.ReadAllText(filePath);
            if (string.IsNullOrWhiteSpace(json))
                return;

            string fileName = Path.GetFileNameWithoutExtension(filePath);

            core?.LogMemory(
                $"Reflected on external learning: {fileName}",
                "ExternalLearning",
                3,
                "curious"
            );

            if (verboseLogs)
                Debug.Log($"[ExternalLearnings] Reflected: {fileName}");
        }
        catch (IOException ex)
        {
            Debug.LogWarning($"[ExternalLearnings] Failed to read {filePath}: {ex.Message}");
        }
    }
}
