using System;

[Serializable]
public class ThoughtPathNode
{
    public string belief;                 // The belief this path supports
    public string originTrail;           // Name of the trail or source
    public string supportingMemory;      // Raw memory entry (cleaned)
    public string emotion;               // Emotion felt at this node
    public float confidence;             // Confidence in this single step
    public string stepType;              // "Reflection", "Correction", "ContradictionCheck", etc.
    public string symbolicCategory;      // "conflict", "growth", "curiosity", "alignment", etc.
    public float importanceScore;        // Weighted importance (0.0–1.0) of this node
    public bool contradictionFlag;       // True if this step introduced internal conflict
    public bool simulated;               // Flag if this came from a sandbox simulation (ignored in Unity)
    public string visualPulseID;         // Optional visual reference (used in Armuda only)
    public string timestamp;             // When this thought occurred
    public float ageInSeconds;           // Age since created (optional real-time tracking)
    public bool flaggedForDeletion;      // For pruning or memory cleanup
    public bool isDormant;               // Temporarily inactive — e.g., frozen trail node

    // 🧠 Constructor
    public ThoughtPathNode(
        string belief,
        string trail,
        string memory,
        string emotion,
        float confidence,
        string stepType = "Reflection",
        string symbolicCategory = "growth",
        float importanceScore = 0.5f,
        bool contradictionFlag = false,
        bool simulated = false,
        string visualPulseID = ""
    )
    {
        this.belief = belief;
        this.originTrail = trail;
        this.supportingMemory = memory;
        this.emotion = emotion;
        this.confidence = confidence;
        this.stepType = stepType;
        this.symbolicCategory = symbolicCategory;
        this.importanceScore = importanceScore;
        this.contradictionFlag = contradictionFlag;
        this.simulated = simulated;
        this.visualPulseID = visualPulseID;
        this.timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        this.ageInSeconds = 0f;
        this.flaggedForDeletion = false;
        this.isDormant = false;
    }

    // 🔄 Optional: lightweight text summary
    public string GetSummary()
    {
        return $"[{timestamp}] Step in '{belief}' via '{originTrail}' – {stepType}, Emotion: {emotion}, Conf: {confidence:F2}";
    }
}
