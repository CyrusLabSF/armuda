// Step 1: PerformanceManager.cs
// Place in your Core/Managers or Scripts folder
using UnityEngine;

public class PerformanceManager : MonoBehaviour
{
    public static PerformanceManager Instance;

    [Header("Global Performance Flags")]
    public bool allowUnityVisualization = false;
    public bool allowSimulation = true;
    public bool allowBeliefPromotion = true;
    public bool allowFileWrites = true;
    public bool debugMode = false;
    public float contradictionScanInterval = 5f;
    public bool allowVoice = true; // Or false by default

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
        DontDestroyOnLoad(gameObject);
    }
}
