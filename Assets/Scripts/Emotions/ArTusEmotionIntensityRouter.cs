using UnityEngine;

public class ArTusEmotionIntensityRouter : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private ArTusEpisodicMemory episodicMemory;

    [Header("Emotion Scaling")]
    public float baseIntensity = 1.0f;
    public float confidenceWeight = 0.4f;
    public float clarityWeight = 0.3f;
    public float ageDecay = 0.1f;

    // --------------------------------------------------
    // UNITY LIFECYCLE
    // --------------------------------------------------
    void Awake()
    {
        if (episodicMemory == null)
        {
            episodicMemory = GetComponent<ArTusEpisodicMemory>();

            if (episodicMemory == null)
                Debug.LogWarning("[EmotionRouter] EpisodicMemory not found.");
        }
    }

    // --------------------------------------------------
    // PUBLIC ROUTING (PASSIVE READ)
    // --------------------------------------------------
    public float GetEmotionIntensity()
    {
        if (episodicMemory == null)
            return baseIntensity;

        var lastEvent = episodicMemory.GetLastEvent();

        if (lastEvent == null)
            return baseIntensity;

        float confidenceFactor = lastEvent.averageConfidence * confidenceWeight;
        float clarityFactor = lastEvent.averageClarity * clarityWeight;
        float ageFactor = Mathf.Max(0f, 1f - lastEvent.ageInDays * ageDecay);

        float intensity =
            baseIntensity *
            (1f + confidenceFactor + clarityFactor) *
            ageFactor;

        return Mathf.Clamp(intensity, 0.2f, 3.0f);
    }
}
