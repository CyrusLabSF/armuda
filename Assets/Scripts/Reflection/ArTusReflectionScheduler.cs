using UnityEngine;
using System;
using System.Collections;

/// <summary>
/// Runs ArTusReflectionEngine.ReflectOnCycle() on a schedule.
/// SAFE: does not define reflection logic, only calls it.
/// </summary>
public class ArTusReflectionScheduler : MonoBehaviour
{
    [Header("Daily Reflection")]
    public int dailyHour = 2;
    public int dailyMinute = 0;

    [Header("Weekly Deep Reflection")]
    public DayOfWeek deepReflectionDay = DayOfWeek.Sunday;
    public int weeklyHour = 3;
    public int weeklyMinute = 0;

    [Header("Monthly Digest")]
    public DayOfWeek monthlyReflectionDay = DayOfWeek.Sunday;
    public int monthlyHour = 4;
    public int monthlyMinute = 0;

    private ArTusReflectionEngine reflectionEngine;
    private ArTusCoreState core;

    void Start()
    {
        reflectionEngine = GetComponent<ArTusReflectionEngine>();
        core = GetComponent<ArTusCoreState>();

        if (reflectionEngine == null)
        {
            Debug.LogError("[ReflectionScheduler] ArTusReflectionEngine missing.");
            enabled = false;
            return;
        }

        StartCoroutine(ScheduleLoop());
    }

    private IEnumerator ScheduleLoop()
    {
        while (true)
        {
            DateTime now = DateTime.Now;

            // -------------------------
            // DAILY
            // -------------------------
            DateTime nextDaily = new DateTime(
                now.Year, now.Month, now.Day,
                dailyHour, dailyMinute, 0
            );

            if (nextDaily <= now)
                nextDaily = nextDaily.AddDays(1);

            // -------------------------
            // WEEKLY
            // -------------------------
            int daysUntilWeekly =
                ((int)deepReflectionDay - (int)now.DayOfWeek + 7) % 7;

            DateTime nextWeekly = new DateTime(
                now.Year, now.Month, now.Day,
                weeklyHour, weeklyMinute, 0
            ).AddDays(daysUntilWeekly);

            if (nextWeekly <= now)
                nextWeekly = nextWeekly.AddDays(7);

            // -------------------------
            // MONTHLY
            // -------------------------
            DateTime nextMonthly =
                FirstOfMonthDay(now.Year, now.Month, monthlyReflectionDay);

            if (nextMonthly <= now)
                nextMonthly = FirstOfMonthDay(
                    now.Year,
                    now.Month + 1,
                    monthlyReflectionDay
                );

            nextMonthly = new DateTime(
                nextMonthly.Year,
                nextMonthly.Month,
                nextMonthly.Day,
                monthlyHour,
                monthlyMinute,
                0
            );

            // -------------------------
            // SELECT NEXT EVENT
            // -------------------------
            DateTime nextEvent = nextDaily;
            string type = "daily";

            if (nextWeekly < nextEvent)
            {
                nextEvent = nextWeekly;
                type = "weekly";
            }

            if (nextMonthly < nextEvent)
            {
                nextEvent = nextMonthly;
                type = "monthly";
            }

            // -------------------------
            // SAFE WAIT LOOP (FIXED)
            // -------------------------
            float waitTime = Mathf.Max(1f, (float)(nextEvent - now).TotalSeconds);

            while (waitTime > 0f)
            {
                float step = Mathf.Min(60f, waitTime); // check every minute
                yield return new WaitForSeconds(step);
                waitTime -= step;
            }

            // -------------------------
            // EXECUTE REFLECTION
            // -------------------------
            try
            {
                Debug.Log($"[ReflectionScheduler] Running {type} reflection");

                core?.LogMemory(
                    $"🧠 Preparing {type} reflection cycle",
                    "ReflectionPrep",
                    2,
                    "focused"
                );

                // NOTE:
                // Your ReflectionEngine should support this overload:
                // ReflectOnCycle(string type)
                reflectionEngine.ReflectOnCycle();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ReflectionScheduler] Reflection failed: {ex.Message}");

                core?.LogMemory(
                    $"Reflection scheduler error: {ex.Message}",
                    "ReflectionScheduler",
                    2,
                    "alert"
                );
            }

            yield return new WaitForSeconds(1f);
        }
    }

    // ------------------------------------------------
    // MONTH HELPER
    // ------------------------------------------------
    private static DateTime FirstOfMonthDay(int year, int month, DayOfWeek targetDay)
    {
        if (month > 12)
        {
            year++;
            month = 1;
        }

        DateTime first = new DateTime(year, month, 1);

        int offset =
            ((int)targetDay - (int)first.DayOfWeek + 7) % 7;

        return first.AddDays(offset);
    }
}