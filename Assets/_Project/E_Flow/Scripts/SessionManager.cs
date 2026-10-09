using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum SessionPhase { Briefing, PreCheck, Training, Result }

// Pre-work check has three stages:
//   Ppe     - all PPE must be worn (no timer yet)
//   Hazards - the timer runs; find and make safe the hazards
//   Review  - time up with hazards missed: they flash so the trainee can see them, then they are made safe
public enum PreCheckStage { Ppe, Hazards, Review }

public class SessionManager : MonoBehaviour
{
    public static SessionManager Instance { get; private set; }

    public static event Action<SessionPhase> PhaseChanged;
    public static event Action<PreCheckStage> PreCheckStageChanged;
    public static event Action ScoreChanged;
    public static event Action PreCheckTimedOut;

    public static readonly string[] DecisionKeys =
        { "PPE", "Hazards", "Isolate", "Lockout", "Verify", "Restore" };

    [Tooltip("Tick in test scenes to skip straight to training.")]
    public bool autoStartForTesting;
    public int passMark = 5;

    [Header("Pre-work check")]
    public float preCheckSeconds = 120f;
    [Tooltip("How long missed hazards flash after the time is up")]
    public float reviewSeconds = 15f;
    [Tooltip("Seconds before the end of the review when missed hazards are made safe")]
    public float reviewMakeSafeAt = 5f;

    public SessionPhase Phase { get; private set; } = SessionPhase.Briefing;
    public PreCheckStage Stage { get; private set; } = PreCheckStage.Ppe;
    public bool IsRunning => Phase == SessionPhase.Training;
    public bool IsPreCheck => Phase == SessionPhase.PreCheck;
    public bool IsReviewing => IsPreCheck && Stage == PreCheckStage.Review;
    public bool HazardHuntActive => IsPreCheck && Stage == PreCheckStage.Hazards;
    public float ElapsedSeconds { get; private set; }
    public float PreCheckSecondsLeft { get; private set; }
    public float ReviewSecondsLeft { get; private set; }
    public int PreCheckAttempts { get; private set; }
    public bool PreCheckDone { get; private set; }
    public int MissedHazards { get; private set; }
    public bool RestartedTraining { get; private set; }
    public bool Completed { get; private set; }
    public bool Passed { get; private set; }
    public readonly List<string> UnsafeNotes = new List<string>();
    public readonly List<string> PreCheckHistory = new List<string>();

    readonly Dictionary<string, bool> decisions = new Dictionary<string, bool>();
    bool reviewMadeSafe;

    // ---- kept across "Restart Training" (scene reload) ----
    static bool carryActive;
    static readonly List<string> carryPpe = new List<string>();
    static readonly Dictionary<string, bool> carryHazards = new Dictionary<string, bool>(); // true = found, false = missed
    static readonly List<string> carryHistory = new List<string>();

    void Awake() { Instance = this; }
    void OnEnable() { ElectricalSafetyManager.StateChanged += OnStateChanged; }
    void OnDisable() { ElectricalSafetyManager.StateChanged -= OnStateChanged; }

    void Start()
    {
        PhaseChanged?.Invoke(Phase);
        if (carryActive)
        {
            carryActive = false;
            StartCoroutine(ResumeTraining());
            return;
        }
        if (autoStartForTesting) { PreCheckDone = true; BeginTraining(); }
    }

    void Update()
    {
        if (IsRunning) ElapsedSeconds += Time.deltaTime;
        if (!IsPreCheck) return;

        ElectricalSafetyManager m = ElectricalSafetyManager.Instance;
        switch (Stage)
        {
            case PreCheckStage.Ppe:
                if (m != null && m.PpeWorn >= m.requiredPpeItems) SetStage(PreCheckStage.Hazards);
                break;

            case PreCheckStage.Hazards:
                PreCheckSecondsLeft -= Time.deltaTime;
                if (m != null && m.HazardsFound >= m.TotalHazards)
                {
                    EndPreCheck();
                }
                else if (PreCheckSecondsLeft <= 0f)
                {
                    PreCheckSecondsLeft = 0f;
                    PreCheckTimedOut?.Invoke();
                    BeginReview();
                }
                break;

            case PreCheckStage.Review:
                ReviewSecondsLeft -= Time.deltaTime;
                if (!reviewMadeSafe && ReviewSecondsLeft <= reviewMakeSafeAt)
                {
                    reviewMadeSafe = true;
                    foreach (var h in FindObjectsByType<Hazard>(FindObjectsSortMode.None))
                        if (!h.Controlled) h.AutoMakeSafe(false);
                }
                if (ReviewSecondsLeft <= 0f) EndPreCheck();
                break;
        }
    }

    void SetStage(PreCheckStage stage)
    {
        Stage = stage;
        PreCheckStageChanged?.Invoke(stage);
    }

