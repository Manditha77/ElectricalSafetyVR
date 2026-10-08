using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum SessionPhase { Briefing, PreCheck, Training, Result }

public class SessionManager : MonoBehaviour
{
    public static SessionManager Instance { get; private set; }

    public static event Action<SessionPhase> PhaseChanged;
    public static event Action ScoreChanged;

    public static readonly string[] DecisionKeys =
        { "PPE", "Hazards", "Isolate", "Lockout", "Verify", "Restore" };

    [Tooltip("Tick in test scenes to skip straight to training.")]
    public bool autoStartForTesting;
    public int passMark = 5;

    [Header("Pre-work check")]
    public float preCheckSeconds = 120f;

    public SessionPhase Phase { get; private set; } = SessionPhase.Briefing;
    public bool IsRunning => Phase == SessionPhase.Training;
    public bool IsPreCheck => Phase == SessionPhase.PreCheck;
    public float ElapsedSeconds { get; private set; }
    public float PreCheckSecondsLeft { get; private set; }
    public int PreCheckAttempts { get; private set; }
    public bool Completed { get; private set; }
    public bool Passed { get; private set; }
    public readonly List<string> UnsafeNotes = new List<string>();
    public readonly List<string> PreCheckHistory = new List<string>();

    readonly Dictionary<string, bool> decisions = new Dictionary<string, bool>();

    void Awake() { Instance = this; }
    void OnEnable() { ElectricalSafetyManager.StateChanged += OnStateChanged; }
    void OnDisable() { ElectricalSafetyManager.StateChanged -= OnStateChanged; }

    void Start()
    {
        PhaseChanged?.Invoke(Phase);
        if (autoStartForTesting) BeginTraining();
    }

    void Update()
    {
        if (IsRunning) ElapsedSeconds += Time.deltaTime;

        if (IsPreCheck)
        {
            PreCheckSecondsLeft -= Time.deltaTime;
            ElectricalSafetyManager m = ElectricalSafetyManager.Instance;
            bool allDone = m != null && m.PpeWorn >= m.requiredPpeItems && m.HazardsFound >= m.TotalHazards;
            if (PreCheckSecondsLeft <= 0f || allDone) EndPreCheck();
        }
    }

    public void StartPreCheck()
    {
        if (Phase != SessionPhase.Briefing) return;
        PreCheckAttempts++;
        PreCheckSecondsLeft = preCheckSeconds;
        Phase = SessionPhase.PreCheck;
        PhaseChanged?.Invoke(Phase);
    }

    public void EndPreCheck()
    {
        if (!IsPreCheck) return;
        ElectricalSafetyManager m = ElectricalSafetyManager.Instance;
        PreCheckHistory.Add("Check " + PreCheckAttempts + ": PPE " + m.PpeWorn + " of " + m.requiredPpeItems +
                            ", hazards " + m.HazardsFound + " of " + m.TotalHazards);
        Phase = SessionPhase.Briefing;
        PhaseChanged?.Invoke(Phase);
    }

    public void BeginTraining()
    {
        if (Phase != SessionPhase.Briefing) return;
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
        if (!string.IsNullOrEmpty(note)) UnsafeNotes.Add(note);
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

    public void Restart()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}