using System;
using UnityEngine;

public class ElectricalSafetyManager : MonoBehaviour
{
    public static ElectricalSafetyManager Instance { get; private set; }

    public static event Action<ElectricalState> StateChanged;
    public static event Action<string, bool> MessageRaised;   // text, isUnsafe

    [Header("Rules")]
    public int requiredPpeItems = 2;
    public int requiredHazards = 3;
    public int maxUnsafeActions = 3;

    public ElectricalState State { get; private set; } = ElectricalState.Energised;
    public bool LockoutApplied { get; private set; }
    public bool CoverOpen { get; private set; }
    public bool ToolsClear { get; private set; }
    public int UnsafeCount { get; private set; }
    public int HazardsFound { get; private set; }
    public int PpeWorn { get; private set; }
    public int TotalHazards { get; private set; }

    bool ppeChecked, lockoutChecked, fuseRemoved;
    int liveAttempts;

    void Awake()
    {
        Instance = this;
        TotalHazards = FindObjectsByType<Hazard>(FindObjectsSortMode.None).Length;
    }

    bool Running => SessionManager.Instance != null && SessionManager.Instance.IsRunning;
    bool PreCheck => SessionManager.Instance != null && SessionManager.Instance.IsPreCheck;

    public void SetElectricalState(ElectricalState newState)
    {
        State = newState;
        StateChanged?.Invoke(State);
    }

    // ---------- pre-work check ----------

    public void PpeItemWorn(bool isRequiredItem)
    {
        if (!PreCheck) return;
        if (isRequiredItem)
        {
            PpeWorn++;
            Info("PPE on: " + PpeWorn + " of " + requiredPpeItems);
        }
        else Info("That item is not suitable for electrical work.");
    }

    public void HazardIdentified(string hazardName)
    {
        if (!PreCheck) return;
        HazardsFound++;
        Info("Hazard reported: " + hazardName + " (" + HazardsFound + " of " + TotalHazards + ")");
    }

    // Called once when training starts: scores what the pre-work check achieved.
    public void ScorePreCheck(int attempts)
    {
        ppeChecked = true;
        if (PpeWorn >= requiredPpeItems) Score("PPE", true);
        else Unsafe("PPE", "You started the job wearing " + PpeWorn + " of " + requiredPpeItems + " PPE items.", false);

        bool enoughHazards = HazardsFound >= requiredHazards;
        string note = enoughHazards ? "" :
            "You reported " + HazardsFound + " of " + TotalHazards + " hazards before starting work.";
        SessionManager.Instance.RegisterAction("Hazards", enoughHazards, note);

        if (attempts == 0)
            SessionManager.Instance.UnsafeNotes.Add("You started the job without a pre-work check.");
    }

    // ---------- the job ----------

    public bool TryIsolate()
    {
        if (!Running) return false;
        CheckPpeOnce();
        if (LockoutApplied) { Info("The breaker is locked. Remove the tag first."); return false; }
        Score("Isolate", true);
        SetElectricalState(ElectricalState.Isolated);
        Info("Supply switched off.");
        return true;
    }

    public bool TryRestore()
    {
        if (!Running) return false;
        if (LockoutApplied) { Info("The breaker is locked out. Remove the tag first."); return false; }
        if (CoverOpen)
        {
            Unsafe("Restore", "Power restored while the cover was open.", true);
            return true;
        }
        if (State == ElectricalState.Repaired)
        {
            if (!ToolsClear)
            {
                Unsafe("Restore", "Return the fuse puller to the rack before restoring power.", false);
                return false;
            }
            Score("Restore", true);
            SetElectricalState(ElectricalState.Restored);
            return true;
        }
        Info("Power is back on but the repair is not finished. Isolate again.");
        SetElectricalState(ElectricalState.Energised);
        return true;
    }

    public void WrongBreaker()
    {
        if (!Running) return;
        CheckPpeOnce();
        Unsafe("Isolate", "Wrong isolator. Check the job card for the correct supply.", false);
    }

    public void SetLockout(bool applied)
    {
        LockoutApplied = applied;
        if (!Running) return;
        if (applied && State == ElectricalState.Energised)
            Info("The tag is on, but the breaker is still ON. Remove the tag, switch off, then tag.");
        else if (applied) Info("Lockout tag applied.");
        else if (CoverOpen) Unsafe("Lockout", "Lockout tag removed while the cover was open.", false);
        else Info("Lockout tag removed.");
    }

    // Returns true when the tester should show "dead" (0 V).
    public bool TryVerify()
    {
        if (!Running) return false;
        CheckPpeOnce();
        if (State == ElectricalState.Energised)
        {
            Info("Tester reads LIVE. Isolate the supply first.");
            return false;
        }
        if (State == ElectricalState.Isolated)
        {
            if (!LockoutApplied)
            {
                Info("Tester reads 0 V. Apply the lockout tag, then test again.");
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

    public bool TryOpenCover()
    {
        if (!Running) return false;
        CheckPpeOnce();
        if (State == ElectricalState.Energised)
        {
            liveAttempts++;
            Unsafe("Isolate", "Do not open the cover while the workstation is live.", liveAttempts >= 2);
            return false;
        }
        if (!lockoutChecked)
        {
            lockoutChecked = true;
            if (LockoutApplied) Score("Lockout", true);
            else Unsafe("Lockout", "You went to open the cover without a lockout tag on the breaker.", false);
        }
        if (State == ElectricalState.Isolated)
        {
            Unsafe("Verify", "Test for voltage before opening the cover.", false);
            return false;
        }
        CoverOpen = true;
        return true;
    }

    public void CoverClosed() { CoverOpen = false; }

    public bool TryRemoveFuse()
    {
        if (!Running || !CoverOpen || State != ElectricalState.VerifiedSafe) return false;
        fuseRemoved = true;
        Info("Damaged fuse removed. Fit the new fuse.");
        return true;
    }

    public void FuseInserted()
    {
        if (!Running || !fuseRemoved || State != ElectricalState.VerifiedSafe) return;
        SetElectricalState(ElectricalState.Repaired);
        Info("New fuse fitted. Close the cover, clear your tools, then restore the supply.");
    }

    public void SetToolsClear(bool clear) { ToolsClear = clear; }

    void CheckPpeOnce()
    {
        if (ppeChecked) return;
        ppeChecked = true;
        if (PpeWorn >= requiredPpeItems) Score("PPE", true);
        else Unsafe("PPE", "You started work without full PPE.", false);
    }

    void Score(string decision, bool correct)
    {
        SessionManager.Instance.RegisterAction(decision, correct);
    }

    void Info(string text) { MessageRaised?.Invoke(text, false); }

    void Unsafe(string decision, string text, bool critical)
    {
        if (!Running) return;
        UnsafeCount++;
        SessionManager.Instance.RegisterAction(decision, false, text);
        MessageRaised?.Invoke(text, true);
        if (critical || UnsafeCount >= maxUnsafeActions) SessionManager.Instance.EndSession(false);
    }
}