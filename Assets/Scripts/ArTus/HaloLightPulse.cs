using UnityEngine;

public class HaloLightPulse : MonoBehaviour
{
    [Header("🔆 Core Light")]
    public Light haloLight;

    [Header("🫀 Pulse Settings")]
    public float baseIntensity = 0.5f;
    public float pulseSpeed = 1.5f;
    public float pulseRange = 0.2f;

    [Header("🎭 ArTus Sync")]
    public bool syncWithEmotionController = true;
    public string visualTag = "default";

    [Header("✨ Bloom & Jitter Settings")]
    public bool enableBloom = true;
    public bool enableJitter = true;
    public float bloomThreshold = 0.8f; // Trigger bloom if intensity > threshold
    public float bloomBoost = 1.5f;     // Temporary intensity boost
    public float jitterAmount = 0.05f;  // Range of intensity randomization during jitter
    private float bloomTimer = 0f;
    private float bloomDuration = 0.3f;

    private float targetPulseSpeed;
    private float targetRange;
    private float timeOffset;

    void Start()
    {
        if (haloLight == null)
        {
            haloLight = GetComponent<Light>();
            if (haloLight == null)
            {
                Debug.LogWarning("[HaloLightPulse] No Light found on object.");
                enabled = false;
                return;
            }
        }

        timeOffset = Random.Range(0f, 3f);
        targetPulseSpeed = pulseSpeed;
        targetRange = pulseRange;
    }

    void Update()
    {
        if (haloLight == null) return;

        float emotionIntensity = 0.5f;

        if (syncWithEmotionController && ArTusEmotionController.Instance != null)
        {
            // Look up the current emotion state
            var state = ArTusEmotionController.Instance.CurrentEmotion;

            // Use ArTusEmotionData for all lookups
            targetPulseSpeed = ArTusEmotionData.GetPulseSpeed(state);
            targetRange = ArTusEmotionData.GetRippleStrength(state);
            emotionIntensity = ArTusEmotionData.GetEmotionIntensity(state);
        }

        float time = Time.time + timeOffset;
        float pulse = Mathf.Sin(time * targetPulseSpeed) * targetRange;
        float desired = baseIntensity + pulse;

        // 🌟 Bloom trigger
        if (enableBloom && emotionIntensity > bloomThreshold && bloomTimer <= 0f)
        {
            bloomTimer = bloomDuration;
            desired += bloomBoost;
        }

        if (bloomTimer > 0f)
        {
            bloomTimer -= Time.deltaTime;
        }

        // 🌫 Add jitter if high emotion intensity
        if (enableJitter && emotionIntensity > bloomThreshold)
        {
            desired += Random.Range(-jitterAmount, jitterAmount);
        }

        haloLight.intensity = Mathf.Lerp(haloLight.intensity, desired, Time.deltaTime * 5f);
    }
}
