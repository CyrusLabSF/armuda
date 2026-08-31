public static class ArTusRuntimeProfile
{
#if UNITY_WEBGL && !UNITY_EDITOR
    public const bool IsWebGL = true;
#else
    public const bool IsWebGL = false;
#endif
}
