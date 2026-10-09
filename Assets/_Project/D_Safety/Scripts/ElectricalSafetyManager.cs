
using System;
using System.Collections.Generic;
using UnityEngine;

// Merged: Member D (feature/electrical-safety-sanduni) + main (pre-work check flow).
// - PPE + hazards are handled in the PRE-WORK CHECK phase; ScorePreCheck() records completion at training start.
// - Member D's job rules are kept: lockout, verify, cover, fuse scenarios, wrong fuse / wrong tool,
//   tools clear, PPE removed, 3-strike rule (maxUnsafeActions) and critical danger.
// - Methods used by other scripts (Say, ProveTester, PreCheck, Running...) are kept so nothing breaks.
public class ElectricalSafetyManager : MonoBehaviour
{
    public static ElectricalSafetyManager Instance { get; private set; }

    public static event Action<ElectricalState> StateChanged;
    public static event Action<string, bool> MessageRaised;

    [Header("Rules")]
    public int requiredPpeItems = 6;
    public int requiredHazards = 4;
    public int maxUnsafeActions = 3;
    [Tooltip("If on, the voltage tester must be proven on the proving unit before a 0 V reading counts.")]
    public bool requireProvenTester = false;

    public ElectricalState State { get; private set; }
        = ElectricalState.Energised;

    public bool LockoutApplied { get; private set; }
    public bool CoverOpen { get; private set; }
    public bool ToolsClear { get; private set; }
    public bool TesterProven { get; private set; }

    public int UnsafeCount { get; private set; }
    public int HazardsFound { get; private set; }
    public int PpeWorn { get; private set; }
    public int TotalHazards { get; private set; }

    public bool Running => SessionManager.Instance != null && SessionManager.Instance.IsRunning;
    public bool PreCheck => SessionManager.Instance != null && SessionManager.Instance.IsPreCheck;

    bool ppeChecked;
    bool lockoutChecked;
    bool fuseRemoved;
    bool preCheckScored;

    int liveAttempts;
    int legacyPpeSequence;

    readonly HashSet<string> wornPpeItems =
        new HashSet<string>();

    readonly HashSet<string> identifiedHazards =
        new HashSet<string>();

    void Awake()
    {
        Instance = this;
        TotalHazards = FindObjectsByType<Hazard>(FindObjectsSortMode.None).Length;
    }

    // ---------- ELECTRICAL STATE ----------

    public void SetElectricalState(ElectricalState newState)
    {
        State = newState;
        StateChanged?.Invoke(State);
    }

    // ---------- PRE-WORK CHECK: PPE ----------

    // Old callers (PpeLocker / PpeItem) that only pass "is it a required item".
    public void PpeItemWorn(bool isRequiredItem)
    {
        if (!PreCheck) return;

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
        if (!PreCheck) return;

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

        if (PpeWorn < requiredPpeItems)
        {
            Info(
                "[GUIDANCE] PPE incomplete. " +
                PpeWorn + " of " + requiredPpeItems +
                " items worn. Check your remaining protective equipment."
            );
        }
        else
        {
            Info(
                "PPE check completed! All " +
                requiredPpeItems +
                " required items are worn. Proceed to hazard inspection."
            );
        }
    }

    
    public void PpeItemRemoved(string itemName)
    {
        if (!wornPpeItems.Remove(itemName))
            return;

        PpeWorn = wornPpeItems.Count;

        if (Running)
        {
            Unsafe(
                "PPE",
                "Warning! Required protective equipment has been removed. Put it back on before continuing.",
                false
            );
        }
        else if (PreCheck)
        {
            Info(
                "PPE removed: " +
                PpeWorn + " of " +
                requiredPpeItems + " worn."
            );
        }
    }

    // ---------- PRE-WORK CHECK: HAZARDS ----------

