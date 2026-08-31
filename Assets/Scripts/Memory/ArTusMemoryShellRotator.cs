using UnityEngine;

/// <summary>
/// Hi-Class Memory Shell Rotator
/// Governs full volumetric rotation (yaw + pitch + roll)
/// Represents ArTus circling, weighing, tumbling thoughts
/// </summary>
public class ArTusMemoryShellRotator : MonoBehaviour
{
    [Header("Base Rotation")]
    public float baseRotationSpeed = 12f;
    public float spinMultiplier = 1.0f;

    [Header("Volumetric Axes")]
    public float yawStrength = 1.0f;     // Left / Right
    public float pitchStrength = 0.6f;   // Forward / Back
    public float rollStrength = 0.8f;    // Barrel roll

    [Header("Organic Drift")]
    public float wobbleStrength = 0.35f;
    public float wobbleSpeed = 0.4f;

    [Header("Emotion Influence")]
    public bool enableEmotionInfluence = true;
    public ArTusEmotionController emotionController;

    private float currentSpeed;
    private float timeSeed;

    void Awake()
    {
        currentSpeed = baseRotationSpeed;
        timeSeed = Random.value * 1000f;
    }

    void Update()
    {
        float dt = Time.deltaTime;

        // --------------------------------------------------
        // EMOTION-AWARE SPEED MODULATION (ENUM → STRING SAFE)
        // --------------------------------------------------
        if (enableEmotionInfluence && emotionController != null)
        {
            string emotion = emotionController.CurrentEmotion.ToString().ToLowerInvariant();

            currentSpeed = baseRotationSpeed * GetEmotionSpinBias(emotion);
        }
        else
        {
            currentSpeed = baseRotationSpeed;
        }

        // --------------------------------------------------
        // ORGANIC WOBBLE (BREAKS PERFECT SYMMETRY)
        // --------------------------------------------------
        float wobble =
            (Mathf.PerlinNoise(Time.time * wobbleSpeed, timeSeed) - 0.5f)
            * wobbleStrength;

        float finalSpeed = currentSpeed * (1f + wobble) * spinMultiplier;

        // --------------------------------------------------
        // TRUE 360° VOLUMETRIC ROTATION (NOT CLOCK SPIN)
        // --------------------------------------------------
        float yaw = finalSpeed * yawStrength * dt;
        float pitch = finalSpeed * pitchStrength * dt;
        float roll = finalSpeed * rollStrength * dt;

        transform.Rotate(pitch, yaw, roll, Space.Self);
    }

    // --------------------------------------------------
    // EMOTION → ROTATIONAL BEHAVIOR
    // --------------------------------------------------
    private float GetEmotionSpinBias(string emotion)
    {
        return emotion switch
        {
            "alert" => 1.8f, // aggressive tumble
            "curious" => 1.4f, // exploratory roll
            "joy" => 1.2f, // buoyant spin
            "thinking" => 0.7f, // slow cognitive drift
            "calm" => 0.5f, // near-still float
            _ => 1.0f
        };
    }
}
