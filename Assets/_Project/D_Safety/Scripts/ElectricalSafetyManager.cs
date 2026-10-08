
using System;
using System.Collections.Generic;
using UnityEngine;

public class ElectricalSafetyManager : MonoBehaviour
{
    public static ElectricalSafetyManager Instance { get; private set; }

    // Events used by the warning display, audio and UI.
    public static event Action<ElectricalState> StateChanged;
    public static event Action<string, bool> MessageRaised;

    [Header("Rules")]
    public int requiredPpeItems = 6;
    public int requiredHazards = 4;
    public int maxUnsafeActions = 3;

    public ElectricalState State { get; private set; }
        = ElectricalState.Energised;

    public bool LockoutApplied { get; private set; }
    public bool CoverOpen { get; private set; }
    public bool ToolsClear { get; private set; }

    public int UnsafeCount { get; private set; }
    public int HazardsFound { get; private set; }
    public int PpeWorn { get; private set; }

    bool ppeChecked;
    bool lockoutChecked;
    bool fuseRemoved;

    int liveAttempts;
    int legacyPpeSequence;

    // Track unique PPE items.
    readonly HashSet<string> wornPpeItems =
        new HashSet<string>();

    // Track unique reported hazards.
    readonly HashSet<string> identifiedHazards =
        new HashSet<string>();

    void Awake()
    {
        Instance = this;
    }

    bool Running =>
        SessionManager.Instance != null &&
        SessionManager.Instance.IsRunning;

    // ---------- ELECTRICAL STATE ----------

    public void SetElectricalState(ElectricalState newState)
    {
        State = newState;
        StateChanged?.Invoke(State);
    }

    // ---------- PPE ----------

    // Retained for compatibility with Member C's existing code.
    // Use the named overload for final PPE interactables.
    public void PpeItemWorn(bool isRequiredItem)
    {
        if (!Running) return;

        if (!isRequiredItem)
        {
            PpeItemWorn("Incorrect PPE", false);
            return;
        }

        legacyPpeSequence++;

        PpeItemWorn(
            "Legacy PPE " + legacyPpeSequence,
            true
        );
    }

    public void PpeItemWorn(string itemName, bool isRequiredItem)
    {
        if (!Running) return;

        // Scenario 2: Incorrect PPE.
        if (!isRequiredItem)
        {
            Unsafe(
                "PPE",
                "Incorrect PPE! Select protective equipment suitable for electrical work.",
                false
            );
            return;
        }

        if (string.IsNullOrWhiteSpace(itemName))
            return;

        // Prevent duplicate counting.
        if (wornPpeItems.Contains(itemName))
        {
            Info("[GUIDANCE] This PPE item is already worn.");
            return;
        }

        if (PpeWorn >= requiredPpeItems)
        {
            Info("[GUIDANCE] All required PPE items are already worn.");
            return;
        }

        wornPpeItems.Add(itemName);
        PpeWorn = wornPpeItems.Count;

        // Scenario 3: Incomplete PPE.
        if (PpeWorn < requiredPpeItems)
        {
            Info(
                "[GUIDANCE] PPE incomplete. " +
                PpeWorn + " of " + requiredPpeItems +
                " items worn. Check your remaining protective equipment."
            );
        }
        // Scenario 4: Full PPE.
        else
        {
            Info(
                "PPE check completed! All " +
                requiredPpeItems +
                " required items are worn. Proceed to hazard inspection."
            );
        }
    }

    // Scenario 5: PPE removed.
    public void PpeItemRemoved(string itemName)
    {
        if (!Running) return;

        if (wornPpeItems.Remove(itemName))
        {
            PpeWorn = wornPpeItems.Count;

            Unsafe(
                "PPE",
                "Warning! Required protective equipment has been removed. Put it back on before continuing.",
                false
            );
        }
    }

    // ---------- HAZARDS ----------

