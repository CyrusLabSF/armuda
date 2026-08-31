using UnityEngine;

public static class ArTusEmotionData
{
    public static Color GetColorForEmotion(ArTusEmotionController.EmotionState state)
    {
        return state switch
        {
            ArTusEmotionController.EmotionState.joy => Color.yellow,
            ArTusEmotionController.EmotionState.sad => Color.blue,
            ArTusEmotionController.EmotionState.alert => Color.red,
            ArTusEmotionController.EmotionState.curious => Color.cyan,
            ArTusEmotionController.EmotionState.thinking => Color.magenta,
            _ => Color.white
        };
    }

    public static float GetPulseSpeed(ArTusEmotionController.EmotionState state)
    {
        return state switch
        {
            ArTusEmotionController.EmotionState.alert => 2.0f,
            ArTusEmotionController.EmotionState.joy => 1.5f,
            ArTusEmotionController.EmotionState.sad => 0.5f,
            _ => 1f
        };
    }

    public static float GetRippleStrength(ArTusEmotionController.EmotionState state)
    {
        return state switch
        {
            ArTusEmotionController.EmotionState.joy => 1.2f,
            ArTusEmotionController.EmotionState.alert => 1.5f,
            ArTusEmotionController.EmotionState.sad => 0.6f,
            _ => 1f
        };
    }

    public static float GetEmotionIntensity(ArTusEmotionController.EmotionState state)
    {
        return state switch
        {
            ArTusEmotionController.EmotionState.alert => 1.5f,
            ArTusEmotionController.EmotionState.joy => 1.2f,
            ArTusEmotionController.EmotionState.sad => 0.8f,
            ArTusEmotionController.EmotionState.thinking => 0.9f,
            _ => 1f
        };
    }
}
