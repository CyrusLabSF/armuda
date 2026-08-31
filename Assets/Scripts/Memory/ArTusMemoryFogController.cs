using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class ArTusMemoryFogController : MonoBehaviour
{
    public ParticleSystem fogSystem;
    public Material fogMaterial;
    private ParticleSystem.ColorOverLifetimeModule colorModule;

    [Header("Behavior Settings")]
    public bool allowEmotionControl = true;
    public float baseIntensity = 0.5f;

    private float currentFogIntensity = 0.5f;

    public float GetFogIntensity()
    {
        return currentFogIntensity; // or whatever variable you use internally
    }

    private void Awake()
    {
        if (fogSystem != null)
            colorModule = fogSystem.colorOverLifetime;
    }

    void Start()
    {
        LoadPreviousFogState();
    }

    // 🌫 External trigger (EmotionController or CoreState)
    public void UpdateFogVisuals(string dominantEmotion)
    {
        if (!allowEmotionControl || string.IsNullOrWhiteSpace(dominantEmotion)) return;

        Color fogColor = GetColorForEmotion(dominantEmotion);
        SetFogColor(fogColor, baseIntensity);

        if (fogMaterial != null && fogMaterial.HasProperty("_ReflectionPulse"))
        {
            fogMaterial.SetColor("_ReflectionPulse", fogColor);
            fogMaterial.SetFloat("_PulseIntensity", 1.2f);
        }

        Debug.Log($"[FogController] Emotion-linked fog updated to '{dominantEmotion}'");
    }

    public void SetFogColor(Color fogColor, float intensity = 0.5f)
    {
        if (fogSystem == null) return;

        intensity = Mathf.Clamp01(intensity);
        currentFogIntensity = intensity; // ✅ TRACK IT

        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(fogColor, 0f),
                new GradientColorKey(new Color(fogColor.r, fogColor.g, fogColor.b, 0f), 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(intensity, 0f),
                new GradientAlphaKey(0f, 1f)
            });

        colorModule.color = new ParticleSystem.MinMaxGradient(gradient);

        PlayerPrefs.SetString("LastFogColor", ColorUtility.ToHtmlStringRGBA(fogColor));

        if (!fogSystem.isPlaying)
        {
            fogSystem.Clear(true);
            fogSystem.Play();
        }
    }

    public void UpdateFogFromEmotionHistory(Dictionary<string, float> emotionCounts, int totalMemories)
    {
        if (fogSystem == null || emotionCounts == null || emotionCounts.Count == 0) return;

        string dominant = emotionCounts
            .OrderByDescending(kv => kv.Value)
            .Select(kv => kv.Key)
            .FirstOrDefault();

        Color fogColor = GetColorForEmotion(dominant);

        if (emotionCounts.ContainsKey("joy") && emotionCounts.ContainsKey("sad"))
            fogColor = Color.magenta;

        float intensity = Mathf.Clamp(totalMemories / 25f, 0.2f, 1f);
        SetFogColor(fogColor, intensity);
    }

    public void FadeFogByActivity(float activityScore)
    {
        if (fogSystem == null) return;

        float alpha = Mathf.Clamp(activityScore / 10f, 0.05f, 0.7f);

        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(alpha, 0f),
                new GradientAlphaKey(0f, 1f)
            });

        colorModule.color = new ParticleSystem.MinMaxGradient(gradient);
    }

    private void LoadPreviousFogState()
    {
        if (PlayerPrefs.HasKey("LastFogColor"))
        {
            string hex = PlayerPrefs.GetString("LastFogColor");
            if (ColorUtility.TryParseHtmlString("#" + hex, out Color savedColor))
            {
                SetFogColor(savedColor, baseIntensity);
            }
        }
    }

    private Color GetColorForEmotion(string emotion)
    {
        return emotion.ToLower() switch
        {
            "joy" => Color.yellow,
            "sad" => Color.blue * 0.5f,
            "alert" => Color.red,
            "curious" => Color.cyan,
            "growing" => Color.green,
            "thinking" => Color.white * 0.7f,
            _ => Color.gray
        };
    }

    public void BeginReflectionPulse(string emotion)
    {
        if (fogMaterial == null) return;

        if (fogMaterial.HasProperty("_ReflectionPulse"))
        {
            Color pulseColor = EmotionToColor(emotion);
            fogMaterial.SetColor("_ReflectionPulse", pulseColor);
            fogMaterial.SetFloat("_PulseIntensity", 1.5f);

            Debug.Log($"[FogController] 🌫️ Reflection pulse triggered for emotion: {emotion}");
        }
        else
        {
            Debug.LogWarning("[FogController] Reflection pulse material or property not found.");
        }
    }

    public void PulseFogBurst(float clarity, int beliefCount)
    {
        if (fogMaterial == null) return;

        float intensity = Mathf.Clamp01(1f - clarity);
        float pulseSize = Mathf.Clamp(beliefCount / 100f, 0.2f, 1.5f);

        fogMaterial.SetFloat("_PulseStrength", intensity * 2f);
        fogMaterial.SetFloat("_PulseSize", pulseSize);

        Debug.Log($"[Fog] Burst sync: Clarity={clarity:F2}, Beliefs={beliefCount}");
    }

    private Color EmotionToColor(string emotion)
    {
        return emotion.ToLower() switch
        {
            "joy" => new Color(1f, 0.8f, 0.2f),
            "sad" => new Color(0.4f, 0.5f, 1f),
            "curious" => new Color(0.2f, 1f, 1f),
            "alert" => new Color(1f, 0.3f, 0.3f),
            _ => Color.gray
        };
    }
}
