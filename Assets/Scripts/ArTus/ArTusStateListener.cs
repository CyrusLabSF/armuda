using UnityEngine;
using System;

[Serializable]
public class ArTusState
{
    public string emotion;
    public float confidence;
    public string intent;
    public float urgency;
    public string speech;
}

/// <summary>
/// ArTusStateListener
/// Receives state packets from Web / Chat and renders ArTus presence.
/// STRICTLY presentation-layer. WebGL safe.
/// </summary>
public class ArTusStateListener : MonoBehaviour
{
    [Header("References")]
    public ArTusEmotionController emotionController;
    public ArTusMemoryShellRotator shellRotator;
    public ArTusSpeechResponder speechResponder;

    [Header("Visual Tuning")]
    public float confidenceSpinMultiplier = 1.2f;
    public float urgencySpeedMin = 6f;
    public float urgencySpeedMax = 18f;

    private ArTusState currentState;

    // =========================================================
    // ENTRY POINT — CALLED FROM WEB / JS / CHAT BRIDGE
    // =========================================================
    public void ApplyState(string json)
    {
        try
        {
            var state = JsonUtility.FromJson<ArTusState>(json);
            if (state == null) return;

            currentState = state;
            RenderState(state);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ArTusStateListener] Failed to parse state: {ex.Message}");
        }
    }

    // =========================================================
    // CORE RENDERING (NO LOGIC)
    // =========================================================
    private void RenderState(ArTusState state)
    {
        // ----------------------------
        // 1️⃣ Emotion (STRING → ENUM)
        // ----------------------------
        if (emotionController != null && !string.IsNullOrEmpty(state.emotion))
        {
            if (Enum.TryParse(
                    state.emotion,
                    true,
                    out ArTusEmotionController.EmotionState parsedEmotion))
            {
                emotionController.SetEmotion(parsedEmotion);
            }
            else
            {
                Debug.LogWarning(
                    $"[ArTusStateListener] Unknown emotion '{state.emotion}'");
            }
        }

        // ----------------------------
        // 2️⃣ Confidence → spin intensity
        // ----------------------------
        if (shellRotator != null)
        {
            shellRotator.spinMultiplier =
                1f + Mathf.Clamp01(state.confidence) * confidenceSpinMultiplier;
        }

        // ----------------------------
        // 3️⃣ Urgency → base speed
        // ----------------------------
        if (shellRotator != null)
        {
            shellRotator.baseRotationSpeed =
                Mathf.Lerp(
                    urgencySpeedMin,
                    urgencySpeedMax,
                    Mathf.Clamp01(state.urgency)
                );
        }

        // ----------------------------
        // 4️⃣ Speech (SAFE GENERIC CALL)
        // ----------------------------
        if (speechResponder != null && !string.IsNullOrEmpty(state.speech))
        {
            speechResponder.TriggerVoice(state.speech);
        }

        Debug.Log(
            $"[ArTusStateListener] Applied → Emotion:{state.emotion}, " +
            $"Confidence:{state.confidence:F2}, Urgency:{state.urgency:F2}"
        );
    }
}
