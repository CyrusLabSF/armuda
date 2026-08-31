using UnityEngine;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System;

public class ArTusCategoryNavigator : MonoBehaviour
{
    public string univercityRootPath = "D:/ArTusCloud-Deployment/UNIVERcity";
    private string exportPath = "D:/ArTusCloud-Deployment/UNIVERcity/Structure/category_map.json";

    private ArTusSpeechResponder speech;
    private ArTusCuriosityEngine curiosityEngine;
    private ArTusCoreState core;

    [System.Serializable]
    public class CategoryNode
    {
        public string name;
        public List<string> subtopics = new();
    }

    [System.Serializable]
    public class CategoryMapWrapper
    {
        public List<CategoryNode> categories = new();
        public string timestamp;
    }

    void Awake()
    {
        speech = GetComponent<ArTusSpeechResponder>();
        curiosityEngine = GetComponent<ArTusCuriosityEngine>();
        core = GetComponent<ArTusCoreState>();
    }

    public List<CategoryNode> GetAvailableCategories()
    {
        var list = new List<CategoryNode>();
        var root = new DirectoryInfo(univercityRootPath);
        if (!root.Exists) return list;

        foreach (var dir in root.GetDirectories())
        {
            var node = new CategoryNode { name = dir.Name };
            foreach (var file in dir.GetFiles("*.json"))
            {
                node.subtopics.Add(Path.GetFileNameWithoutExtension(file.Name));
            }
            list.Add(node);
        }

        return list;
    }

    public void SpeakCategoryOverview()
    {
        var categories = GetAvailableCategories();
        if (categories.Count == 0)
        {
            speech?.Speak("I don’t see any categories to explore yet.");
            return;
        }

        foreach (var node in categories)
        {
            speech?.Speak($"In {node.name}, I found {node.subtopics.Count} topics.");
        }

        ExportCategoryMap(categories);
    }

    public void ExportCategoryMap(List<CategoryNode> map)
    {
        var wrapper = new CategoryMapWrapper
        {
            categories = map,
            timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        };

        string json = JsonUtility.ToJson(wrapper, true);
        Directory.CreateDirectory(Path.GetDirectoryName(exportPath));
        File.WriteAllText(exportPath, json);

        Debug.Log($"[CategoryNavigator] Category map exported to {exportPath}");
    }

    public string GetRandomCuriousTopic()
    {
        var all = GetAvailableCategories();
        var unexplored = new List<(string category, string topic)>();

        foreach (var node in all)
        {
            foreach (string sub in node.subtopics)
            {
                bool alreadyKnown = core.memoryLog.Any(m =>
                    m.content.ToLower().Contains(sub.ToLower()) ||
                    m.category.ToLower().Contains(node.name.ToLower()));

                if (!alreadyKnown)
                    unexplored.Add((node.name, sub));
            }
        }

        if (unexplored.Count == 0)
        {
            speech?.Speak("I’ve already explored most topics. New resources may be required.");
            return null;
        }

        var selected = unexplored[UnityEngine.Random.Range(0, unexplored.Count)];
        string trailID = $"Trail_{selected.category}_{selected.topic}";

        core?.LogMemory($"🌱 Curiosity initiated on {selected.topic} in {selected.category}", "Exploration", 2, "curious", trailID);
        curiosityEngine?.AddTopic(selected.topic, selected.category);

        return Path.Combine(univercityRootPath, selected.category, selected.topic + ".json");
    }
}