    public void HazardIdentified(string hazardName)
    {
        if (!PreCheck) return;
        if (string.IsNullOrWhiteSpace(hazardName)) return;

        if (identifiedHazards.Contains(hazardName))
        {
            Info("[GUIDANCE] This hazard has already been reported.");
            return;
        }

        identifiedHazards.Add(hazardName);
        HazardsFound = identifiedHazards.Count;

        // Main counts the hazard objects in the scene; fall back to the configured rule.
        int hazardTarget = TotalHazards > 0 ? TotalHazards : requiredHazards;
        if (HazardsFound < hazardTarget)
        {
            Info(
                "[GUIDANCE] Hazard reported: " + hazardName + " (" +
                HazardsFound + " of " + hazardTarget + "). " +
                "Hazard inspection incomplete. Identify all " + hazardTarget + " hazards."
            );
        }
        else
        {
            Info(
                "All " + hazardTarget +
                " hazards made safe! Finish your PPE, then start the training."
            );
        }
    }

    // Called when training begins; PPE and hazards are scored once, not at restoration.
    public void ScorePreCheck(int attempts)
    {
        if (preCheckScored || SessionManager.Instance == null) return;
        preCheckScored = true;
        ppeChecked = true;

        if (PpeWorn >= requiredPpeItems)
        {
            Score("PPE", true);
        }
        else
        {
            Unsafe(
                "PPE",
                "You started the job wearing " + PpeWorn + " of " +
                requiredPpeItems + " PPE items.",
                false
            );
        }

        bool enoughHazards = HazardsFound >= requiredHazards;
        string note = enoughHazards ? "" :
            "You made safe " + HazardsFound + " of " +
            (TotalHazards > 0 ? TotalHazards : requiredHazards) +
            " hazards before starting work.";
        SessionManager.Instance.RegisterAction("Hazards", enoughHazards, note);

        if (attempts == 0)
            SessionManager.Instance.UnsafeNotes.Add(
                "You started the job without a pre-work check."
            );
    }

    // ---------- STAGE 1: ISOLATE ----------

    public bool TryIsolate()
    {
        if (!Running) return false;
        CheckPpeOnce();
        if (!Running) return false;

        if (LockoutApplied)
        {
            Info("[GUIDANCE] The isolator is locked. Remove the tag first.");
            return false;
        }

        // Hazards were already recorded by ScorePreCheck at the start.
        Score("Isolate", true);
        SetElectricalState(ElectricalState.Isolated);
        Info("Workstation 2 isolated. Now apply your lock and DANGER tag.");
        return true;
    }

    public void WrongBreaker()
    {
        if (!Running) return;

        CheckPpeOnce();
        if (!Running) return;

        Unsafe("Isolate", "Wrong isolator! W1 feeds Workstation 1. Check the job card: isolate W2.", false);
    }

    // ---------- STAGE 2: LOCKOUT ----------

    public void SetLockout(bool applied)
    {
        LockoutApplied = applied;
        if (!Running) return;

        if (applied && State == ElectricalState.Energised)
        {
            Info("[GUIDANCE] The tag is on, but the isolator is still ON. Remove the tag, switch off, then tag.");
        }
        else if (applied)
        {
            Info("Lockout tag applied. Now prove the tester and test for dead.");
        }
        else if (CoverOpen)
        {
            Unsafe("Lockout", "Lockout tag removed while the cover was open.", false);
        }
        else
        {
            Info("Lockout tag removed.");
        }
    }

    // ---------- STAGE 3: PROVE + VERIFY ----------

