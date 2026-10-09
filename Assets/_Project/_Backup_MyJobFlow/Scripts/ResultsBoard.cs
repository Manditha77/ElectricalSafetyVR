using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
namespace MyJobFlowBackup {

// End-of-training results on the south wall, with a restart button.
public class ResultsBoard : MonoBehaviour
{
    public GameObject content;                 // shown only at the end
    public TextMeshPro title;
    public TextMeshPro details;
    public XRSimpleInteractable restartButton;

    static readonly string[] Labels =
    {
        "Wore full PPE", "Reported the hazards", "Isolated the correct supply",
        "Locked off before work", "Proved dead before opening", "Restored power safely"
    };

    void OnEnable()  { SessionManager.PhaseChanged += Refresh; }
    void OnDisable() { SessionManager.PhaseChanged -= Refresh; }

    void Start()
    {
        if (restartButton != null)
            restartButton.selectEntered.AddListener(_ => SessionManager.Instance.Restart());
        if (SessionManager.Instance != null) Refresh(SessionManager.Instance.Phase);
    }

    void Refresh(SessionPhase phase)
    {
        bool show = phase == SessionPhase.Result;
        if (content != null) content.SetActive(show);
        if (!show || title == null || details == null) return;

        SessionManager s = SessionManager.Instance;
        ElectricalSafetyManager m = ElectricalSafetyManager.Instance;

        if (s.Passed)
            title.text = "<color=#3DDC6A>PASS</color>\n<size=45%>Job completed safely</size>";
        else if (s.Completed)
            title.text = "<color=#FF5A4E>FAIL</color>\n<size=45%>Job completed, but not safely enough</size>";
        else
            title.text = "<color=#FF5A4E>STOPPED</color>\n<size=45%>Training ended for safety</size>";

        StringBuilder sb = new StringBuilder();
        int secs = Mathf.RoundToInt(s.ElapsedSeconds);
        sb.AppendLine("<b>Score " + s.CorrectCount + " / " + SessionManager.DecisionKeys.Length +
                      "     Time " + (secs / 60) + ":" + (secs % 60).ToString("00") +
                      "     Unsafe actions " + m.UnsafeCount + "</b>");
        sb.AppendLine();

        for (int i = 0; i < SessionManager.DecisionKeys.Length; i++)
        {
            bool? d = s.GetDecision(SessionManager.DecisionKeys[i]);
            string mark = d == true ? "<color=#3DDC6A>[x]</color>"
                        : d == false ? "<color=#FF5A4E>[!]</color>"
                        : "<color=#999999>[-]</color>";
            sb.AppendLine(mark + "  " + Labels[i]);
        }

        if (s.PreCheckHistory.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("<b>Pre-work checks</b>");
            foreach (string line in s.PreCheckHistory) sb.AppendLine(line);
        }
        if (s.UnsafeNotes.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("<b>What to improve</b>");
            foreach (string note in s.UnsafeNotes) sb.AppendLine("- " + note);
        }
        details.text = sb.ToString();
    }
}
}
