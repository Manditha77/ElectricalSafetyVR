using System.Text;
using UnityEngine;
using TMPro;

public class ResultBoard : MonoBehaviour
{
    public TMP_Text resultText;

    static readonly string[] Labels =
    {
        "Selected the required PPE before starting work",
        "Reported the visible workshop hazards",
        "Isolated the correct supply before maintenance",
        "Applied the lockout tag before opening the workstation",
        "Verified the workstation was dead before the repair",
        "Restored power only when the workstation was safe"
    };

    void OnEnable()
    {
        SessionManager.PhaseChanged += OnPhase;
    }

    void OnDisable()
    {
        SessionManager.PhaseChanged -= OnPhase;
    }

    void OnPhase(SessionPhase phase)
    {
        if (phase == SessionPhase.Result)
            Build();
    }

    void Build()
    {
        SessionManager session = SessionManager.Instance;
        StringBuilder text = new StringBuilder();

        text.AppendLine(
            session.Passed
                ? "<color=green>PASSED</color>"
                : "<color=red>NOT PASSED</color>"
        );

        if (!session.Completed)
            text.AppendLine("The run was stopped after a critical safety failure.");

        int seconds = Mathf.FloorToInt(session.ElapsedSeconds);

        text.AppendLine(
            "Time: " +
            (seconds / 60).ToString("00") + ":" +
            (seconds % 60).ToString("00")
        );

        text.AppendLine(
            "Safety decisions correct: " +
            session.CorrectCount +
            " of " +
            Labels.Length
        );

        text.AppendLine();

        for (int i = 0; i < SessionManager.DecisionKeys.Length; i++)
        {
            bool? decision =
                session.GetDecision(SessionManager.DecisionKeys[i]);

            string mark =
                decision == true
                    ? "<color=green>YES</color>"
                    : decision == false
                        ? "<color=red>NO</color>"
                        : "--";

            text.AppendLine(mark + " " + Labels[i]);
        }

        text.AppendLine();
        text.AppendLine("Unsafe actions: " + session.UnsafeNotes.Count);

        foreach (string note in session.UnsafeNotes)
            text.AppendLine("- " + note);

        resultText.text = text.ToString();
    }
}