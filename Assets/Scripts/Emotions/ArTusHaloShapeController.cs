using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Controls the SHAPE of ArTus halo (not motion).
/// Works with shader-driven deformation + radial logic.
/// This is where torus → star → bloom happens.
/// </summary>
public class ArTusHaloShapeController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Renderer haloRenderer;
    [SerializeField] private ArTusEmotionController emotionController;
    [SerializeField] private ArTusCoreState core;

    private Material haloMat;

    [Header("Shape Dynamics")]
    public float shapeLerpSpeed = 4f;
    public float pulseSpeed = 2.5f;

    [Header("Base Values")]
    public float baseFlare = 0.2f;
    public float baseSharpness = 0.3f;
    public float baseNoise = 0.15f;

    private float targetFlare;
    private float targetSharpness;
    private float targetNoise;

    private float currentFlare;
    private float currentSharpness;
    private float currentNoise;

    private float pulse;

    private string lastEmotion = "thinking";

    // ------------------------------------------------
    // SHAPE PROFILES
    // ------------------------------------------------

    private struct ShapeProfile
    {
        public float flare;
        public float sharpness;
        public float noise;
        public int segments;
    }

    private Dictionary<string, ShapeProfile> profiles;

    void Awake()
    {
        ResolveReferences();
        RefreshHaloMaterial();

        profiles = new Dictionary<string, ShapeProfile>
        {
            { "thinking", new ShapeProfile { flare = 0.2f, sharpness = 0.3f, noise = 0.1f, segments = 8 } },
            { "curious",  new ShapeProfile { flare = 0.6f, sharpness = 0.7f, noise = 0.3f, segments = 10 } },
            { "alert",    new ShapeProfile { flare = 1.2f, sharpness = 1.0f, noise = 0.2f, segments = 12 } },
            { "joy",      new ShapeProfile { flare = 0.9f, sharpness = 0.6f, noise = 0.4f, segments = 9 } },
            { "calm",     new ShapeProfile { flare = 0.15f, sharpness = 0.2f, noise = 0.05f, segments = 6 } },
            { "focused",  new ShapeProfile { flare = 0.4f, sharpness = 0.8f, noise = 0.1f, segments = 5 } },
            { "growing",  new ShapeProfile { flare = 0.7f, sharpness = 0.6f, noise = 0.3f, segments = 11 } }
        };
    }

    void OnValidate()
    {
        ResolveReferences();
        RefreshHaloMaterial();
    }

    void OnEnable()
    {
        ResolveReferences();
        if (emotionController != null)
            emotionController.OnEmotionChanged.AddListener(OnEmotionChanged);
    }

    void OnDisable()
    {
        if (emotionController != null)
            emotionController.OnEmotionChanged.RemoveListener(OnEmotionChanged);
    }

    private void OnEmotionChanged(ArTusEmotionController.EmotionState state)
    {
        lastEmotion = state.ToString().ToLower();
    }

    void Update()
    {
        if (haloMat == null && haloRenderer != null)
            RefreshHaloMaterial();

        if (haloMat == null) return;

        // ------------------------------------------------
        // GET PROFILE
        // ------------------------------------------------

        if (!profiles.TryGetValue(lastEmotion, out var profile))
            profile = profiles["thinking"];

        // ------------------------------------------------
        // COGNITIVE INFLUENCE
        // ------------------------------------------------

        float contradiction = core != null
            ? Mathf.Clamp01(core.GetConflictBeliefCount() * 0.08f)
            : 0f;

        float curiosity =
            Mathf.PerlinNoise(Time.time * 0.25f, 0f) * 0.3f;

        // ------------------------------------------------
        // TARGET VALUES
        // ------------------------------------------------

        targetFlare = profile.flare + contradiction;
        targetSharpness = profile.sharpness + contradiction * 0.5f;
        targetNoise = profile.noise + curiosity;

        // ------------------------------------------------
        // SMOOTH TRANSITION
        // ------------------------------------------------

        currentFlare = Mathf.Lerp(currentFlare, targetFlare, Time.deltaTime * shapeLerpSpeed);
        currentSharpness = Mathf.Lerp(currentSharpness, targetSharpness, Time.deltaTime * shapeLerpSpeed);
        currentNoise = Mathf.Lerp(currentNoise, targetNoise, Time.deltaTime * shapeLerpSpeed);

        // ------------------------------------------------
        // PULSE (LIFE)
        // ------------------------------------------------

        pulse = Mathf.Sin(Time.time * pulseSpeed) * 0.5f + 0.5f;

        // ------------------------------------------------
        // APPLY TO SHADER
        // ------------------------------------------------

        haloMat.SetFloat("_FlareStrength", currentFlare);
        haloMat.SetFloat("_ShapeSharpness", currentSharpness);
        haloMat.SetFloat("_NoiseStrength", currentNoise);
        haloMat.SetFloat("_Pulse", pulse);

        haloMat.SetInt("_RadialSegments", profile.segments);
    }

    private void ResolveReferences()
    {
        if (haloRenderer == null)
            haloRenderer = GetComponent<Renderer>() ?? GetComponentInChildren<Renderer>(true);

        if (emotionController == null)
            emotionController = GetComponent<ArTusEmotionController>() ?? FindAnyObjectByType<ArTusEmotionController>();

        if (core == null)
            core = GetComponent<ArTusCoreState>() ?? FindAnyObjectByType<ArTusCoreState>();
    }

    private void RefreshHaloMaterial()
    {
        if (haloRenderer == null)
        {
            haloMat = null;
            return;
        }

        // OnValidate runs outside Play Mode. Accessing Renderer.material there
        // creates and leaks an instanced material into the scene.
        haloMat = Application.isPlaying
            ? haloRenderer.material
            : haloRenderer.sharedMaterial;
    }

    // ------------------------------------------------
    // EXTERNAL TRIGGERS (IMPORTANT)
    // ------------------------------------------------

    public void Burst(float intensity)
    {
        targetFlare += intensity * 0.8f;
        targetSharpness += intensity * 0.5f;
    }

    public void Collapse(float intensity)
    {
        targetFlare *= (1f - intensity);
        targetSharpness *= (1f - intensity);
    }

    public void Spike()
    {
        targetSharpness += 1.2f;
        targetFlare += 0.6f;
    }
}
