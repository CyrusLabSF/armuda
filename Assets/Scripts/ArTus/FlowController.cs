using UnityEngine;
using System;
using System.Collections;

public class ArTusFlowController : MonoBehaviour
{
    private ArTusCoreState coreState;
    private ArTusObserver observer;
    private ArTusAuditManager auditManager;
    private ArTusScheduledDefense defense;

    [Header("Scheduling Settings")]
    public float contradictionCheckInterval = 300f;   // every 5 minutes
    public float reflectionCheckInterval = 600f;      // every 10 minutes
    public float auditInterval = 3600f;               // every hour
    public float nightSummaryHour = 2f;               // 2 AM

    private float contradictionTimer = 0f;
    private float reflectionTimer = 0f;
    private float auditTimer = 0f;

    void Start()
    {
        coreState = GetComponent<ArTusCoreState>();
        observer = GetComponent<ArTusObserver>();
        auditManager = GetComponent<ArTusAuditManager>();
        defense = GetComponent<ArTusScheduledDefense>();

        StartCoroutine(SchedulingLoop());
    }

    private IEnumerator SchedulingLoop()
    {
        while (true)
        {
            contradictionTimer += Time.deltaTime;
            reflectionTimer += Time.deltaTime;
            auditTimer += Time.deltaTime;

            // 🧠 Contradiction checks
            if (contradictionTimer >= contradictionCheckInterval)
            {
                contradictionTimer = 0f;
                coreState?.EvaluateContradictions();
            }

            // 💭 Reflection scheduling
            if (reflectionTimer >= reflectionCheckInterval)
            {
                reflectionTimer = 0f;
                coreState?.ScheduleReflection("Flow-driven reflection", "thinking");
            }

            // 📊 Audit
            if (auditTimer >= auditInterval)
            {
                auditTimer = 0f;
                auditManager?.SendMessage("RunInternalAudit"); // invoke audit
            }
        }
    }
}
