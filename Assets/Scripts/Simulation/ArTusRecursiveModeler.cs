using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;

public class ArTusRecursiveModeler : MonoBehaviour
{
    private ArTusCoreState core;
    private ArTusSpeechResponder speech;

    [Header("Mode")]
    public bool betaMode = true;
    public bool allowModeling = true;

    [Header("Limits")]
    public int maxDepth = 2; // reduced for beta
    public float modelingCooldown = 10f;

    private float lastModelTime;

    private string outputPath;
    private string csvPath;

    [Serializable]
    public class RecursiveNode
    {
        public string concept;
        public string parentConcept;
        public string generatedHypothesis;
        public string emotion;
        public string timestamp;
    }

    private readonly List<RecursiveNode> modelTrail = new();

    void Awake()
    {
        core = GetComponent<ArTusCoreState>();
        speech = GetComponent<ArTusSpeechResponder>();

        outputPath = ArTusPathUtility.GetPersistent("UNIVERcity/RecursiveModels");
        csvPath = ArTusPathUtility.GetPersistent("UNIVERcity/Logs/RecursiveModelLog.csv");

        InitializeStorage();
    }

    // ------------------------------------------------------
    // INIT STORAGE (FIXED)
    // ------------------------------------------------------
    private void InitializeStorage()
    {
        try
        {
            if (!Directory.Exists(outputPath))
                Directory.CreateDirectory(outputPath);

            string csvDir = Path.GetDirectoryName(csvPath);

            if (!string.IsNullOrEmpty(csvDir) && !Directory.Exists(csvDir))
                Directory.CreateDirectory(csvDir);

            if (!File.Exists(csvPath))
            {
                File.WriteAllText(
                    csvPath,
                    "Timestamp,Concept,Parent,Hypothesis,Emotion\n"
                );
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[RecursiveModeler] Init failed: {ex.Message}");
        }
    }

    // ------------------------------------------------------
    // ENTRY (SAFE)
    // ------------------------------------------------------
    public void BeginRecursiveModel(string rootConcept)
    {
        if (!allowModeling)
            return;

        if (Time.time - lastModelTime < modelingCooldown)
            return;

        if (string.IsNullOrWhiteSpace(rootConcept))
            return;

        lastModelTime = Time.time;

        modelTrail.Clear();

        RunNestedModel(rootConcept, null, 0);

        SaveRecursiveTrail(rootConcept);
        ExportTrailToCSV();
    }

    // ------------------------------------------------------
    // CORE LOGIC (CONTROLLED)
    // ------------------------------------------------------
    private void RunNestedModel(string concept, string parent, int depth)
    {
        if (depth > maxDepth)
            return;

        if (modelTrail.Exists(n =>
            n.concept == concept &&
            n.parentConcept == (parent ?? "none")))
        {
            return;
        }

        string hypothesis = GenerateHypothesis(concept);
        string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        modelTrail.Add(new RecursiveNode
        {
            concept = concept,
            parentConcept = parent ?? "none",
            generatedHypothesis = hypothesis,
            emotion = "curious",
            timestamp = timestamp
        });

        // -----------------------------
        // SAFE MEMORY LOGGING
        // -----------------------------
        if (!betaMode)
        {
            core?.LogMemory(
                $"🔂 Depth {depth}: Modeled '{concept}'",
                "RecursiveDepth",
                1,
                "curious"
            );
        }

        // -----------------------------
        // SAFE SPEECH (DISABLED IN BETA)
        // -----------------------------
        if (!betaMode && depth == 0)
        {
            speech?.TriggerVoice(
                $"Beginning recursive modeling of {concept}."
            );
        }

        // -----------------------------
        // CONTROLLED BRANCHING
        // -----------------------------
        string lower = concept.ToLowerInvariant();

        if (lower.Contains("society"))
        {
            RunNestedModel("cultural behavior", concept, depth + 1);
            RunNestedModel("emotional memory", concept, depth + 1);
        }
        else if (lower.Contains("language"))
        {
            RunNestedModel("speech patterns", concept, depth + 1);
            RunNestedModel("memory encoding", concept, depth + 1);
        }
        else if (lower.Contains("belief"))
        {
            RunNestedModel("bias", concept, depth + 1);
            RunNestedModel("contradiction resolution", concept, depth + 1);
        }
    }

    // ------------------------------------------------------
    // OUTPUT
    // ------------------------------------------------------
    private string GenerateHypothesis(string concept)
    {
        return
            $"If '{concept}' is processed recursively, it may reveal links to belief, memory, or emotional structure.";
    }

    private void SaveRecursiveTrail(string root)
    {
        try
        {
            string safeRoot = root.Replace(" ", "_");
            string filename =
                $"RecursiveModel_{safeRoot}_{DateTime.Now:yyyyMMdd_HHmmss}.json";

            string json = JsonUtility.ToJson(
                new RecursiveModelWrapper
                {
                    root = root,
                    nodes = modelTrail
                },
                true
            );

            File.WriteAllText(
                Path.Combine(outputPath, filename),
                json
            );
        }
        catch (Exception ex)
        {
            Debug.LogError($"[RecursiveModel] Save failed: {ex.Message}");
        }
    }

    private void ExportTrailToCSV()
    {
        try
        {
            foreach (var node in modelTrail)
            {
                string line =
                    $"{node.timestamp}," +
                    $"{node.concept}," +
                    $"{node.parentConcept}," +
                    $"{node.generatedHypothesis.Replace(",", ";")}," +
                    $"{node.emotion}\n";

                File.AppendAllText(csvPath, line);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[RecursiveModel] CSV export failed: {ex.Message}");
        }
    }

    // ------------------------------------------------------
    // ACCESS
    // ------------------------------------------------------
    public List<RecursiveNode> GetTrail()
    {
        return modelTrail;
    }

    // ------------------------------------------------------
    // INTERNAL WRAPPER
    // ------------------------------------------------------
    [Serializable]
    private class RecursiveModelWrapper
    {
        public string root;
        public List<RecursiveNode> nodes;
    }
}