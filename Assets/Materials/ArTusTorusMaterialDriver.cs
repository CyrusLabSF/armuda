using UnityEngine;

/// <summary>
/// Hi-Class Torus Material Driver (Emotion-Contract Safe)
/// Animates drip, veins, flow, and emission without
/// depending on EmotionState internals.
/// </summary>
[RequireComponent(typeof(Renderer))]
public class ArTusTorusMaterialDriver : MonoBehaviour
{
    [Header("Core (Optional)")]
    public ArTusCoreState core;
    public ArTusEmotionController emotionController;

    [Header("Shader Property Names")]
    public string emissionProp = "_EmissionIntensity";
    public string flowProp = "_FlowSpeed";
    public string noiseProp = "_NoiseSpeed";
    public string dripProp = "_DripStrength";
    public string veinProp = "_VeinIntensity";

    [Header("Base Ranges")]
    public float baseEmission = 6f;
    public float baseFlow = 0.6f;
    public float baseNoise = 0.25f;
    public float baseDrip = 0.15f;
    public float baseVeins = 2.0f;

    [Header("Influence Weights")]
    public float cognitionInfluence = 1.0f;
    public float emotionPresenceBoost = 0.25f;
    public float surgeInfluence = 1.4f;

    [Header("Drip Threshold")]
    public float dripThreshold = 0.6f;

    private Material mat;
    private float timeSeed;
    private float surgeEnergy;

    void Awake()
    {
        mat = GetComponent<Renderer>().material;
        timeSeed = Random.value * 1000f;
    }

    void Update()
    {
        if (mat == null)
            return;

        // --------------------------------------------------
        // EMOTION PRESENCE (NO STATE INTROSPECTION)
        // --------------------------------------------------

        float emotionPresence = 0f;

        if (emotionController != null)
        {
            // If controller exists and is enabled, emotion is "alive"
            if (emotionController.isActiveAndEnabled)
            {
                emotionPresence = emotionPresenceBoost;
            }
        }

        // --------------------------------------------------
        // COGNITIVE PRESSURE (SAFE SIGNAL)
        // --------------------------------------------------

        float cognitivePressure = 0f;

        if (core != null)
        {
            cognitivePressure = Mathf.Clamp01(
                core.GetConflictBeliefCount() * 0.06f
            );
        }

        // --------------------------------------------------
        // AUTONOMOUS NOISE (LIFE FORCE)
        // --------------------------------------------------

        float noiseSurge =
            Mathf.PerlinNoise(Time.time * 0.25f, timeSeed) * 0.35f;

        float composite =
            cognitivePressure * cognitionInfluence +
            emotionPresence +
            noiseSurge;

        surgeEnergy = Mathf.Lerp(
            surgeEnergy,
            composite,
            Time.deltaTime * 1.5f
        );

        // --------------------------------------------------
        // FLOW + NOISE
        // --------------------------------------------------

        mat.SetFloat(flowProp,
            baseFlow + surgeEnergy * 1.2f);

        mat.SetFloat(noiseProp,
            baseNoise + surgeEnergy * 0.8f);

        // --------------------------------------------------
        // VEINS (MUSCLE)
        // --------------------------------------------------

        float veinPulse =
            baseVeins +
            Mathf.Sin(Time.time * 2.2f) * surgeEnergy * 2.0f;

        mat.SetFloat(veinProp, veinPulse);

        // --------------------------------------------------
        // DRIP (EXCESS ENERGY ONLY)
        // --------------------------------------------------

        float drip = 0f;

        if (surgeEnergy > dripThreshold)
        {
            drip = baseDrip +
                   (surgeEnergy - dripThreshold) * 1.6f;
        }

        mat.SetFloat(dripProp, drip);

        // --------------------------------------------------
        // EMISSION (INNER LIFE)
        // --------------------------------------------------

        mat.SetFloat(emissionProp,
            baseEmission + surgeEnergy * 6.0f);
    }
}
