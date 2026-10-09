using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// Results wall board. The job decides pass / fail; the pre-work check is shown next to it and affects the grade.
//   PASS - EXCELLENT              job passed, all hazards found, no unsafe actions
//   PASS                          job passed, all hazards found
//   PASS - IMPROVE HAZARD CHECK   job passed, but hazards were missed in the pre-work check
//   NOT YET COMPETENT             job finished but too many wrong decisions
//   JOB STOPPED - UNSAFE          the job was stopped for safety
public class TrainingResultsBoard : MonoBehaviour
{
    public GameObject resultGroup;
    public GameObject waitingGroup;
    public Renderer headerBar;
    public TextMeshPro grade;
    public TextMeshPro summary;
    public TextMeshPro preWork;
    public TextMeshPro jobSteps;
    public TextMeshPro improve;
    public XRSimpleInteractable restartTrainingButton;
    public XRSimpleInteractable newTraineeButton;

    static readonly Color Green = new Color(0.18f, 0.62f, 0.28f);
    static readonly Color Amber = new Color(1f, 0.7f, 0.05f);
    static readonly Color Red = new Color(0.82f, 0.13f, 0.1f);

    MaterialPropertyBlock mpb;

    void OnEnable() { SessionManager.PhaseChanged += OnPhase; }
    void OnDisable() { SessionManager.PhaseChanged -= OnPhase; }

    void Start()
    {
        mpb = new MaterialPropertyBlock();
        if (restartTrainingButton != null)
            restartTrainingButton.selectEntered.AddListener(_ => Press(restartTrainingButton, true));
        if (newTraineeButton != null)
            newTraineeButton.selectEntered.AddListener(_ => Press(newTraineeButton, false));
        var s = SessionManager.Instance;
        OnPhase(s != null ? s.Phase : SessionPhase.Briefing);
    }

    void Press(XRSimpleInteractable button, bool trainingOnly)
    {
        var s = SessionManager.Instance;
        if (s == null || s.Phase != SessionPhase.Result) return;
        Sfx.PlayAt(Sfx.Click, button.transform.position, 0.8f);
        if (trainingOnly) s.RestartTraining();
        else s.Restart();
    }

    void OnPhase(SessionPhase phase)
    {
        bool result = phase == SessionPhase.Result;
        if (resultGroup != null) resultGroup.SetActive(result);
        if (waitingGroup != null) waitingGroup.SetActive(!result);
        if (result) Build();
    }

    static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);
    static string Mark(bool? v) => v == null ? "<color=#8A949E>[ -- ]</color>" : v.Value ? "<color=#66BB6A>[ OK ]</color>" : "<color=#FF5252>[ X ]</color>";

    void Build()
    {
        var s = SessionManager.Instance;
        var m = ElectricalSafetyManager.Instance;
        if (s == null || m == null) return;

        int total = m.TotalHazards;
        int found = 0;
        foreach (var h in Hazard.All) if (h.Controlled && !h.MadeSafeForYou && !h.ControlledLate) found++;
        bool allHazards = found >= total;

        // ---- grade: the job is the main thing; the pre-work check refines it ----
        string g; Color c;
        if (!s.Completed) { g = "JOB STOPPED - UNSAFE"; c = Red; }
        else if (!s.Passed) { g = "NOT YET COMPETENT"; c = Red; }
        else if (allHazards && m.UnsafeCount == 0) { g = "PASS - EXCELLENT"; c = Green; }
        else if (allHazards) { g = "PASS"; c = Green; }
        else { g = "PASS - IMPROVE HAZARD CHECK"; c = Amber; }

        grade.text = "<b>" + g + "</b>";
        grade.color = c;
        if (headerBar != null)
        {
            headerBar.GetPropertyBlock(mpb);
            mpb.SetColor("_BaseColor", c);
            mpb.SetColor("_Color", c);
            headerBar.SetPropertyBlock(mpb);
        }

        int secs = Mathf.RoundToInt(s.ElapsedSeconds);
        summary.text = "Job score <b>" + s.CorrectCount + " / " + SessionManager.DecisionKeys.Length + "</b>" +
                       "     Time <b>" + (secs / 60) + ":" + (secs % 60).ToString("00") + "</b>" +
                       "     Unsafe actions <b>" + m.UnsafeCount + "</b>";

        // ---- pre-work check ----
        var pw = new StringBuilder();
        pw.Append("<b><color=#FFC400>PRE-WORK CHECK</color></b>\n");
        pw.Append(Mark(m.PpeWorn >= m.requiredPpeItems)).Append("  PPE ").Append(m.PpeWorn).Append(" / ").Append(m.requiredPpeItems).Append("\n");
        pw.Append(Mark(allHazards)).Append("  Hazards found <b>").Append(found).Append(" / ").Append(total).Append("</b>\n");
        foreach (var h in Hazard.All)
        {
            string name = h.title.Replace("\n", " ");
            pw.Append("<size=85%>   ");
            if (h.MadeSafeForYou) pw.Append("<color=#FF8A80>missed: ").Append(name).Append("</color>");
            else if (h.ControlledLate) pw.Append("<color=#FFD180>late: ").Append(name).Append("</color>");
            else pw.Append("<color=#A5D6A7>found: ").Append(name).Append("</color>");
            pw.Append("</size>\n");
        }
        preWork.text = pw.ToString();

        // ---- job steps ----
        var js = new StringBuilder();
        js.Append("<b><color=#FFC400>THE JOB</color></b>\n");
        js.Append(Mark(s.GetDecision("Isolate"))).Append("  Isolate W2 (correct isolator)\n");
        js.Append(Mark(s.GetDecision("Lockout"))).Append("  Lock off + DANGER tag\n");
        js.Append(Mark(s.GetDecision("Verify"))).Append("  Prove dead before working\n");
        js.Append(Mark(s.GetDecision("Restore"))).Append("  Fuse replaced, power restored safely\n");
        if (s.RestartedTraining) js.Append("<size=80%><color=#8A949E>(training restarted)</color></size>");
        jobSteps.text = js.ToString();

        // ---- what to improve ----
        var im = new StringBuilder();
        im.Append("<b><color=#FFC400>WHAT TO IMPROVE</color></b>\n");
        int n = 0;
        foreach (var note in s.UnsafeNotes)
        {
            if (string.IsNullOrEmpty(note)) continue;
            im.Append("- ").Append(note).Append("\n");
            if (++n >= 4) break;
        }
        if (n == 0) im.Append(allHazards ? "Nothing - excellent, safe work.\n" : "- Look for every hazard before you start the job.\n");
        improve.text = im.ToString();
    }
}
