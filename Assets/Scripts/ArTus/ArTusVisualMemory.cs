using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Hi-Class Visual Memory
/// Passive visual store: persists and recalls images by topic
/// No memory logging, no emotion inference, no belief mutation
/// </summary>
public class ArTusVisualMemory : MonoBehaviour
{
    private readonly Dictionary<string, Texture2D> visualMemory = new();
    private readonly HashSet<string> knownImageHashes = new();

    private string memoryPath;

    // --------------------------------------------------
    // UNITY LIFECYCLE
    // --------------------------------------------------
    void Awake()
    {
        memoryPath = Path.Combine(Application.persistentDataPath, "VisualMemory");
        Directory.CreateDirectory(memoryPath);

        LoadVisualMemoryFromDisk();
    }

    // --------------------------------------------------
    // PUBLIC API (PASSIVE)
    // --------------------------------------------------
    public void StoreVisual(string topic, Texture2D image, string source = "direct")
    {
        if (image == null || string.IsNullOrWhiteSpace(topic))
        {
            Debug.LogWarning("[VisualMemory] Cannot store null image or empty topic.");
            return;
        }

        string imageHash = HashImage(image);
        if (knownImageHashes.Contains(imageHash))
            return;

        visualMemory[topic] = image;
        knownImageHashes.Add(imageHash);

        try
        {
            byte[] bytes = image.EncodeToPNG();
            File.WriteAllBytes(Path.Combine(memoryPath, topic + ".png"), bytes);

            SaveVisualMetadata(topic, source);
        }
        catch (IOException ex)
        {
            Debug.LogWarning($"[VisualMemory] Failed to write image: {ex.Message}");
        }
    }

    public Texture2D RecallVisual(string topic)
    {
        if (visualMemory.TryGetValue(topic, out var tex))
            return tex;

        return null;
    }

    public List<string> GetAllVisualTopics()
    {
        return visualMemory.Keys.ToList();
    }

    // --------------------------------------------------
    // LOAD
    // --------------------------------------------------
    private void LoadVisualMemoryFromDisk()
    {
        string[] files = Directory.GetFiles(memoryPath, "*.png");

        foreach (string file in files)
        {
            try
            {
                byte[] bytes = File.ReadAllBytes(file);
                Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);

                if (!tex.LoadImage(bytes))
                    continue;

                string topic = Path.GetFileNameWithoutExtension(file);
                string hash = HashImage(tex);

                if (!visualMemory.ContainsKey(topic))
                {
                    visualMemory[topic] = tex;
                    knownImageHashes.Add(hash);
                }
            }
            catch
            {
                // Corrupt or unreadable file — skip silently
            }
        }
    }

    // --------------------------------------------------
    // METADATA (PASSIVE)
    // --------------------------------------------------
    private void SaveVisualMetadata(string topic, string source)
    {
        try
        {
            string json = JsonUtility.ToJson(new VisualMemoryMetadata
            {
                topic = topic,
                timestamp = DateTime.UtcNow.ToString("o"),
                source = source
            }, true);

            File.WriteAllText(Path.Combine(memoryPath, topic + ".json"), json);
        }
        catch
        {
            // Metadata failure should never block storage
        }
    }

    // --------------------------------------------------
    // UTIL
    // --------------------------------------------------
    private string HashImage(Texture2D tex)
    {
        byte[] raw = tex.EncodeToPNG();
        using var md5 = System.Security.Cryptography.MD5.Create();
        byte[] hash = md5.ComputeHash(raw);
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }

    // --------------------------------------------------
    // DATA
    // --------------------------------------------------
    [Serializable]
    private class VisualMemoryMetadata
    {
        public string topic;
        public string timestamp;
        public string source;
    }
}
