using UnityEngine;
using System.Collections.Generic;

public class EmotionRingRenderer : MonoBehaviour
{
    [Header("Ring Settings")]
    public GameObject ringPrefab;                 // Prefab with emission shader and pulse logic
    public Transform armudaCenter;                // Center anchor for orbit
    public float maxLifetime = 10f;
    public float spinSpeed = 15f;

    private Dictionary<string, Color> emotionColors = new()
    {
        { "joy", new Color(1f, 0.84f, 0.3f) },
        { "curious", Color.cyan },
        { "conflicted", new Color(1f, 0.3f, 0.3f) },
        { "inspired", new Color(0.6f, 0.4f, 1f) },
        { "sad", new Color(0.4f, 0.5f, 1f) },
        { "rest", Color.gray },
        { "analytical", new Color(0.4f, 1f, 0.6f) },
        { "neutral", Color.white }
    };

    public void RenderEmotionRing(string category, string emotion, float intensity = 1f)
    {
        if (ringPrefab == null || armudaCenter == null) return;

        Color color = emotionColors.ContainsKey(emotion.ToLower()) ? emotionColors[emotion.ToLower()] : Color.gray;

        GameObject ring = Instantiate(ringPrefab, armudaCenter);
        ring.name = $"EmotionRing_{category}_{emotion}_{System.DateTime.Now:HHmmss}";

        // Position and scale
        float offset = Random.Range(-0.75f, 0.75f);
        ring.transform.localPosition = new Vector3(offset, Random.Range(-0.25f, 0.25f), offset);
        ring.transform.localScale = Vector3.one * Mathf.Lerp(1.2f, 3.5f, intensity);

        // Inline spinner logic
        float rotationSpeed = spinSpeed * Mathf.Lerp(0.5f, 2f, intensity);
        ring.AddComponent<EmotionRingRotation>().rotationSpeed = rotationSpeed;

        // Material effects
        Renderer rend = ring.GetComponent<Renderer>();
        if (rend && rend.material.HasProperty("_EmissionColor"))
        {
            rend.material.SetColor("_EmissionColor", color * 2f);
            rend.material.SetColor("_PulseColor", color);
            rend.material.SetFloat("_PulseSpeed", Mathf.Lerp(1f, 3f, intensity));
        }

        Destroy(ring, maxLifetime); // Auto-fade and cleanup
    }

    private class EmotionRingRotation : MonoBehaviour
    {
        public float rotationSpeed = 15f;

        void Update()
        {
            transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.Self);
        }
    }
}