    public void HazardIdentified(string hazardName)
    {
        if (!Running) return;

        CheckPpeOnce();

        // Stop processing if the session has ended.
        if (!Running) return;

        if (string.IsNullOrWhiteSpace(hazardName))
            return;

        // Do not count the same hazard twice.
        if (identifiedHazards.Contains(hazardName))
        {
            Info("[GUIDANCE] This hazard has already been reported.");
            return;
        }

        if (HazardsFound >= requiredHazards)
        {
            Info("[GUIDANCE] All required hazards have been identified.");
            return;
        }

        identifiedHazards.Add(hazardName);
        HazardsFound = identifiedHazards.Count;

        // Scenario 3: Incomplete hazard inspection.
        if (HazardsFound < requiredHazards)
        {
            Info(
                "[GUIDANCE] Hazard reported: " +
                hazardName + " (" +
                HazardsFound + " of " +
                requiredHazards + "). " +
                "Hazard inspection incomplete. Identify all " +
                requiredHazards + " hazards."
            );
        }
        // Scenario 4: All hazards identified.
        else
        {
            Info(
                "All " + requiredHazards +
                " hazards identified! Proceed to electrical isolation."
            );
        }
    }

    // ---------- BREAKER ----------

    public bool TryIsolate()
    {
        if (!Running) return false;

        CheckPpeOnce();

        if (!Running) return false;

        if (LockoutApplied)
        {
            Info("[GUIDANCE] The breaker is locked. Remove the tag first.");
            return false;
        }

        // NEW: Hazard inspection must be completed first.
        if (HazardsFound < requiredHazards)
        {
            Unsafe(
                "Hazards",
                "Warning! Complete the hazard inspection before proceeding. " +
                "You have identified " + HazardsFound +
                " of " + requiredHazards + " hazards.",
                false
            );

            // If this was the third unsafe action, stop.
            if (!Running) return false;
        }

        Score("Isolate", true);

        SetElectricalState(ElectricalState.Isolated);

        Info("Supply switched off.");

        return true;
    }

    public bool TryRestore()
    {
        if (!Running) return false;

        if (LockoutApplied)
        {
            Info("[GUIDANCE] The breaker is locked out. Remove the tag first.");
            return false;
        }

        if (CoverOpen)
        {
            Unsafe(
                "Restore",
                "CRITICAL DANGER! Power restored while the cover was open.",
                true
            );
            return true;
        }

        if (State == ElectricalState.Repaired)
        {
            if (!ToolsClear)
            {
                Unsafe(
                    "Restore",
                    "Return the fuse puller to the rack before restoring power.",
                    false
                );
                return false;
            }

            Score("Hazards", HazardsFound >= requiredHazards);
            Score("Restore", true);

            SetElectricalState(ElectricalState.Restored);

            Info("Power restored successfully! Electrical maintenance completed.");

            return true;
        }

        Info("[GUIDANCE] Power is back on but the repair is not finished. Isolate again.");

        SetElectricalState(ElectricalState.Energised);

        return true;
    }

    public void WrongBreaker()
    {
        if (!Running) return;

        CheckPpeOnce();

        if (!Running) return;

        Unsafe(
            "Isolate",
            "Wrong breaker! Check the job card for the correct electrical supply.",
            false
        );
    }

    // ---------- LOCKOUT ----------

    public void SetLockout(bool applied)
    {
        LockoutApplied = applied;

        if (!Running) return;

        if (applied && State == ElectricalState.Energised)
        {
            Info(
                "[GUIDANCE] The tag is on, but the breaker is still ON. Remove the tag, switch off, then tag."
            );
        }
        else if (applied)
        {
            Info("Lockout tag applied.");
        }
        else if (CoverOpen)
        {
            Unsafe(
                "Lockout",
                "Lockout tag removed while the cover was open.",
                false
            );
        }
        else
        {
            Info("Lockout tag removed.");
        }
    }

    // ---------- VOLTAGE TESTER ----------

