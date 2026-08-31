using UnityEngine;
using System.Collections.Generic;
using System;
using Random = UnityEngine.Random;

public class ArTusMorphController : MonoBehaviour
{
    [Header("Core References")]
    public ArTusCoreState core;
    public ArTusEmotionController emotionController;

    private ArTusShapeProfile activeShapeProfile;
    private ArTusShapeProfile targetShapeProfile;

    private float shapeBlend = 0f;

    private float lastTwist;
    private float lastRipple;
    private float lastPulse;

    private float scaleFluctuation;
    private Vector3 lastScale;

    [Header("Shader Sync")]
    public Renderer[] haloRenderers;
    private Material[] haloMats;

    [Header("Base Motion")]
    public float baseAmplitude = 0.16f;
    public float baseFrequency = 1.1f;
    public float rotationSpeed = 8f;

    [Header("Elasticity")]
    public float squashStrength = 0.35f;
    public float stretchStrength = 0.45f;
    public float reboundSpeed = 5f;
    public float wobbleStrength = 0.18f;
    public float wobbleFrequency = 2.2f;

    [Header("Autonomous Shape System")]
    public SkinnedMeshRenderer blobRenderer;
    public float shapeChangeSpeed = 5f;
    public float decisionInterval = 6f;

    [Header("Runtime Mesh Deformation")]
    public bool enableRuntimeMeshDeformation = true;
    [Range(0f, 2f)] public float runtimeDeformationStrength = 1.3f;
    public bool recalculateNormalsDuringDeformation = true;
    public bool preserveSmoothNormalsDuringDeformation = true;
    public bool recalculateTangentsDuringDeformation = true;

    [Header("Shape Commitment")]
    public float shapeCommitmentDuration = 24f;
    [Range(0f, 1f)] public float minimumCommittedBlend = 0.95f;
    [Range(0f, 2f)] public float shapeDominance = 1.35f;
    public float shapeReleaseSpeed = 0.05f;

    private int stretchIndex = -1;
    private int compressIndex = -1;
    private int pulseIndex = -1;

    private float targetStretch;
    private float targetCompress;
    private float targetPulse;

    private float currentStretch;
    private float currentCompress;
    private float currentPulse;

    private float nextDecisionTime;
    private Mesh runtimeMorphMesh;
    private Vector3[] baseVertices;
    private Vector3[] baseNormals;
    private Vector3[] deformedVertices;
    private Vector3[] deformedNormals;
    private Bounds runtimeMeshBounds;
    private bool runtimeMeshReady;
    private bool runtimeMeshDeformationUnavailable;
    private float shapeCommitmentUntil;

    [Header("Shape Memory")]
    [Range(0f, 100f)] public float preferredStretch = 59f;
    [Range(0f, 100f)] public float preferredCompress = 40f;
    [Range(0f, 100f)] public float preferredPulse = 80f;
    public float memoryLerpSpeed = 0.4f;

    [Header("Surge Control")]
    public float surgeDeltaThreshold = 0.28f;
    public float surgeGain = 2.5f;
    public float surgeDecay = 1.15f;
    public float surgeCooldownSeconds = 22f;
    [Range(0f, 1f)] public float committedSurgeChance = 0.01f;
    [Range(0f, 1f)] public float uncommittedSurgeChance = 0.05f;

    [Header("Expression")]
    public float asymmetryStrength = 0.35f;
    public float tumbleStrength = 22f;
    public float spinBoost = 1.75f;
    public bool performerMode = true;

    [Header("Identity Memory")]
    public float identityLerpSpeed = 0.09f;
    public Vector3 identityScale = Vector3.one;
    public Vector3 identityRotationBias = Vector3.zero;

    [Header("Debug")]
    public float liveMorphScalar = 1f;
    public float surgeEnergy = 2f;
    public string liveEmotion = "thinking";
    public bool verboseValidation = true;

    private float lastMorphScalar;
    private float lastSurgeTriggerTime = -999f;
    private Vector3 currentScale;
    private Quaternion currentRotation;
    private float timeSeed;
    private string lastEmotion = "thinking";

    private struct MorphProfile
    {
        public float ampBias;
        public float freqBias;
        public float rotationBias;
        public float asymmetry;
        public float elasticity;
        public float performer;
    }

    private Dictionary<string, MorphProfile> profiles;

    public ArTusShapeProfile GetActiveShapeProfile()
    {
        return targetShapeProfile;
    }

    public Vector3 GetCurrentScale()
    {
        return currentScale;
    }