    public void ProveTester()
    {
        TesterProven = true;
        if (Running)
            Info("Tester proven on the proving unit. Now test Workstation 2 for dead.");
    }

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
                Info("[GUIDANCE] Tester reads 0 V. Apply the lockout tag, then test again.");
                return true;
            }

            if (requireProvenTester && !TesterProven)
            {
                Info("[GUIDANCE] Tester reads 0 V, but prove the tester on the proving unit first, then test again.");
                return true;
            }

            Score("Verify", true);

            SetElectricalState(ElectricalState.VerifiedSafe);
            Info("Tester reads 0 V. Verified safe to work. You may open the cover.");
            return true;
        }

        Info("Tester reads 0 V.");

        return true;
    }

    // ---------- STAGE 4: EQUIPMENT COVER ----------

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

            if (LockoutApplied) Score("Lockout", true);
            else
            {
                Unsafe("Lockout", "You went to open the cover without a lockout tag on the isolator.", false);
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
            Info(
                "Equipment cover closed. Return all tools before restoring power."
            );
        }
    }

    // ---------- STAGE 5: FUSE REPAIR ----------

    // Scenario 2: Remove fuse before voltage verification.
    // Scenario 3: Remove fuse while the cover is closed.
    // Scenario 4: Successfully remove damaged fuse.

    public bool TryRemoveFuse()
    {
        if (!Running) return false;

        if (State != ElectricalState.VerifiedSafe)
        {
            Unsafe(
                "Verify",
                "DANGER! Verify the absence of voltage before removing the fuse.",
                false
            );
            return false;
        }

        if (!CoverOpen)
        {
            Unsafe(
                "Verify",
                "Warning! Open the equipment cover safely before removing the fuse.",
                false
            );
            return false;
        }

        if (fuseRemoved)
        {
            Info("[GUIDANCE] The damaged fuse has already been removed.");
            return false;
        }

        fuseRemoved = true;
        Info("Damaged fuse removed. Fit the new fuse.");
        return true;
    }

    // Scenario 5: Correct replacement.
    // Scenario 6: Insert new fuse before removing old fuse.

    public void FuseInserted()
    {
        if (!Running) return;

        if (!fuseRemoved)
        {
            Info("[GUIDANCE] Remove the damaged fuse before installing the replacement fuse.");
            return;
        }

        if (State != ElectricalState.VerifiedSafe)
        {
            Unsafe("Verify", "Warning! Verify the absence of voltage before replacing the fuse.", false);
            return;
        }

        if (!CoverOpen)
        {
            Unsafe("Verify", "Warning! Open the equipment cover before installing the replacement fuse.", false);
            return;
        }

        SetElectricalState(ElectricalState.Repaired);
        Info("New fuse fitted. Close the cover, clear your tools, then remove your lock and restore the supply.");
    }

    // ---------- STAGE 5: WRONG FUSE / WRONG TOOL ----------

    public void WrongFuseSelected()
    {
        if (!Running) return;
        Unsafe("Verify", "Incorrect replacement fuse! Check the required fuse type and rating before installation.", false);
    }

    public void WrongToolSelected()
    {
        if (!Running) return;
        Unsafe("Verify", "Incorrect tool! Use the approved fuse puller to remove the damaged fuse.", false);
    }

    // ---------- TOOLS ----------

    public void SetToolsClear(bool clear)
    {
        ToolsClear = clear;
        if (Running && clear)
            Info("Tools returned successfully.");
    }

    // ---------- STAGE 6: RESTORE ----------

    public bool TryRestore()
    {
        if (!Running) return false;

        if (LockoutApplied)
        {
            Info("[GUIDANCE] The isolator is locked out. Remove your tag first.");
            return false;
        }

        if (CoverOpen)
        {
            Unsafe("Restore", "CRITICAL DANGER! Power restored while the cover was open.", true);
            return true;
        }

        if (State == ElectricalState.Repaired)
        {
            if (!ToolsClear)
            {
                Unsafe("Restore", "Return the fuse puller to the rack before restoring power.", false);
                return false;
            }

            // Hazards were scored at pre-check; do not score them a second time here.
            Score("Restore", true);
            SetElectricalState(ElectricalState.Restored);
            Info("Power restored successfully! Electrical maintenance completed. Workstation 2 is back in service.");
            return true;
        }

        Info("[GUIDANCE] Power is back on but the repair is not finished. Isolate again.");
        SetElectricalState(ElectricalState.Energised);
        return true;
    }

    // ---------- SAFETY CHECKS ----------

    // Fallback only: normally ScorePreCheck() has scored PPE when training starts.
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

    // Public message used by other scripts (hazards, HUD prompts).
    public void Say(string text) => Info(text);

    void Score(string decision, bool correct)
    {
        SessionManager.Instance.RegisterAction(
            decision,
            correct
        );
    }

    void Info(string text)
    {
        MessageRaised?.Invoke(text, false);
    }

    void Unsafe(string decision, string text, bool critical)
    {
        if (!Running && !PreCheck) return;

        UnsafeCount++;

        SessionManager.Instance.RegisterAction(
            decision,
            false,
            text
        );

        MessageRaised?.Invoke(text, true);

        if (Running && (critical || UnsafeCount >= maxUnsafeActions))
            SessionManager.Instance.EndSession(false);
    }
}
