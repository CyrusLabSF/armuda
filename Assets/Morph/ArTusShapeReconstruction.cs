using UnityEngine;
using System.Linq;

public class ArTusShapeReconstruction : MonoBehaviour
{
    [Header("Dependencies")]
    public ArTusMorphController morph;
    public ArTusShapeIntelligence shapeIntelligence;

    [Header("Evaluation Settings")]
    public float evaluationInterval = 6f;
    public bool enableAutoEvaluation = true;

    private float nextEvalTime;
    private float lastScaleScore;
    private float lastMotionScore;
    private float lastStabilityScore;
    private float lastFinalScore;
    private string lastEvaluatedShapeId;

    private void Awake()
    {
        if (morph == null)
            morph = FindAnyObjectByType<ArTusMorphController>();

        if (shapeIntelligence == null)
            shapeIntelligence = FindAnyObjectByType<ArTusShapeIntelligence>();

        nextEvalTime = Time.time + Random.Range(3f, 6f);
    }

    private void Update()
    {
        if (!enableAutoEvaluation) return;
        if (Time.time < nextEvalTime) return;

        nextEvalTime = Time.time + evaluationInterval;

        EvaluateCurrentShape();
    }

    // =========================================
    // CORE EVALUATION
    // =========================================
    public void EvaluateCurrentShape()
    {
        var profile = morph.GetActiveShapeProfile();

        if (profile == null)
            return;

        float scaleScore = EvaluateScaleMatch(profile);
        float motionScore = EvaluateMotionMatch(profile);
        float stabilityScore = EvaluateStability(profile);

        float finalScore = (scaleScore * 0.5f) + (motionScore * 0.3f) + (stabilityScore * 0.2f);

        lastScaleScore = scaleScore;
        lastMotionScore = motionScore;
        lastStabilityScore = stabilityScore;
        lastFinalScore = finalScore;
        lastEvaluatedShapeId = profile.shapeId;

        ApplyLearning(profile, finalScore);

        Debug.Log($"[Reconstruction] {profile.displayName} Score: {finalScore:F2}");
    }

    // =========================================
    // SCALE MATCH
    // =========================================
    private float EvaluateScaleMatch(ArTusShapeProfile profile)
    {
        Vector3 current = morph.GetCurrentScale();
        Vector3 target = profile.stretchAxisWeights;

        float dx = Mathf.Abs(current.x - target.x);
        float dy = Mathf.Abs(current.y - target.y);
        float dz = Mathf.Abs(current.z - target.z);

        float error = (dx + dy + dz) / 3f;

        return Mathf.Clamp01(1f - error);
    }

    // =========================================
    // MOTION MATCH
    // =========================================
    private float EvaluateMotionMatch(ArTusShapeProfile profile)
    {
        float twist = morph.GetTwistLevel();
        float ripple = morph.GetRippleLevel();
        float pulse = morph.GetPulseLevel();

        float twistError = Mathf.Abs(twist - profile.twistStrength);
        float rippleError = Mathf.Abs(ripple - profile.rippleStrength);
        float pulseError = Mathf.Abs(pulse - profile.pulseStrength);

        float error = (twistError + rippleError + pulseError) / 3f;

        return Mathf.Clamp01(1f - error);
    }

    // =========================================
    // STABILITY CHECK
    // =========================================
    private float EvaluateStability(ArTusShapeProfile profile)
    {
        float fluctuation = morph.GetScaleFluctuation();

        // lower fluctuation = higher stability
        float stabilityScore = 1f - Mathf.Clamp01(fluctuation);

        return Mathf.Lerp(stabilityScore, profile.stability, 0.5f);
    }

    // =========================================
    // LEARNING UPDATE
    // =========================================
    private void ApplyLearning(ArTusShapeProfile profile, float score)
    {
        profile.reconstructionScore = Mathf.Lerp(profile.reconstructionScore, score, 0.4f);

        if (score > 0.7f)
        {
            profile.successfulReproductions++;
            profile.confidence = Mathf.Clamp01(profile.confidence + 0.05f);
        }
        else if (score < 0.4f)
        {
            profile.confidence = Mathf.Clamp01(profile.confidence - 0.03f);
        }

        profile.timesLearned++;
    }

    public float GetLastScaleScore() => lastScaleScore;

    public float GetLastMotionScore() => lastMotionScore;

    public float GetLastStabilityScore() => lastStabilityScore;

    public float GetLastFinalScore() => lastFinalScore;

    public string GetLastEvaluatedShapeId() => lastEvaluatedShapeId;
}
