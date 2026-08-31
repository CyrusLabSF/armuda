using UnityEngine;

public class ArTusUnifiedParticleController : MonoBehaviour
{
    [Header("Burst Particle Systems")]
    [SerializeField] private ParticleSystem defaultBurst;
    [SerializeField] private ParticleSystem synthesisBurst;
    [SerializeField] private ParticleSystem contradictionBurst;

    [Header("Ambient Fireflies")]
    [SerializeField] private ParticleSystem fireflySystem;

    private Color currentFireflyColor = Color.cyan;
    private float fireflyPulseTime;
    private bool hasLoggedMissingFireflySystem;

    // ------------------------------------------------
    // INIT
    // ------------------------------------------------
    void Start()
    {
        InitializeFireflies(currentFireflyColor);
    }

    void Update()
    {
        UpdateFireflies();
    }

    // ------------------------------------------------
    // BURST SYSTEM (UNCHANGED CORE)
    // ------------------------------------------------
    public void TriggerBurstEffect(string effectName, Color tint)
    {
        ParticleSystem target = effectName.ToLower() switch
        {
            "synthesis" => synthesisBurst,
            "contradiction" => contradictionBurst,
            _ => defaultBurst
        };

        if (target == null)
        {
            Debug.LogWarning($"[Particles] No particle system assigned for '{effectName}'.");
            return;
        }

        var main = target.main;
        main.startColor = new ParticleSystem.MinMaxGradient(tint);

        target.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        target.Play();

        Debug.Log($"[Particles] Burst → {effectName}");
    }

    // ------------------------------------------------
    // REFLECTION BURST
    // ------------------------------------------------
    public void TriggerReflectionBurst(Color tint, int strength = 5)
    {
        if (defaultBurst == null) return;

        var main = defaultBurst.main;
        main.startColor = new ParticleSystem.MinMaxGradient(tint);

        var emission = defaultBurst.emission;
        emission.SetBursts(new ParticleSystem.Burst[]
        {
            new ParticleSystem.Burst(0f, (short)Mathf.Clamp(strength, 1, 50))
        });

        defaultBurst.Play();
    }

    // ------------------------------------------------
    // TRAIL (OPTIONAL)
    // ------------------------------------------------
    public void SpawnTrail(Color tint)
    {
        ParticleSystem trailSystem = GetComponentInChildren<ParticleSystem>();

        if (trailSystem == null)
        {
            Debug.LogWarning("[Particles] No trail system assigned.");
            return;
        }

        var main = trailSystem.main;
        main.startColor = new ParticleSystem.MinMaxGradient(tint);

        trailSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        trailSystem.Play();
    }

    // ------------------------------------------------
    // 🔥 FIRELY SYSTEM (AMBIENT LIFE)
    // ------------------------------------------------
    public void InitializeFireflies(Color baseColor)
    {
        if (fireflySystem == null)
        {
            if (!hasLoggedMissingFireflySystem)
            {
                Debug.Log("[Particles] Firefly system not assigned; ambient fireflies disabled.");
                hasLoggedMissingFireflySystem = true;
            }

            return;
        }

        var main = fireflySystem.main;
        main.loop = true;
        main.startLifetime = 3.5f;
        main.startSpeed = 0.15f;
        main.startSize = 0.06f;
        main.startColor = new ParticleSystem.MinMaxGradient(baseColor);

        var emission = fireflySystem.emission;
        emission.rateOverTime = 14f;

        var shape = fireflySystem.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.5f;

        // Optional: add natural motion
        var noise = fireflySystem.noise;
        noise.enabled = true;
        noise.strength = 0.2f;
        noise.frequency = 0.4f;

        fireflySystem.Play();

        currentFireflyColor = baseColor;

        Debug.Log("[Particles] Fireflies initialized.");
    }

    private void UpdateFireflies()
    {
        if (fireflySystem == null) return;

        fireflyPulseTime += Time.deltaTime * 2f;

        float pulse = Mathf.Sin(fireflyPulseTime) * 0.5f + 0.5f;

        var main = fireflySystem.main;

        // soft breathing glow
        Color pulsedColor = currentFireflyColor * (0.6f + pulse * 0.6f);
        main.startColor = new ParticleSystem.MinMaxGradient(pulsedColor);
    }

    // ------------------------------------------------
    // EXTERNAL CONTROL (FROM ROUTER)
    // ------------------------------------------------
    public void SetFireflyColor(Color newColor)
    {
        currentFireflyColor = newColor;
    }
}
