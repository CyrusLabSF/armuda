using System.Collections.Generic;
using System.IO;
using UnityEngine;
using System;

[System.Serializable]
public class KnowledgeCategory
{
    public string name;
    public List<string> subtopics;
}

public class UNIVERcityTaxonomyBuilder : MonoBehaviour
{
    public string basePath = "D:/ArTusCloud-Deployment/UNIVERcity";
    public string summaryPath = "D:/ArTusCloud-Deployment/UNIVERcity/Logs/TaxonomySummary.csv";

    public List<KnowledgeCategory> categories = new()
    {
        new KnowledgeCategory
        {
            name = "Neuroscience",
            subtopics = new List<string> { "Neural Plasticity", "Cognition", "Synaptic Growth" }
        },
        new KnowledgeCategory
        {
            name = "Psychology",
            subtopics = new List<string> { "Habit Formation", "Emotion Regulation" }
        },
        new KnowledgeCategory
        {
            name = "Astronomy",
            subtopics = new List<string> { "Dark Matter", "Exoplanets" }
        }
    };

    private ArTusCoreState core;

    void Start()
    {
        core = GetComponent<ArTusCoreState>();

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(summaryPath));
            if (!File.Exists(summaryPath))
                File.WriteAllText(summaryPath, "Timestamp,Category,Subtopic,Path\n");
        }
        catch (IOException ex)
        {
            Debug.LogError($"[TaxonomyBuilder] Failed to prepare summary log: {ex.Message}");
        }
    }

    [ContextMenu("Generate Taxonomy")]
    public void GenerateTaxonomy()
    {
        foreach (var category in categories)
        {
            string categoryPath = Path.Combine(basePath, category.name);

            try
            {
                Directory.CreateDirectory(categoryPath);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[TaxonomyBuilder] Failed to create category folder: {ex.Message}");
                continue;
            }

            foreach (var subtopic in category.subtopics)
            {
                string filePath = Path.Combine(categoryPath, subtopic + ".json");

                if (!File.Exists(filePath))
                {
                    var node = new KnowledgeNode
                    {
                        title = subtopic,
                        category = category.name,
                        tags = new(),
                        emotion = "neutral",
                        confidence = 0f,
                        importance = 1,
                        related_beliefs = new(),
                        last_updated = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                    };

                    try
                    {
                        string json = JsonUtility.ToJson(node, true);
                        File.WriteAllText(filePath, json);
                        File.AppendAllText(summaryPath,
                            $"{DateTime.Now},{category.name},{subtopic},{filePath}\n");

                        core?.LogMemory(
                            $"📚 Created knowledge node: {subtopic} in {category.name}",
                            "TaxonomyBuild", 1, "neutral", category.name);

                        Debug.Log($"[UNIVERcity] Created: {filePath}");
                    }
                    catch (IOException ex)
                    {
                        Debug.LogError($"[TaxonomyBuilder] Failed to write node: {subtopic}: {ex.Message}");
                    }
                }
            }
        }

        core?.TriggerVoice("UNIVERcity taxonomy has been generated.");
    }
}
