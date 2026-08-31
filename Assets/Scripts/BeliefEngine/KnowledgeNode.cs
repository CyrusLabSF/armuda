using System;
using System.Collections.Generic;

[System.Serializable]
public class KnowledgeNode
{
    // 🔹 Core fields
    public string title;
    public string summary;
    public string category;
    public float confidence;
    public int importance;
    public string emotion;
    public string extract;

    // 🔹 Collections (Unity/JsonUtility friendly)
    public List<string> related_beliefs;
    public List<string> tags;

    // 🔹 Metadata
    public string origin;       // e.g., "VettedHub", "IoT", "Manual"
    public string sourceFile;   // Path to where it was saved
    public string last_updated;

    // 🔹 Constructor ensures safe defaults
    public KnowledgeNode()
    {
        title = "";
        summary = "";
        category = "general";
        confidence = 0.5f;
        importance = 1;
        emotion = "neutral";
        extract = "";

        related_beliefs = new List<string>();
        tags = new List<string>();

        origin = "unspecified";
        sourceFile = "";
        last_updated = DateTime.Now.ToString("s");
    }
}
