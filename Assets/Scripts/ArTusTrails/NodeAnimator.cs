using UnityEngine;
using System.Collections;

public class NodeAnimator : MonoBehaviour
{
    [Header("Visual Settings")]
    public Renderer nodeRenderer;
    public float pulseSpeed = 2f;
    public float maxGlow = 3f;
    public float baseScale = 1f;

    [Header("Emotion + State")]
    public string emotionState = "neutral";
    public bool isContradicted = false;
    public float clarityValue = 1f;

    [Header("Decay Settings")]
    public bool decayActive = false;
    public float decayTimer = 0f;
    public float decayRate = 0.2f;

    private Material mat;
    private Vector3 initialScale;

    void Start()
    {
        if (nodeRenderer == null)
            nodeRenderer = GetComponent<Renderer>();

        if (nodeRenderer != null)
            mat = nodeRenderer.material;

        initialScale = transform.localScale;
        UpdateEmotionColor();
    }

    void Update()
    {
        // 💡 Glow Pulse
        float glow = Mathf.Sin(Time.time * pulseSpeed) * 0.5f + 0.5f;
        Color pulseColor = GetBlendedEmotionColor() * Mathf.Lerp(1f, maxGlow, glow * clarityValue);

        if (mat != null)
        {
            mat.SetColor("_EmissionColor", pulseColor);
            mat.EnableKeyword("_EMISSION");
        }

        // 🌊 Subtle pulse scale
        float scalePulse = Mathf.Sin(Time.time * pulseSpeed) * 0.05f;
        transform.localScale = initialScale * baseScale * (1f + scalePulse);

        // ⚠ Contradiction flicker
        if (isContradicted && mat != null)
        {
            float flicker = Mathf.PingPong(Time.time * 10f, 1f);
            Color conflictColor = Color.Lerp(pulseColor, Color.red, flicker);
            mat.SetColor("_EmissionColor", conflictColor);
        }

        // 🕳 Decay shrink
        if (decayActive)
        {
            decayTimer += Time.deltaTime;
            float decayFactor = Mathf.Clamp01(1f - decayTimer * decayRate);
            transform.localScale = initialScale * decayFactor;
            if (decayFactor < 0.05f)
                Destroy(gameObject); // Cleanup
        }
    }

    public void BeginDecay()
    {
        decayActive = true;
        decayTimer = 0f;
    }

    public void SetDecayByAge(float age)
    {
        decayActive = age > 60f;
        decayTimer = Mathf.Clamp(age - 60f, 0f, 999f);
    }

    public void UpdateEmotionColor()
    {
        if (mat == null) return;

        Color baseColor;
        if (System.Enum.TryParse(emotionState, true, out ArTusEmotionController.EmotionState parsed))
        {
            baseColor = ArTusEmotionData.GetColorForEmotion(parsed);
        }
        else
        {
            baseColor = Color.gray;
        }

        mat.color = baseColor;
        mat.SetColor("_EmissionColor", baseColor * maxGlow);
    }

    private Color GetBlendedEmotionColor()
    {
        if (System.Enum.TryParse(emotionState, true, out ArTusEmotionController.EmotionState parsed))
        {
            return ArTusEmotionData.GetColorForEmotion(parsed);
        }

        return new Color(0.5f, 0.5f, 0.5f); // fallback
    }

    public void SyncFromNode(ThoughtPathNode node)
    {
        if (node == null) return;

        emotionState = node.emotion ?? "neutral";
        isContradicted = node.contradictionFlag;
        clarityValue = Mathf.Clamp01(node.confidence / 10f);
        baseScale = 1f + Mathf.Clamp(node.importanceScore / 5f, 0.5f, 2f);
        SetDecayByAge(node.ageInSeconds);
        UpdateEmotionColor();
    }

    public void TriggerGlow()
    {
        Debug.Log("[NodeAnimator] Glow triggered.");
    }

    public void DrawThreadLine(Transform target)
    {
        Debug.Log("[NodeAnimator] Thread line drawn.");
    }

    public void SpawnContradictionRipple()
    {
        Debug.Log("[NodeAnimator] Contradiction ripple spawned.");
    }
}