    public bool TryVerify()
    {
        if (!Running) return false;

        CheckPpeOnce();

        if (!Running) return false;

        if (State == ElectricalState.Energised)
        {
            Info("[GUIDANCE] Tester reads LIVE. Isolate the supply first.");
            return false;
        }

        if (State == ElectricalState.Isolated)
        {
            if (!LockoutApplied)
            {
                Info(
                    "[GUIDANCE] Tester reads 0 V. Apply the lockout tag, then test again."
                );
                return true;
            }

            Score("Verify", true);

            SetElectricalState(ElectricalState.VerifiedSafe);

            Info("Tester reads 0 V. Verified safe to work.");

            return true;
        }

        Info("Tester reads 0 V.");

        return true;
    }

    // ---------- EQUIPMENT COVER ----------

    public bool TryOpenCover()
    {
        if (!Running) return false;

        CheckPpeOnce();

        if (!Running) return false;

        if (State == ElectricalState.Energised)
        {
            liveAttempts++;

            Unsafe(
                "Isolate",
                "Do not open the cover while the workstation is live.",
                liveAttempts >= 2
            );

            return false;
        }

        if (!lockoutChecked)
        {
            lockoutChecked = true;

            if (LockoutApplied)
            {
                Score("Lockout", true);
            }
            else
            {
                Unsafe(
                    "Lockout",
                    "You went to open the cover without a lockout tag on the breaker.",
                    false
                );

                if (!Running) return false;
            }
        }

        if (State == ElectricalState.Isolated)
        {
            Unsafe(
                "Verify",
                "Test for voltage before opening the cover.",
                false
            );

            return false;
        }

        CoverOpen = true;

        Info("Cover opened. Use the fuse puller to remove the damaged fuse.");

        return true;
    }

    public void CoverClosed()
    {
        CoverOpen = false;

        if (Running)
        {
            Info("Equipment cover closed. Return all tools before restoring power.");
        }
    }

    // ---------- FUSE REPAIR ----------

    public bool TryRemoveFuse()
    {
        if (!Running ||
            !CoverOpen ||
            State != ElectricalState.VerifiedSafe)
        {
            return false;
        }

        fuseRemoved = true;

        Info("Damaged fuse removed. Fit the new fuse.");

        return true;
    }

    public void FuseInserted()
    {
        if (!Running ||
            !fuseRemoved ||
            State != ElectricalState.VerifiedSafe)
        {
            return;
        }

        SetElectricalState(ElectricalState.Repaired);

        Info(
            "New fuse fitted. Close the cover, clear your tools, then restore the supply."
        );
    }

    // ---------- TOOLS ----------

    public void SetToolsClear(bool clear)
    {
        ToolsClear = clear;

        if (Running && clear)
        {
            Info("Tools returned successfully.");
        }
    }

    // ---------- SAFETY CHECKS ----------

    void CheckPpeOnce()
    {
        if (ppeChecked) return;

        ppeChecked = true;

        if (PpeWorn >= requiredPpeItems)
        {
            Score("PPE", true);
        }
        else if (PpeWorn == 0)
        {
            Unsafe(
                "PPE",
                "Warning! Wear all required PPE before starting electrical work.",
                false
            );
        }
        else
        {
            Unsafe(
                "PPE",
                "Warning! You started work with incomplete PPE. All " +
                requiredPpeItems + " required items must be worn.",
                false
            );
        }
    }

    // ---------- HELPERS ----------

    void Score(string decision, bool correct)
    {
        SessionManager.Instance.RegisterAction(decision, correct);
    }

    void Info(string text)
    {
        MessageRaised?.Invoke(text, false);
    }

    void Unsafe(string decision, string text, bool critical)
    {
        if (!Running) return;

        UnsafeCount++;

        SessionManager.Instance.RegisterAction(
            decision,
            false,
            text
        );

        MessageRaised?.Invoke(text, true);

        if (critical || UnsafeCount >= maxUnsafeActions)
        {
            SessionManager.Instance.EndSession(false);
        }
    }
}
