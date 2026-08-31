using UnityEngine;

public class ArTusEmotionVisualRouter : MonoBehaviour
{
    [Header("Core References")]
    [SerializeField] private ArTusEmotionController emotionController;
    [SerializeField] private ArTusMorphController morphController;
    [SerializeField] private ArTusUnifiedParticleController particleController; // ✅ ADD THIS

    [Header("Particles")]
    [SerializeField] private ParticleSystem joyParticles;
    [SerializeField] private ParticleSystem sadParticles;
    [SerializeField] private ParticleSystem alertParticles;
    [SerializeField] private ParticleSystem curiousParticles;

    [Header("Halo (Optional)")]
    [SerializeField] private Renderer haloRenderer;

    private Material haloMat;

    private void Awake()
    {
        if (emotionController == null)
            emotionController = FindAnyObjectByType<ArTusEmotionController>();

        if (particleController == null)
            particleController = FindAnyObjectByType<ArTusUnifiedParticleController>();

        if (haloRenderer != null)
            haloMat = haloRenderer.material;
    }

    private void OnEnable()
    {
        if (emotionController != null)
            emotionController.OnEmotionChanged.AddListener(RouteEmotion);
    }

    private void OnDisable()
    {
        if (emotionController != null)
            emotionController.OnEmotionChanged.RemoveListener(RouteEmotion);
    }

    // ------------------------------------------------
    // CORE ROUTING
    // ------------------------------------------------
    private void RouteEmotion(ArTusEmotionController.EmotionState state)
    {
        StopAllParticles();

        switch (state)
        {
            case ArTusEmotionController.EmotionState.joy:
                Play(joyParticles);
                SetHaloColor(Color.yellow, 2.2f);
                BoostMorph(1.3f);
                particleController?.SetFireflyColor(Color.yellow); // ✅ MOVED HERE
                break;

            case ArTusEmotionController.EmotionState.sad:
                Play(sadParticles);
                SetHaloColor(Color.blue, 0.8f);
                BoostMorph(0.7f);
                particleController?.SetFireflyColor(Color.blue);
                break;

            case ArTusEmotionController.EmotionState.alert:
                Play(alertParticles);
                SetHaloColor(Color.red, 2.8f);
                BoostMorph(1.8f);
                particleController?.SetFireflyColor(Color.red); // ✅ MOVED HERE
                break;

            case ArTusEmotionController.EmotionState.curious:
                Play(curiousParticles);
                SetHaloColor(new Color(0.6f, 0.9f, 1f), 1.6f);
                BoostMorph(1.5f);
                particleController?.SetFireflyColor(Color.cyan);
                break;

            default:
                SetHaloColor(Color.white, 1f);
                BoostMorph(1f);
                particleController?.SetFireflyColor(Color.white);
                break;
        }
    }

    // ------------------------------------------------
    // PARTICLES
    // ------------------------------------------------
    private void Play(ParticleSystem ps)
    {
        if (ps == null) return;

        var main = ps.main;
        main.simulationSpeed = 1.2f;
        ps.Play();
    }

    private void StopAllParticles()
    {
        if (joyParticles != null) joyParticles.Stop();
        if (sadParticles != null) sadParticles.Stop();
        if (alertParticles != null) alertParticles.Stop();
        if (curiousParticles != null) curiousParticles.Stop();
    }

    // ------------------------------------------------
    // HALO CONTROL
    // ------------------------------------------------
    private void SetHaloColor(Color color, float intensity)
    {
        if (haloMat == null) return;

        haloMat.SetColor("_EmissionColor", color * intensity);
    }

    // ------------------------------------------------
    // MORPH BOOST
    // ------------------------------------------------
    private void BoostMorph(float multiplier)
    {
        if (morphController == null) return;

        morphController.baseAmplitude *= multiplier;
        morphController.rotationSpeed *= multiplier * 0.8f;
    }

    // ------------------------------------------------
    // FLASH EVENTS
    // ------------------------------------------------
    public void FlashEmotion(string emotion, float intensity)
    {
        Debug.Log($"[VisualRouter] Flash → {emotion} ({intensity:F2})");

        if (haloMat != null)
        {
            Color flashColor = Color.white;

            if (emotion.Contains("alert")) flashColor = Color.red;
            if (emotion.Contains("curious")) flashColor = Color.cyan;

            haloMat.SetColor("_EmissionColor", flashColor * (2f + intensity));
        }

        if (morphController != null)
        {
            morphController.baseAmplitude += intensity * 0.2f;
        }
    }
}