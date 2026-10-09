using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;

// Wall board next to the results: how each hazard was handled.
public class HazardReportBoard : MonoBehaviour
{
    public TextMeshPro list;

    string lastPhase = "?";

    void Update()
    {
        string phase = HazardBridge.Phase;
        if (phase == lastPhase && phase != "Result") return;
        lastPhase = phase;
        if (list == null) return;

        if (phase != "Result")
        {
            list.text = "<color=#9AA4AE>Your hazard report appears here when the job ends.\n\n" +
                        "<b>1. SPOT</b> it: point and press the trigger.\n" +
                        "<b>2. MAKE IT SAFE</b>: mop, unplug or remove.\n" +
                        "<b>3. THEN</b> start the job.</color>";
            return;
        }
        Build();
        enabled = false; // the board is fixed until restart
    }

    void Build()
    {
        var all = Hazard.All.OrderBy(h => h.title).ToList();
        int ok = all.Count(h => h.Controlled && !h.ControlledLate);
        var sb = new StringBuilder();
        string c = ok == all.Count ? "#66BB6A" : "#FF7043";
        sb.Append("<color=").Append(c).Append("><b>").Append(ok).Append(" of ").Append(all.Count)
          .Append("</b> made safe before work started</color>\n\n");

        foreach (var h in all)
        {
            sb.Append("<b>").Append(h.title.Replace("\n", " ")).Append("</b>\n<size=80%>");
            if (h.Controlled && !h.ControlledLate) sb.Append("<color=#66BB6A>Made safe in the pre-work check</color>");
            else if (h.Controlled) sb.Append("<color=#FFB300>Made safe late, during the job</color>");
            else if (h.Spotted) sb.Append("<color=#FF5252>Spotted but NOT made safe</color>");
            else sb.Append("<color=#FF5252>MISSED</color>");
            if (!string.IsNullOrEmpty(h.Outcome)) sb.Append("<color=#FF8A80>: ").Append(h.Outcome).Append("</color>");
            sb.Append("</size>\n");
        }
        list.text = sb.ToString();
    }
}
