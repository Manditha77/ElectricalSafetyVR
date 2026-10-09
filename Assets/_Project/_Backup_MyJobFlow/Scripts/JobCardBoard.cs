using System.Text;
using TMPro;
using UnityEngine;
namespace MyJobFlowBackup {

// Work order on the wall next to Workstation 2. Steps tick off live as they are done.
public class JobCardBoard : MonoBehaviour
{
    public TextMeshPro text;

    static readonly string[] Steps =
    {
        "Pre-work check: full PPE on, hazards reported",
        "Isolate: switch OFF W2 at the isolation point",
        "Lock off: fit your lockout tag to W2",
        "Prove your tester on the proving unit",
        "Test for dead at the TEST POINT (0 V)",
        "Open the cover, replace fuse F2",
        "Close cover, hang up puller, remove your tag",
        "Restore: switch W2 back ON"
    };

    readonly bool[] reached = new bool[Steps.Length];
    float nextRefresh;

    void Update()
    {
        if (text == null || Time.time < nextRefresh) return;
        nextRefresh = Time.time + 0.25f;
        text.text = Build();
    }

    string Build()
    {
        SessionManager s = SessionManager.Instance;
        ElectricalSafetyManager m = ElectricalSafetyManager.Instance;
        if (s == null || m == null) return "";

        bool training = s.Phase == SessionPhase.Training || s.Phase == SessionPhase.Result;
        int level = training ? (int)m.State : -1;

        bool[] now =
        {
            s.PreCheckAttempts > 0 && m.PpeWorn >= m.requiredPpeItems && m.HazardsFound >= m.requiredHazards,
            level >= (int)ElectricalState.Isolated,
            training && m.LockoutApplied,
            m.TesterProven,
            level >= (int)ElectricalState.VerifiedSafe,
            level >= (int)ElectricalState.Repaired,
            level >= (int)ElectricalState.Repaired && !m.CoverOpen && m.ToolsClear && !m.LockoutApplied,
            m.State == ElectricalState.Restored
        };
        for (int i = 0; i < reached.Length; i++) reached[i] |= now[i];

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("<size=125%><b>JOB CARD  WO-2047</b></size>");
        sb.AppendLine("<b>Equipment:</b> Workstation 2 (this wall)");
        sb.AppendLine("<b>Fault:</b> blown fuse F2");
        sb.AppendLine("<b>Isolate at:</b> W2, isolation point (east wall)");
        sb.AppendLine();

        int current = -1;
        for (int i = 0; i < Steps.Length; i++)
        {
            string mark, color;
            if (reached[i]) { mark = "[x]"; color = "#2E9E44"; }
            else if (current < 0) { current = i; mark = ">>"; color = "#B86E00"; }
            else { mark = "[  ]"; color = "#666666"; }

            string extra = "";
            if (i == 0 && s.PreCheckAttempts > 0)
                extra = "  (PPE " + m.PpeWorn + "/" + m.requiredPpeItems + ", hazards " + m.HazardsFound + "/" + m.TotalHazards + ")";

            sb.Append("<color=").Append(color).Append(">").Append(mark).Append(" ")
              .Append(i + 1).Append(". ").Append(Steps[i]).Append(extra).AppendLine("</color>");
        }
        return sb.ToString();
    }
}
}
