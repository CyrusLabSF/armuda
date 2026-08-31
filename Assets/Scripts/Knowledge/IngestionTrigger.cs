using UnityEngine;

public class IngestionTrigger : MonoBehaviour
{
    [Header("Choose which stage to run on startup")]
    public string stageToRun = "Fundamentals";

    [Header("Run all stages instead of one?")]
    public bool runAllStages = false;

    [Header("Delay start (seconds) to avoid burst load")]
    public float startDelay = 3f;

    private ArTusApiManager manager;

    void Start()
    {
        manager = FindAnyObjectByType<ArTusApiManager>();
        if (manager == null)
        {
            Debug.LogError("[IngestionTrigger] No ArTusApiManager found in scene.");
            return;
        }

        // Kick off ingestion with a slight delay
        Invoke(nameof(TriggerIngestion), startDelay);
    }

    private void TriggerIngestion()
    {
        if (runAllStages)
        {
            Debug.Log("[IngestionTrigger] Running ALL API stages...");
            manager.RunAllStages();
        }
        else
        {
            Debug.Log($"[IngestionTrigger] Running stage: {stageToRun}");
            manager.RunStage(stageToRun);
        }
    }
}