    private void Awake()
    {
        ResolveReferences();
        timeSeed = Random.value * 1000f;
        currentScale = transform.localScale;
        currentRotation = transform.localRotation;
        nextDecisionTime = Time.time + Random.Range(0.5f, 1.5f);

        profiles = new Dictionary<string, MorphProfile>
        {
            { "thinking",  new MorphProfile { ampBias = 0.75f, freqBias = 0.8f,  rotationBias = 0.5f,  asymmetry = 0.10f, elasticity = 0.40f, performer = 0.25f } },
            { "curious",   new MorphProfile { ampBias = 1.30f, freqBias = 1.35f, rotationBias = 1.10f, asymmetry = 0.35f, elasticity = 0.90f, performer = 0.85f } },
            { "alert",     new MorphProfile { ampBias = 1.55f, freqBias = 1.75f, rotationBias = 1.65f, asymmetry = 0.28f, elasticity = 0.80f, performer = 0.65f } },
            { "calm",      new MorphProfile { ampBias = 0.45f, freqBias = 0.55f, rotationBias = 0.30f, asymmetry = 0.06f, elasticity = 0.25f, performer = 0.10f } },
            { "joy",       new MorphProfile { ampBias = 1.20f, freqBias = 0.95f, rotationBias = 0.85f, asymmetry = 0.24f, elasticity = 1.10f, performer = 1.00f } },
            { "focused",   new MorphProfile { ampBias = 0.90f, freqBias = 1.00f, rotationBias = 0.70f, asymmetry = 0.12f, elasticity = 0.55f, performer = 0.35f } },
            { "growing",   new MorphProfile { ampBias = 1.15f, freqBias = 1.10f, rotationBias = 0.95f, asymmetry = 0.22f, elasticity = 0.75f, performer = 0.55f } },
            { "satisfied", new MorphProfile { ampBias = 0.60f, freqBias = 0.65f, rotationBias = 0.40f, asymmetry = 0.08f, elasticity = 0.35f, performer = 0.15f } }
        };

        if (haloRenderers != null && haloRenderers.Length > 0)
        {
            var mats = new List<Material>();
            foreach (var r in haloRenderers)
            {
                if (r != null)
                    mats.AddRange(r.materials);
            }
            haloMats = mats.ToArray();
        }

        ValidateBlobRenderer();
        InitializeRuntimeMeshDeformation();
    }

    private void OnValidate()
    {
        ResolveReferences();
    }

    public void ApplyShapeProfile(ArTusShapeProfile profile)
    {
        if (profile == null) return;

        activeShapeProfile = profile;
        targetShapeProfile = profile;
        shapeBlend = 0f;
        shapeCommitmentUntil = Time.time + shapeCommitmentDuration;

        Debug.Log($"[Morph] Applying Shape Profile: {profile.displayName}");
    }

    private void ApplyDeformation(float sx, float sy, float sz, float twist, float ripple, float pulse)
    {
        transform.localScale = new Vector3(sx, sy, sz);

        // You can later hook twist/ripple/pulse into shaders or mesh logic
    }
    private void OnEnable()
    {
        ResolveReferences();
        if (emotionController != null)
            emotionController.OnEmotionChanged.AddListener(CacheEmotion);
    }

    private void OnDisable()
    {
        if (emotionController != null)
            emotionController.OnEmotionChanged.RemoveListener(CacheEmotion);
    }

