using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class ArTusShapeProfile
{
    public string shapeId;
    public string displayName;
    public string category;
    public string archetype;
    public string symbolicMeaning;

    [Range(0f, 1f)] public float stability = 0.5f;
    [Range(0f, 1f)] public float complexity = 0.5f;
    [Range(0f, 1f)] public float confidence = 0.5f;

    public Vector3 baseScale = Vector3.one;
    public Vector3 stretchAxisWeights = Vector3.one;

    public float pulseStrength = 0.2f;
    public float rippleStrength = 0.2f;
    public float orbitStrength = 0.2f;
    public float twistStrength = 0.2f;
    public float taperStrength = 0.2f;

    public float emotionalAffinityCuriosity = 0.5f;
    public float emotionalAffinityThinking = 0.5f;
    public float emotionalAffinityConflict = 0.5f;
    public float emotionalAffinityCalm = 0.5f;
    public float emotionalAffinityJoy = 0.5f;

    public bool isKnowledgeDerived = false;
    public string learnedTopic;
    public string learnedDomain;
    public string sourceKnowledgeId;
    public string verificationState;
    public List<string> sourceTags = new();

    public int timesLearned = 0;
    public int successfulReproductions = 0;
    public float reconstructionScore = 0f;
}
