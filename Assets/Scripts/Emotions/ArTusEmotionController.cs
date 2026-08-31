using UnityEngine;
using UnityEngine.Events;
using System;
using System.Collections.Generic;

/// <summary>
/// 🧠 ArTus Emotion Controller — Hi-Class Final
/// ------------------------------------------------
/// Core emotional state machine for ArTus.
/// - Enforces dwell time (emotional inertia)
/// - Tracks emotion reason (reflective awareness)
/// - Supports pressure-based emotion emergence
/// - Emits events for visuals / audio / fog (external)
///
/// ❗ No visuals, particles, or side effects live here.
/// </summary>
public class ArTusEmotionController : MonoBehaviour
{
    public static ArTusEmotionController Instance { get; private set; }
    public static bool IsReady => Instance != null;

    // =========================================================
    // EMOTION ENUM
    // =========================================================
    public enum EmotionState
    {
        joy,
        alert,
        curious,
        thinking,
        sad,
        idle,
        rest,
        bored,
        growing,
        cleaning,
        lonely,
        neutral
    }

    // =========================================================
    // SETTINGS
    // =========================================================
    [Header("Core Settings")]
    [Tooltip("Minimum time (seconds) before an emotion may change again.")]
    public float minEmotionDwellTime = 15f;

    [Tooltip("How often emotion pressure decays (seconds).")]
    public float pressureTickInterval = 1.5f;

    [Header("Ambient (Firefly)")]
    [SerializeField] private ParticleSystem fireflySystem;

    private Color currentFireflyColor = Color.cyan;
    private float fireflyPulseTime;

    // =========================================================
    // EVENTS
    // =========================================================
    [System.Serializable]
    public class EmotionChangedEvent : UnityEvent<EmotionState> { }

    [Header("Events")]
    public EmotionChangedEvent OnEmotionChanged;

    // =========================================================
    // INTERNAL STATE
    // =========================================================
    private EmotionState currentEmotion = EmotionState.neutral;
    private float lastEmotionChangeTime = 0f;
    private string lastEmotionReason = "init";

    [SerializeField]
    private string currentEmotionName = "thinking";

    // =========================================================
    // PRESSURE MODEL
    // =========================================================
    [Serializable]
    public class EmotionPressure
    {
        public float value;
        public float decayRate;

        public EmotionPressure(float decay)
        {
            value = 0f;
            decayRate = decay;
        }

        public void Decay(float deltaTime)
        {
            value = Mathf.Max(0f, value - decayRate * deltaTime);
        }
    }

    private Dictionary<EmotionState, EmotionPressure> emotionPressures;
    private float lastPressureTick;

    // =========================================================
    // UNITY LIFECYCLE
    // =========================================================
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;

        InitializePressures();
        currentEmotionName = currentEmotion.ToString();
    }

    private void Update()
    {
        TickPressureDecay();
        EvaluateDominantEmotion(); // 🔥 ADD THIS
    }

    // =========================================================
    // INITIALIZATION
    // =========================================================
    private void InitializePressures()
    {
        emotionPressures = new Dictionary<EmotionState, EmotionPressure>();

        foreach (EmotionState state in Enum.GetValues(typeof(EmotionState)))
        {
            // Default decay — can be tuned per emotion later
            emotionPressures[state] = new EmotionPressure(decay: 0.04f);
        }
    }

    // =========================================================
    // PUBLIC ACCESSORS
    // =========================================================
    public EmotionState CurrentEmotion => currentEmotion;
    public string LastEmotionReason => lastEmotionReason;

    public float GetPressure(EmotionState state)
    {
        return emotionPressures.TryGetValue(state, out var p) ? p.value : 0f;
    }

    public string GetCurrentEmotionName()
    {
        // If you already track emotion as string
        return currentEmotionName;

        // OR if using enum:
        // return currentEmotion.ToString();
    }

    // =========================================================
    // EMOTION SETTERS
    // =========================================================
    /// <summary>
    /// Sets the active emotion, enforcing dwell time unless forced.
    /// </summary>
    public void SetEmotion(
        EmotionState state,
        string reason = "unspecified",
        bool forceApply = false
    )
    {
        float now = Time.time;

        if (!forceApply)
        {
            if (state == currentEmotion)
                return;

            if (now - lastEmotionChangeTime < minEmotionDwellTime)
                return;
        }

        if (now - lastEmotionChangeTime < minEmotionDwellTime)
        {
            Debug.Log($"[EmotionController] Blocked emotion change → dwell time active");
            return;
        }

        currentEmotion = state;
        currentEmotionName = state.ToString();
        lastEmotionChangeTime = now;
        lastEmotionReason = reason;

        OnEmotionChanged?.Invoke(state);
        Debug.Log($"[EmotionController] 🎭 Emotion set to {state} | Reason: {reason}");
    }

    /// <summary>
    /// String-based emotion setter (safe for CLI / LLM / reflection layers).
    /// </summary>
    public void SetEmotionByName(string name, string reason = "external", bool force = false)
    {
        if (string.Equals(name, "reflective", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, "reflection", StringComparison.OrdinalIgnoreCase))
        {
            SetEmotion(EmotionState.thinking, reason, force);
            return;
        }

        if (string.Equals(name, "satisfied", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, "reassured", StringComparison.OrdinalIgnoreCase))
        {
            SetEmotion(EmotionState.joy, reason, force);
            return;
        }

        if (string.Equals(name, "uncertain", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, "uncertainty", StringComparison.OrdinalIgnoreCase))
        {
            SetEmotion(EmotionState.thinking, reason, force);
            return;
        }

        if (Enum.TryParse(name, true, out EmotionState parsed))
            SetEmotion(parsed, reason, force);
        else
            Debug.LogWarning($"[EmotionController] Unknown emotion: {name}");
    }

    // =========================================================
    // PRESSURE MANAGEMENT
    // =========================================================
    /// <summary>
    /// Apply emotional pressure without forcing an immediate switch.
    /// Used by beliefs, reflections, contradictions, curiosity spikes.
    /// </summary>
    public void AddPressure(EmotionState state, float amount)
    {
        if (!emotionPressures.ContainsKey(state))
            return;

        emotionPressures[state].value = Mathf.Clamp(
            emotionPressures[state].value + Mathf.Max(0f, amount),
            0f,
            1f
        );
    }

    private void TickPressureDecay()
    {
        if (Time.time - lastPressureTick < pressureTickInterval)
            return;

        lastPressureTick = Time.time;

        foreach (var kvp in emotionPressures)
            kvp.Value.Decay(pressureTickInterval);
    }

    // =========================================================
    // DEBUG / INTROSPECTION
    // =========================================================
    public Dictionary<string, float> GetPressureSnapshot()
    {
        var snapshot = new Dictionary<string, float>();
        foreach (var kvp in emotionPressures)
            snapshot[kvp.Key.ToString()] = kvp.Value.value;

        return snapshot;
    }

    private void EvaluateDominantEmotion()
    {
        EmotionState strongest = currentEmotion;
        float max = 0f;

        foreach (var kvp in emotionPressures)
        {
            if (kvp.Value.value > max)
            {
                max = kvp.Value.value;
                strongest = kvp.Key;
            }
        }

        // 🔥 Threshold prevents noise switching
        if (max > 0.6f)
        {
            SetEmotion(strongest, "pressure_dominant");
        }
    }
}
