using UnityEngine;
using System.Collections;

/// <summary>
/// Hi-Class Aura Manager (Upgraded)
/// Controls torus, halo, fog, particles, and ambient intelligence.
/// </summary>
public class ArTusAuraManager : MonoBehaviour
{
    [Header("References")]
    public ArTusEmotionController emotionController;
    public ArTusMemoryFogController memoryFogController;
    public ArTusUnifiedParticleController particleController;

    public Renderer torusRenderer;
    public Light haloLight;
    public ParticleSystem emotionParticles;
    public Material fogMaterial;

    [Header("Base Aura Settings")]
    [Range(0f, 2f)] public float baseEmission = 1.0f;
    [Range(0f, 1f)] public float baseTransparency = 0.35f;
    [Range(0f, 5f)] public float pulseSpeed = 1.5f;
    [Range(0f, 1f)] public float pulseAmplitude = 0.2f;
    [Range(0f, 1f)] public float secondaryHueShift = 0.03f;

    [Header("Full Spectrum Blending")]
    public bool enableDynamicSpectrum = true;
    [Range(0f, 1f)] public float hueDrift = 0.08f;
    [Range(0f, 1f)] public float saturationBreath = 0.15f;
    [Range(0f, 1f)] public float valueBreath = 0.1f;
    public float blendSpeed = 2.5f;

    [Header("Fog")]
    [Range(0f, 1f)] public float fogIntensity = 0.6f;

    [Header("Depth Control")]
    [Range(0f, 1f)] public float darknessBias = 0.35f;
    [Range(0f, 1f)] public float contrastBoost = 0.25f;

    [Header("Advanced Aura Dynamics")]
    [Range(0f, 2f)] public float surgeIntensity = 0f;
    [Range(0f, 2f)] public float contradictionRipple = 0f;

    private float lastConflictLevel = 0f;

    // Internal state
    private Color targetColor = Color.white;
    private Color currentColor = Color.white;

    private float targetHue;
    private float targetSat;
    private float targetVal;

    void Awake()
    {
        if (emotionController == null)
            emotionController = FindAnyObjectByType<ArTusEmotionController>();

        if (particleController == null)
            particleController = FindAnyObjectByType<ArTusUnifiedParticleController>();

        if (emotionController == null)
            Debug.LogWarning("[AuraManager] EmotionController not found.");
    }

    void OnEnable()
    {
        if (emotionController != null)
            emotionController.OnEmotionChanged.AddListener(OnEmotionChanged);
    }

    void OnDisable()
    {
        if (emotionController != null)
            emotionController.OnEmotionChanged.RemoveListener(OnEmotionChanged);
    }

    void Update()
    {
        UpdateCognitiveState();
        UpdatePulse();
        UpdateSpectrumBlend();
        ApplyVisuals();
    }

    // --------------------------------------------------
    // EMOTION → TARGET COLOR
    // --------------------------------------------------
    private void OnEmotionChanged(ArTusEmotionController.EmotionState state)
    {
        targetColor = ArTusEmotionData.GetColorForEmotion(state);
        Color.RGBToHSV(targetColor, out targetHue, out targetSat, out targetVal);
    }

    // --------------------------------------------------
    // COGNITIVE STATE (NEW)
    // --------------------------------------------------
    private void UpdateCognitiveState()
    {
        float conflictLevel = 0f;

        if (memoryFogController != null)
            conflictLevel = Mathf.Clamp01(memoryFogController.GetFogIntensity());

        float delta = conflictLevel - lastConflictLevel;

        if (delta > 0.15f)
        {
            surgeIntensity += delta * 1.5f;

            particleController?.TriggerBurstEffect("contradiction", currentColor);
            particleController?.SetFireflyColor(Color.red);
        }

        surgeIntensity = Mathf.Max(0f, surgeIntensity - Time.deltaTime * 1.2f);

        contradictionRipple = Mathf.Lerp(
            contradictionRipple,
            conflictLevel,
            Time.deltaTime * 2f
        );

        lastConflictLevel = conflictLevel;
    }

    // --------------------------------------------------
    // DYNAMIC BLEND (HSV SPACE)
    // --------------------------------------------------
    private void UpdateSpectrumBlend()
    {
        float h = targetHue;
        float s = targetSat;
        float v = targetVal;

        if (enableDynamicSpectrum)
        {
            h += Mathf.Sin(Time.time * 0.25f) * hueDrift;
            s += Mathf.Sin(Time.time * 0.35f) * saturationBreath;
            v += Mathf.Cos(Time.time * 0.3f) * valueBreath;
        }

        v = Mathf.Lerp(v, v * (1f - darknessBias), 0.7f);
        s = Mathf.Clamp01(s + contrastBoost);

        h = Mathf.Repeat(h, 1f);
        s = Mathf.Clamp01(s);
        v = Mathf.Clamp01(v);

        Color primary = Color.HSVToRGB(h, s, v);

        float secondaryHue = h + Mathf.Sin(Time.time * 0.15f) * secondaryHueShift;

        Color secondary = Color.HSVToRGB(
            Mathf.Repeat(secondaryHue, 1f),
            s * 0.9f,
            v * 0.8f
        );

        Color blended = Color.Lerp(primary, secondary, 0.3f);

        currentColor = Color.Lerp(
            currentColor,
            blended,
            Time.deltaTime * blendSpeed * 0.8f
        );

        // 🔥 subtle instability
        currentColor += new Color(
            contradictionRipple * 0.1f,
            0f,
            contradictionRipple * 0.05f,
            0f
        );
    }

    // --------------------------------------------------
    // PULSE (UPGRADED)
    // --------------------------------------------------
    private void UpdatePulse()
    {
        if (haloLight == null) return;

        float basePulse =
            Mathf.Sin(Time.time * pulseSpeed) * pulseAmplitude;

        float surgePulse =
            Mathf.Sin(Time.time * pulseSpeed * 2f) * surgeIntensity * 0.3f;

        float pulse = basePulse + surgePulse;

        haloLight.intensity =
            Mathf.Max(0.1f, baseEmission + pulse + surgeIntensity * 0.5f);
    }

    // --------------------------------------------------
    // APPLY VISUALS
    // --------------------------------------------------
    private void ApplyVisuals()
    {
        if (torusRenderer != null)
        {
            Material mat = torusRenderer.material;

            if (mat.HasProperty("_EmissionColor"))
                mat.SetColor("_EmissionColor", currentColor * baseEmission);

            if (mat.HasProperty("_Transparency"))
                mat.SetFloat("_Transparency", baseTransparency);
        }

        if (haloLight != null)
            haloLight.color = currentColor;

        if (emotionParticles != null)
        {
            var main = emotionParticles.main;
            main.startColor = currentColor;
        }

        memoryFogController?.SetFogColor(currentColor, fogIntensity);

        if (fogMaterial != null)
        {
            if (fogMaterial.HasProperty("_EmissionColor"))
                fogMaterial.SetColor("_EmissionColor", currentColor * 1.2f);
        }

        // 🔥 Firefly sync
        particleController?.SetFireflyColor(currentColor);
    }

    // --------------------------------------------------
    // PUBLIC OVERRIDE
    // --------------------------------------------------
    public void ForceColor(Color color)
    {
        targetColor = color;
        Color.RGBToHSV(color, out targetHue, out targetSat, out targetVal);
    }
}