    private void CacheEmotion(ArTusEmotionController.EmotionState state)
    {
        string resolved = state.ToString().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(resolved))
            lastEmotion = resolved;
    }

    private void ResolveReferences()
    {
        if (core == null)
            core = GetComponent<ArTusCoreState>() ?? FindAnyObjectByType<ArTusCoreState>();

        if (emotionController == null)
            emotionController = GetComponent<ArTusEmotionController>() ?? FindAnyObjectByType<ArTusEmotionController>();

        if (blobRenderer == null)
            blobRenderer = GetComponent<SkinnedMeshRenderer>() ?? GetComponentInChildren<SkinnedMeshRenderer>(true);

        if (haloRenderers == null || haloRenderers.Length == 0)
            haloRenderers = GetComponentsInChildren<Renderer>(true);
    }

    private void ValidateBlobRenderer()
    {
        if (blobRenderer == null)
        {
            if (verboseValidation)
                Debug.LogWarning("[ArTusMorphController] Blob Renderer is not assigned.");
            return;
        }

        Mesh mesh = blobRenderer.sharedMesh;
        if (mesh == null)
        {
            Debug.LogWarning("[ArTusMorphController] Blob Renderer has no mesh assigned.");
            return;
        }

        stretchIndex = mesh.GetBlendShapeIndex("Morph_Stretch");
        compressIndex = mesh.GetBlendShapeIndex("Morph_Compress");
        pulseIndex = mesh.GetBlendShapeIndex("Morph_Pulse");

        if (verboseValidation)
        {
            Debug.Log($"[ArTusMorphController] BlendShape indices | Stretch: {stretchIndex}, Compress: {compressIndex}, Pulse: {pulseIndex}");
        }

        blobRenderer.quality = SkinQuality.Bone4;
        blobRenderer.updateWhenOffscreen = true;

        if (stretchIndex < 0 || compressIndex < 0 || pulseIndex < 0)
        {
            Debug.LogWarning("[ArTusMorphController] One or more blend shapes were not found. Check FBX import and exact blend shape names.");
        }
    }

    private void InitializeRuntimeMeshDeformation()
    {
        if (!enableRuntimeMeshDeformation || blobRenderer == null || blobRenderer.sharedMesh == null)
            return;

        if (!blobRenderer.sharedMesh.isReadable)
        {
            runtimeMeshReady = false;
            runtimeMeshDeformationUnavailable = true;
            Debug.LogWarning("[ArTusMorphController] Runtime mesh deformation is unavailable because the assigned skinned mesh is not readable. Enable Read/Write on the imported face mesh.");
            return;
        }

        runtimeMorphMesh = Instantiate(blobRenderer.sharedMesh);
        runtimeMorphMesh.name = $"{blobRenderer.sharedMesh.name}_ArTusRuntime";
        runtimeMorphMesh.MarkDynamic();
        blobRenderer.sharedMesh = runtimeMorphMesh;

        baseVertices = runtimeMorphMesh.vertices;
        baseNormals = runtimeMorphMesh.normals;
        deformedNormals = new Vector3[baseVertices.Length];

        if (baseNormals == null || baseNormals.Length != baseVertices.Length)
        {
            baseNormals = new Vector3[baseVertices.Length];
            for (int i = 0; i < baseVertices.Length; i++)
                baseNormals[i] = baseVertices[i].sqrMagnitude > 0.0001f ? baseVertices[i].normalized : Vector3.up;
        }

        deformedVertices = new Vector3[baseVertices.Length];
        System.Array.Copy(baseVertices, deformedVertices, baseVertices.Length);
        runtimeMeshBounds = runtimeMorphMesh.bounds;
        runtimeMeshReady = true;
        runtimeMeshDeformationUnavailable = false;
    }

    private void EvaluateShapeState()
    {
        bool shapeCommitted = targetShapeProfile != null && Time.time < shapeCommitmentUntil;

        // Primary driver: purpose / living cadence
        float time = Time.time;
        float slowCycle = Mathf.Sin(time * 0.25f + timeSeed) * 0.5f + 0.5f;
        float midCycle = Mathf.Sin(time * 0.60f + timeSeed * 2f) * 0.5f + 0.5f;
        float fastCycle = Mathf.PerlinNoise(time * 0.40f, timeSeed);

        float baseStretch = Mathf.Lerp(15f, 55f, slowCycle);
        float baseCompress = Mathf.Lerp(5f, 35f, midCycle);
        float basePulse = Mathf.Lerp(20f, 70f, fastCycle);

        // Secondary driver: internal state
        float conflict = core != null ? core.GetConflictBeliefCount() : 0f;
        float curiosity = Mathf.PerlinNoise(time * 0.35f, timeSeed * 3f);

        baseStretch += conflict * 20f;
        baseCompress += conflict * 10f;
        basePulse += curiosity * 20f;

        // Tertiary: emotion influence only
        float emotionStretch = 0f;
        float emotionCompress = 0f;
        float emotionPulse = 0f;

        if (liveEmotion == "alert")
        {
            emotionStretch += 25f;
            emotionCompress += 15f;
            emotionPulse += 30f;
        }
        else if (liveEmotion == "calm")
        {
            emotionStretch -= 10f;
            emotionCompress -= 5f;
            emotionPulse -= 15f;
        }

        // Surge spike (continuous influence)
        if (surgeEnergy > 0.3f)
        {
            baseStretch += surgeEnergy * 40f;
            basePulse += surgeEnergy * 50f;
        }

        // Final combined values BEFORE memory
        float finalStretch = baseStretch + emotionStretch;
        float finalCompress = baseCompress + emotionCompress;
        float finalPulse = basePulse + emotionPulse;

        // 🔥 SURGE EVENT — HARD OVERRIDE
        bool surgeTriggered = false;

        float surgeChance = shapeCommitted ? committedSurgeChance : uncommittedSurgeChance;
        bool surgeCooldownElapsed = Time.time - lastSurgeTriggerTime >= surgeCooldownSeconds;
        bool protectCommittedShape =
            shapeCommitted &&
            targetShapeProfile != null &&
            shapeBlend >= minimumCommittedBlend &&
            (shapeCommitmentUntil - Time.time) > 6f;

        if (!protectCommittedShape && surgeCooldownElapsed && Random.value < surgeChance)
        {
            if (shapeCommitted)
            {
                targetStretch = Mathf.Max(targetStretch, 82f);
                targetCompress = Mathf.Max(targetCompress, 72f);
                targetPulse = Mathf.Max(targetPulse, 88f);
            }
            else
            {
                targetStretch = Random.Range(70f, 100f);
                targetCompress = Random.Range(50f, 90f);
                targetPulse = Random.Range(60f, 100f);
            }

            surgeTriggered = true;
            lastSurgeTriggerTime = Time.time;

            if (verboseValidation)
                Debug.Log("[ArTus] SURGE SHAPE TRIGGERED");
        }

        // 🧠 Memory stabilization ONLY if no surge
        if (!surgeTriggered)
        {
            targetStretch = Mathf.Lerp(finalStretch, preferredStretch, 0.20f);
            targetCompress = Mathf.Lerp(finalCompress, preferredCompress, 0.20f);
            targetPulse = Mathf.Lerp(finalPulse, preferredPulse, 0.20f);
        }

        if (targetShapeProfile != null)
        {
            Vector3 axis = SafeVector(targetShapeProfile.stretchAxisWeights, Vector3.one);
            float axisDeviation = Mathf.Abs(axis.x - 1f) + Mathf.Abs(axis.y - 1f) + Mathf.Abs(axis.z - 1f);
            float profileStretchFloor = 62f + Mathf.Clamp01(targetShapeProfile.complexity) * 18f + axisDeviation * 10f;
            float profileCompressFloor = 48f + Mathf.Clamp01(targetShapeProfile.stability) * 14f + Mathf.Clamp01(targetShapeProfile.taperStrength) * 10f;
            float profilePulseFloor = 58f + Mathf.Clamp01(targetShapeProfile.pulseStrength) * 26f + Mathf.Clamp01(targetShapeProfile.rippleStrength) * 12f;

            if (shapeCommitted)
            {
                profileStretchFloor += 12f;
                profileCompressFloor += 10f;
                profilePulseFloor += 14f;
            }

            targetStretch = Mathf.Max(targetStretch, profileStretchFloor);
            targetCompress = Mathf.Max(targetCompress, profileCompressFloor);
            targetPulse = Mathf.Max(targetPulse, profilePulseFloor);
        }

        // 🔒 Force visible morph strength
        float minStrength = 45f;

        targetStretch = Mathf.Clamp(targetStretch, minStrength, 100f);
        targetCompress = Mathf.Clamp(targetCompress, minStrength, 100f);
        targetPulse = Mathf.Clamp(targetPulse, minStrength, 100f);
    }

    private void Update()
    {
        if (core == null)
            return;

        liveEmotion = lastEmotion;

        float contradiction = Mathf.Clamp01(core.GetConflictBeliefCount() * 0.08f);
        float curiosityNoise = Mathf.PerlinNoise(Time.time * 0.35f, timeSeed) * 0.35f;
        float wobbleNoise = Mathf.PerlinNoise(timeSeed, Time.time * 0.55f) * 0.25f;

        liveMorphScalar = 1f + contradiction + curiosityNoise;

        if (Time.time > nextDecisionTime)
        {
            EvaluateShapeState();
            nextDecisionTime = Time.time + decisionInterval + Random.Range(0f, 2f);
        }

        bool shapeCommitted = targetShapeProfile != null && Time.time < shapeCommitmentUntil;

        // -------------------------------
        // SHAPE INTELLIGENCE VARIABLES
        // -------------------------------
        float shapeStretchX = 1f;
        float shapeStretchY = 1f;
        float shapeStretchZ = 1f;

        float shapeTwist = 0f;
        float shapeRipple = 0f;
        float shapePulse = 0f;

        if (targetShapeProfile != null)
        {
            float targetBlend = shapeCommitted ? 1f : 0.88f;
            float blendSpeed = shapeCommitted ? 1.75f : 0.55f;
            shapeBlend = Mathf.MoveTowards(shapeBlend, targetBlend, Time.deltaTime * blendSpeed);
            if (shapeCommitted)
                shapeBlend = Mathf.Max(shapeBlend, minimumCommittedBlend);

            shapeStretchX = Mathf.Lerp(1f, targetShapeProfile.stretchAxisWeights.x, shapeBlend);
            shapeStretchY = Mathf.Lerp(1f, targetShapeProfile.stretchAxisWeights.y, shapeBlend);
            shapeStretchZ = Mathf.Lerp(1f, targetShapeProfile.stretchAxisWeights.z, shapeBlend);

            shapeTwist = Mathf.Lerp(0f, targetShapeProfile.twistStrength, shapeBlend);
            shapeRipple = Mathf.Lerp(0f, targetShapeProfile.rippleStrength, shapeBlend);
            shapePulse = Mathf.Lerp(0f, targetShapeProfile.pulseStrength, shapeBlend);
        }
        else
        {
            shapeBlend = Mathf.MoveTowards(shapeBlend, 0f, Time.deltaTime * shapeReleaseSpeed);
        }

        // -------------------------------
        // SURGE TRACKING
        // -------------------------------
        float delta = liveMorphScalar - lastMorphScalar;
        if (delta > surgeDeltaThreshold)
            surgeEnergy += delta * surgeGain;

        surgeEnergy = Mathf.Max(0f, surgeEnergy - Time.deltaTime * surgeDecay);
        lastMorphScalar = liveMorphScalar;

        // -------------------------------
        // EMOTION PROFILE
        // -------------------------------
        if (!profiles.TryGetValue(lastEmotion, out MorphProfile profile))
            profile = profiles["thinking"];

        float amp = baseAmplitude * profile.ampBias * liveMorphScalar;
        float freq = baseFrequency * profile.freqBias;
        float rotBias = profile.rotationBias;
        float asym = profile.asymmetry * asymmetryStrength;
        float elastic = profile.elasticity;
        float performer = performerMode ? profile.performer : 0f;

        float t = Time.time * freq + timeSeed;

        float sinA = Mathf.Sin(t * 1.1f);
        float sinB = Mathf.Sin(t * 1.9f + 1.7f);
        float sinC = Mathf.Sin(t * 2.7f + 0.9f);

        float perlinX = Mathf.PerlinNoise(t, 0f) - 0.5f;
        float perlinY = Mathf.PerlinNoise(0f, t) - 0.5f;
        float perlinZ = Mathf.PerlinNoise(t, t) - 0.5f;

        float pulseWave = (Mathf.Sin(t * 2.2f) * 0.5f + 0.5f) * elastic;
        float rebound = Mathf.Sin(Time.time * reboundSpeed) * surgeEnergy;

        float stretchX = 1f + ((perlinX + sinA * 0.45f) * amp) + (rebound * stretchStrength);
        float stretchY = 1f + ((perlinY + sinB * 0.55f) * amp) - (rebound * squashStrength);
        float stretchZ = 1f + ((perlinZ + sinC * 0.45f) * amp) + (pulseWave * 0.15f);

        // -------------------------------
        // SHAPE BLEND (CORE MERGE POINT)
        // -------------------------------
        if (targetShapeProfile != null)
        {
            stretchX = Mathf.Lerp(stretchX, shapeStretchX, shapeBlend);
            stretchY = Mathf.Lerp(stretchY, shapeStretchY, shapeBlend);
            stretchZ = Mathf.Lerp(stretchZ, shapeStretchZ, shapeBlend);
        }

        // Expression layering
        stretchX += asym * sinA;
        stretchY -= asym * sinB * 0.6f;
        stretchZ += asym * sinC * 0.8f;

        stretchX += performer * 0.18f * Mathf.Sin(t * 3.5f);
        stretchY += performer * 0.12f * Mathf.Sin(t * 4.2f + 0.8f);
        stretchZ += performer * 0.16f * Mathf.Sin(t * 3.8f + 1.4f);

        Vector3 targetIdentityScale = new Vector3(stretchX, stretchY, stretchZ);
        if (targetShapeProfile != null)
        {
            Vector3 profileScaleInfluence = Vector3.Scale(
                new Vector3(shapeStretchX, shapeStretchY, shapeStretchZ),
                Vector3.Lerp(Vector3.one, SafeVector(targetShapeProfile.baseScale, Vector3.one), 0.65f)
            );

            float shapeWeight = Mathf.Clamp01(shapeBlend * shapeDominance);
            if (shapeCommitted)
                shapeWeight = Mathf.Max(shapeWeight, minimumCommittedBlend);

            targetIdentityScale = Vector3.Lerp(targetIdentityScale, profileScaleInfluence, shapeWeight);
        }

        identityScale = Vector3.Lerp(
            identityScale,
            targetIdentityScale,
            identityLerpSpeed * (1f + surgeEnergy * 0.4f)
        );

        currentScale = Vector3.Lerp(
            currentScale,
            identityScale,
            Time.deltaTime * (4f + elastic * 2f + surgeEnergy)
        );

        // -------------------------------
        // ROTATION
        // -------------------------------
        Vector3 wobbleEuler = new Vector3(
            (sinB + wobbleNoise) * wobbleStrength * 35f,
            (sinA + perlinX) * wobbleStrength * 45f,
            (sinC + perlinZ) * wobbleStrength * 30f
        );

        Vector3 tumbleEuler = new Vector3(
            performer * tumbleStrength * Mathf.Sin(t * 1.7f),
            rotBias * rotationSpeed * spinBoost * Time.time,
            performer * tumbleStrength * 0.7f * Mathf.Sin(t * 1.35f + 0.7f)
        );

        identityRotationBias = Vector3.Lerp(
            identityRotationBias,
            wobbleEuler * 0.35f,
            identityLerpSpeed
        );

        Quaternion targetRot = Quaternion.Euler(
            identityRotationBias +
            wobbleEuler +
            tumbleEuler +
            new Vector3(
                surgeEnergy * 18f * sinA,
                surgeEnergy * 28f,
                surgeEnergy * 14f * sinC
            )
        );

        currentRotation = Quaternion.Slerp(
            currentRotation,
            targetRot,
            Time.deltaTime * (rotationSpeed + rotBias + performer)
        );

        transform.localRotation = currentRotation;
        transform.localScale = currentScale;

        // -------------------------------
        // BLENDSHAPES
        // -------------------------------
        if (blobRenderer != null)
        {
            currentStretch = Mathf.Lerp(currentStretch, targetStretch, Time.deltaTime * shapeChangeSpeed);
            currentCompress = Mathf.Lerp(currentCompress, targetCompress, Time.deltaTime * shapeChangeSpeed);
            currentPulse = Mathf.Lerp(currentPulse, targetPulse, Time.deltaTime * shapeChangeSpeed);

            currentStretch = Mathf.Clamp(currentStretch, 0f, 100f);
            currentCompress = Mathf.Clamp(currentCompress, 0f, 100f);
            currentPulse = Mathf.Clamp(currentPulse, 0f, 100f);

            if (stretchIndex >= 0)
                blobRenderer.SetBlendShapeWeight(stretchIndex, currentStretch);

            if (compressIndex >= 0)
                blobRenderer.SetBlendShapeWeight(compressIndex, currentCompress);

            if (pulseIndex >= 0)
                blobRenderer.SetBlendShapeWeight(pulseIndex, currentPulse);
        }

        // -------------------------------
        // 🔥 NEW: RECONSTRUCTION TRACKING
        // -------------------------------
        scaleFluctuation = Vector3.Distance(currentScale, lastScale);
        lastScale = currentScale;

        lastTwist = Mathf.Sin(Time.time * 0.5f);
        lastRipple = Mathf.PerlinNoise(Time.time, 0f);
        lastPulse = currentPulse / 100f;

        // -------------------------------
        // MEMORY LEARNING
        // -------------------------------
        preferredStretch = Mathf.Lerp(preferredStretch, currentStretch, Time.deltaTime * memoryLerpSpeed);
        preferredCompress = Mathf.Lerp(preferredCompress, currentCompress, Time.deltaTime * memoryLerpSpeed);
        preferredPulse = Mathf.Lerp(preferredPulse, currentPulse, Time.deltaTime * memoryLerpSpeed);

        ApplyRuntimeMeshDeformation(t, elastic, performer);

        UpdateShader();
    }

    public float GetScaleFluctuation()
    {
        return scaleFluctuation;
    }

    public float GetTwistLevel()
    {
        return lastTwist;
    }

    public float GetRippleLevel()
    {
        return lastRipple;
    }

    public float GetPulseLevel()
    {
        return lastPulse;
    }

    private void UpdateShader()
    {
        if (haloMats == null || haloMats.Length == 0)
            return;

        float emotionIntensity = Mathf.Clamp01(liveMorphScalar - 1f) * 2f;
        float tension = core != null ? Mathf.Clamp01(core.GetConflictBeliefCount() * 0.1f) : 0f;
        float pulse = 1f + surgeEnergy * 2f;

        foreach (var mat in haloMats)
        {
            if (mat == null) continue;

            if (mat.HasProperty("_EmotionIntensity"))
                mat.SetFloat("_EmotionIntensity", emotionIntensity);

            if (mat.HasProperty("_Tension"))
                mat.SetFloat("_Tension", tension);

            if (mat.HasProperty("_Pulse"))
                mat.SetFloat("_Pulse", pulse);
        }
    }

    private void ApplyRuntimeMeshDeformation(float t, float elastic, float performer)
    {
        if (!enableRuntimeMeshDeformation || !runtimeMeshReady || runtimeMeshDeformationUnavailable || runtimeMorphMesh == null || baseVertices == null)
            return;

        if (!runtimeMorphMesh.isReadable)
        {
            runtimeMeshReady = false;
            runtimeMeshDeformationUnavailable = true;
            Debug.LogWarning("[ArTusMorphController] Runtime mesh deformation disabled because the runtime mesh is not readable. Enable Read/Write on the blob mesh import.");
            return;
        }

        bool shapeCommitted = targetShapeProfile != null && Time.time < shapeCommitmentUntil;
        float blend = targetShapeProfile == null
            ? 0f
            : Mathf.SmoothStep(0f, 1f, shapeBlend) * runtimeDeformationStrength;

        if (shapeCommitted)
            blend = Mathf.Max(blend, minimumCommittedBlend * runtimeDeformationStrength);

        Vector3 extents = runtimeMeshBounds.extents;
        Vector3 center = runtimeMeshBounds.center;

        string archetype = ResolveArchetype(targetShapeProfile);
        Vector3 axisWeights = targetShapeProfile != null ? SafeVector(targetShapeProfile.stretchAxisWeights, Vector3.one) : Vector3.one;
        Vector3 profileScale = targetShapeProfile != null ? SafeVector(targetShapeProfile.baseScale, Vector3.one) : Vector3.one;
        float twist = targetShapeProfile != null ? targetShapeProfile.twistStrength : 0f;
        float ripple = targetShapeProfile != null ? targetShapeProfile.rippleStrength : 0f;
        float pulse = targetShapeProfile != null ? targetShapeProfile.pulseStrength : 0f;
        float orbit = targetShapeProfile != null ? targetShapeProfile.orbitStrength : 0f;
        float taper = targetShapeProfile != null ? targetShapeProfile.taperStrength : 0f;

        for (int i = 0; i < baseVertices.Length; i++)
        {
            Vector3 vertex = baseVertices[i];
            Vector3 normalized = NormalizeVertex(vertex, center, extents);
            Vector3 normal = baseNormals[i].sqrMagnitude > 0.0001f ? baseNormals[i].normalized : normalized.normalized;

            Vector3 deformed = BuildArchetypeTarget(archetype, normalized, normal, t, twist, ripple, pulse, orbit, taper, elastic, performer);
            deformed.Scale(Vector3.Lerp(Vector3.one, axisWeights, 0.65f));
            deformed.Scale(Vector3.Lerp(Vector3.one, profileScale, 0.45f));

            Vector3 finalVertex = Vector3.Lerp(normalized, deformed, blend);
            deformedVertices[i] = DenormalizeVertex(finalVertex, center, extents);

            if (deformedNormals != null && i < deformedNormals.Length)
            {
                Vector3 targetNormal = finalVertex.sqrMagnitude > 0.0001f
                    ? finalVertex.normalized
                    : normal;
                deformedNormals[i] = Vector3.Slerp(normal, targetNormal, Mathf.Clamp01(blend * 0.22f)).normalized;
            }
        }

        try
        {
            runtimeMorphMesh.vertices = deformedVertices;
            runtimeMorphMesh.RecalculateBounds();
            if (preserveSmoothNormalsDuringDeformation && deformedNormals != null && deformedNormals.Length == deformedVertices.Length)
            {
                runtimeMorphMesh.normals = deformedNormals;
            }
            else if (recalculateNormalsDuringDeformation)
            {
                runtimeMorphMesh.RecalculateNormals();
            }

            if (recalculateTangentsDuringDeformation)
                runtimeMorphMesh.RecalculateTangents();
        }
        catch (Exception ex)
        {
            runtimeMeshReady = false;
            runtimeMeshDeformationUnavailable = true;
            Debug.LogWarning($"[ArTusMorphController] Runtime mesh deformation disabled after mesh access failure: {ex.Message}");
        }
    }

    private static Vector3 BuildArchetypeTarget(
        string archetype,
        Vector3 p,
        Vector3 n,
        float t,
        float twist,
        float ripple,
        float pulse,
        float orbit,
        float taper,
        float elastic,
        float performer)
    {
        Vector3 result = p;
        Vector2 xz = new Vector2(p.x, p.z);
        float radial = xz.magnitude;
        Vector2 radialDir = radial > 0.0001f ? xz / radial : Vector2.right;
        float motion = 0.08f + elastic * 0.04f;

        switch (archetype)
        {
            case "sphere":
                result = p.sqrMagnitude > 0.0001f ? p.normalized * 0.9f : Vector3.up * 0.9f;
                break;
            case "cube":
                result = new Vector3(
                    Mathf.Sign(p.x) * Mathf.Pow(Mathf.Abs(p.x), 0.55f),
                    Mathf.Sign(p.y) * Mathf.Pow(Mathf.Abs(p.y), 0.55f),
                    Mathf.Sign(p.z) * Mathf.Pow(Mathf.Abs(p.z), 0.55f)
                );
                break;
            case "cylinder":
                result = new Vector3(radialDir.x * 0.8f, p.y, radialDir.y * 0.8f);
                break;
            case "cone":
                float coneRadius = Mathf.Lerp(0.85f, 0.08f, Mathf.InverseLerp(-1f, 1f, p.y));
                result = new Vector3(radialDir.x * coneRadius, p.y, radialDir.y * coneRadius);
                break;
            case "torus":
                float major = 0.72f;
                float minor = 0.22f + pulse * 0.06f;
                result = new Vector3(
                    radialDir.x * (major + (radial - 0.45f) * minor),
                    p.y * 0.42f,
                    radialDir.y * (major + (radial - 0.45f) * minor)
                );
                break;
            case "helix":
                float helixAngle = p.y * (2.6f + twist * 3.4f);
                float helixRadius = Mathf.Lerp(0.35f, 0.78f, (p.y + 1f) * 0.5f);
                result = new Vector3(
                    Mathf.Cos(helixAngle) * helixRadius,
                    p.y,
                    Mathf.Sin(helixAngle) * helixRadius
                );
                break;
            case "shell":
                float shellTwist = p.y * (1.9f + twist * 2.2f);
                float shellRadius = Mathf.Lerp(0.15f, 0.9f, (p.y + 1f) * 0.5f);
                result = new Vector3(
                    Mathf.Cos(shellTwist) * shellRadius,
                    p.y,
                    Mathf.Sin(shellTwist) * shellRadius
                );
                break;
            case "star":
                float spoke = Mathf.Atan2(p.z, p.x) * 5f;
                float starRadius = 0.45f + Mathf.Cos(spoke) * 0.28f;
                result = new Vector3(radialDir.x * starRadius, p.y * 0.7f, radialDir.y * starRadius);
                break;
            case "lattice":
                result = new Vector3(
                    p.x + Mathf.Sin(p.y * 8f + t * 2.4f) * 0.16f,
                    p.y + Mathf.Sin((p.x + p.z) * 7f + t * 2.1f) * 0.12f,
                    p.z + Mathf.Cos(p.y * 8f + t * 2.4f) * 0.16f
                );
                break;
        }

        float twistAngle = twist * (0.8f + performer * 0.4f) * p.y * Mathf.PI;
        float sinTwist = Mathf.Sin(twistAngle);
        float cosTwist = Mathf.Cos(twistAngle);
        Vector3 twisted = new Vector3(
            result.x * cosTwist - result.z * sinTwist,
            result.y,
            result.x * sinTwist + result.z * cosTwist
        );

        float orbitAngle = orbit * 0.8f;
        float sinOrbit = Mathf.Sin(orbitAngle);
        float cosOrbit = Mathf.Cos(orbitAngle);
        Vector3 orbited = new Vector3(
            twisted.x * cosOrbit - twisted.z * sinOrbit,
            twisted.y,
            twisted.x * sinOrbit + twisted.z * cosOrbit
        );

        float taperScale = Mathf.Lerp(1f, 1f - taper * 0.55f, Mathf.InverseLerp(-1f, 1f, p.y));
        orbited.x *= taperScale;
        orbited.z *= taperScale;

        float pulseOffset = Mathf.Sin(t * (2.2f + pulse * 2.4f) + (p.y * 4f)) * pulse * motion;
        float rippleOffset = Mathf.Sin((radial * 9f) - (p.y * 5f) + (t * 2.7f)) * ripple * motion * 0.8f;
        orbited += n * (pulseOffset + rippleOffset);

        return Vector3.Lerp(p, orbited, 0.9f);
    }

    private static string ResolveArchetype(ArTusShapeProfile profile)
    {
        if (profile == null)
            return "organic";

        string joined = string.Join(
            " ",
            new[]
            {
                profile.archetype,
                profile.shapeId,
                profile.displayName,
                profile.category,
                profile.symbolicMeaning
            }
        ).ToLowerInvariant();

        if (joined.Contains("torus")) return "torus";
        if (joined.Contains("helix") || joined.Contains("spiral")) return "helix";
        if (joined.Contains("sphere") || joined.Contains("orb")) return "sphere";
        if (joined.Contains("cube") || joined.Contains("box")) return "cube";
        if (joined.Contains("cylinder")) return "cylinder";
        if (joined.Contains("cone") || joined.Contains("tower")) return "cone";
        if (joined.Contains("shell")) return "shell";
        if (joined.Contains("star")) return "star";
        if (joined.Contains("lattice") || joined.Contains("grid") || joined.Contains("network")) return "lattice";

        return "organic";
    }

    private static Vector3 NormalizeVertex(Vector3 vertex, Vector3 center, Vector3 extents)
    {
        return new Vector3(
            extents.x > 0.0001f ? (vertex.x - center.x) / extents.x : 0f,
            extents.y > 0.0001f ? (vertex.y - center.y) / extents.y : 0f,
            extents.z > 0.0001f ? (vertex.z - center.z) / extents.z : 0f
        );
    }

    private static Vector3 DenormalizeVertex(Vector3 vertex, Vector3 center, Vector3 extents)
    {
        return new Vector3(
            center.x + vertex.x * extents.x,
            center.y + vertex.y * extents.y,
            center.z + vertex.z * extents.z
        );
    }

    private static Vector3 SafeVector(Vector3 value, Vector3 fallback)
    {
        if (float.IsNaN(value.x) || float.IsNaN(value.y) || float.IsNaN(value.z))
            return fallback;

        return new Vector3(
            Mathf.Abs(value.x) < 0.0001f ? fallback.x : value.x,
            Mathf.Abs(value.y) < 0.0001f ? fallback.y : value.y,
            Mathf.Abs(value.z) < 0.0001f ? fallback.z : value.z
        );
    }
}