    public void StartPreCheck()
    {
        if (Phase != SessionPhase.Briefing || PreCheckDone) return;
        PreCheckAttempts++;
        PreCheckSecondsLeft = preCheckSeconds;
        Stage = PreCheckStage.Ppe;
        Phase = SessionPhase.PreCheck;
        PhaseChanged?.Invoke(Phase);
        PreCheckStageChanged?.Invoke(Stage);
    }

    void BeginReview()
    {
        MissedHazards = 0;
        foreach (var h in FindObjectsByType<Hazard>(FindObjectsSortMode.None))
            if (!h.Controlled) { MissedHazards++; h.BeginReview(); }

        if (MissedHazards == 0) { EndPreCheck(); return; }
        reviewMadeSafe = false;
        ReviewSecondsLeft = reviewSeconds;
        SetStage(PreCheckStage.Review);
    }

    public void EndPreCheck()
    {
        if (!IsPreCheck) return;
        foreach (var h in FindObjectsByType<Hazard>(FindObjectsSortMode.None))
        {
            if (!h.Controlled) { MissedHazards++; h.AutoMakeSafe(true); }
            h.EndReview();
        }

        ElectricalSafetyManager m = ElectricalSafetyManager.Instance;
        PreCheckHistory.Clear();
        PreCheckHistory.Add("PPE " + m.PpeWorn + " of " + m.requiredPpeItems +
                            "   |   Hazards found " + m.HazardsFound + " of " + m.TotalHazards);
        PreCheckDone = true;
        Phase = SessionPhase.Briefing;
        PhaseChanged?.Invoke(Phase);
    }

    public void BeginTraining()
    {
        if (Phase != SessionPhase.Briefing) return;
        if (!PreCheckDone && !autoStartForTesting) return;
        ElapsedSeconds = 0f;
        Phase = SessionPhase.Training;
        ElectricalSafetyManager.Instance.SetElectricalState(ElectricalState.Energised);
        ElectricalSafetyManager.Instance.ScorePreCheck(PreCheckAttempts);
        PhaseChanged?.Invoke(Phase);
    }

    public void RegisterAction(string name, bool correct, string note = "")
    {
        if (!IsRunning) return;
        if (decisions.TryGetValue(name, out bool earlier) && !earlier) correct = false;
        decisions[name] = correct;
        if (!string.IsNullOrEmpty(note) && !UnsafeNotes.Contains(note)) UnsafeNotes.Add(note);
        ScoreChanged?.Invoke();
    }

    public bool? GetDecision(string name)
    {
        return decisions.TryGetValue(name, out bool value) ? value : (bool?)null;
    }

    public int CorrectCount
    {
        get
        {
            int count = 0;
            foreach (bool value in decisions.Values) if (value) count++;
            return count;
        }
    }

    public void EndSession(bool completed)
    {
        if (!IsRunning) return;
        Completed = completed;
        Passed = completed && CorrectCount >= passMark;
        Phase = SessionPhase.Result;
        PhaseChanged?.Invoke(Phase);
    }

    void OnStateChanged(ElectricalState state)
    {
        if (state == ElectricalState.Restored) EndSession(true);
    }

    // Full restart: back to the welcome board (new trainee).
    public void Restart()
    {
        carryActive = false;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    // Restart only the training: keeps the PPE that is worn and the pre-work check result,
    // reloads the job equipment fresh and goes straight into training.
    public void RestartTraining()
    {
        if (!PreCheckDone) { Restart(); return; }

        carryPpe.Clear();
        foreach (var item in FindObjectsByType<PpeItem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (!item.gameObject.activeSelf && item.isRequired) carryPpe.Add(item.gameObject.name);

        carryHazards.Clear();
        foreach (var h in FindObjectsByType<Hazard>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            carryHazards[h.gameObject.name] = h.Controlled && !h.MadeSafeForYou;

        carryHistory.Clear();
        carryHistory.AddRange(PreCheckHistory);
        carryActive = true;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    IEnumerator ResumeTraining()
    {
        yield return null; // let every other Start() run first

        // replay the pre-work check silently
        Phase = SessionPhase.PreCheck;
        Stage = PreCheckStage.Hazards;
        PreCheckAttempts = 1;
        ElectricalSafetyManager m = ElectricalSafetyManager.Instance;

        foreach (var item in FindObjectsByType<PpeItem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!carryPpe.Contains(item.gameObject.name)) continue;
            m.PpeItemWorn(string.IsNullOrEmpty(item.itemName) ? item.gameObject.name : item.itemName, true);
            item.gameObject.SetActive(false);
        }

        MissedHazards = 0;
        foreach (var h in FindObjectsByType<Hazard>(FindObjectsSortMode.None))
        {
            if (!carryHazards.TryGetValue(h.gameObject.name, out bool found)) continue;
            if (found) h.RestoreFound();
            else { MissedHazards++; h.AutoMakeSafe(true); }
        }

        PreCheckHistory.Clear();
        PreCheckHistory.AddRange(carryHistory);
        PreCheckDone = true;
        RestartedTraining = true;
        Phase = SessionPhase.Briefing;
        BeginTraining();
    }
}
