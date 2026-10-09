using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// Welcome board + phase locks.
// Before the check: board explains the session, only "Start Pre-Work Check".
// After the check:  board shows what was found / missed (recorded), only "Start Training". No redo.
public class TrainingFlow : MonoBehaviour
{
    public Button preCheckButton;
    public TMP_Text preCheckButtonLabel;
    public Button startTrainingButton;
    public TMP_Text summaryText;
    public GameObject welcomeGroup;      // shown only while at the welcome board
    [Tooltip("Main welcome text (WelcomeCanvas/Text (TMP)). Found by name if empty.")]
    public TMP_Text boardText;

    PpeItem[] ppeItems;
    Hazard[] hazards;
    TrainingTool[] tools;

    void OnEnable()  { SessionManager.PhaseChanged += Refresh; SessionManager.PreCheckStageChanged += OnStage; }
    void OnDisable() { SessionManager.PhaseChanged -= Refresh; SessionManager.PreCheckStageChanged -= OnStage; }

    void OnStage(PreCheckStage stage) { if (SessionManager.Instance != null) Refresh(SessionManager.Instance.Phase); }

    void Update()
    {
        if (hazards == null) return;

        SessionManager session = SessionManager.Instance;
        bool allowHazards = session != null && session.HazardHuntActive && HazardBridge.PpeComplete;

        foreach (Hazard hazard in hazards)
        {
            if (hazard == null) continue;
            XRSimpleInteractable interactable = hazard.GetComponent<XRSimpleInteractable>();
            if (interactable != null && interactable.enabled != allowHazards)
                interactable.enabled = allowHazards;
        }
    }

    void Start()
    {
        ppeItems = FindObjectsByType<PpeItem>(FindObjectsSortMode.None);
        hazards = FindObjectsByType<Hazard>(FindObjectsSortMode.None);
        tools = FindObjectsByType<TrainingTool>(FindObjectsSortMode.None);
        preCheckButton.onClick.AddListener(() => SessionManager.Instance.StartPreCheck());
        startTrainingButton.onClick.AddListener(() => SessionManager.Instance.BeginTraining());

        if (boardText == null)
        {
            var t = GameObject.Find("WelcomeCanvas/Text (TMP)");
            if (t != null) boardText = t.GetComponent<TMP_Text>();
        }
        Style(boardText, 22f, 46f);
        Style(summaryText, 18f, 34f);
        if (boardText != null) boardText.text = WelcomeText();

        Refresh(SessionManager.Instance.Phase);
    }

    // keep text inside the board with margins, shrink to fit
    static void Style(TMP_Text t, float min, float max)
    {
        if (t == null) return;
        t.enableAutoSizing = true;
        t.fontSizeMin = min;
        t.fontSizeMax = max;
        t.margin = new Vector4(40f, 16f, 40f, 12f);
        t.textWrappingMode = TextWrappingModes.Normal;
        t.alignment = TextAlignmentOptions.Top;
    }

    string WelcomeText()
    {
        var m = ElectricalSafetyManager.Instance;
        return BuildWelcomeText(m != null ? m.requiredPpeItems : 6, Mathf.RoundToInt(SessionManager.Instance.preCheckSeconds));
    }

    // Also used by the editor so the Scene view shows the same text as the game.
    public static string BuildWelcomeText(int ppe, int secs)
    {
        return
            "<size=125%><b>WELCOME, TRAINEE</b></size>\n" +
            "<color=#3A3A3A>Workstation 2 has a blown fuse. Make the area safe and replace it.</color>\n\n" +
            "<align=left><color=#B86E00><b>STEP 1  PRE-WORK CHECK</b></color>  <size=85%><color=#B00020>(counts in your result)</color></size>\n" +
            "<indent=4%>- Put on <b>all " + ppe + " PPE items</b> at the PPE station. This is required.\n" +
            "- Then you have <b>" + (secs / 60) + ":" + (secs % 60).ToString("00") + "</b> to find and make safe every hazard. " +
            "Hazards you miss are shown to you and recorded.</indent>\n\n" +
            "<color=#1E7B34><b>STEP 2  THE JOB</b></color>\n" +
            "<indent=4%>- Isolate W2, lock it off, prove it dead, replace the fuse, restore power.\n" +
            "- Follow the <b>JOB CARD</b> on the wall beside Workstation 2.</indent>\n\n" +
            "<color=#B00020><b>POWER CUT?</b></color>  An <b>emergency torch</b> charges under the yellow beacon, by the isolation board.</align>";
    }

    public static string BuildFirstSummary(int ppe)
    {
        return "<color=#1F4E99><b>Your pre-work check is scored.</b> All " + ppe +
               " PPE items are required; then find as many hazards as you can.</color>";
    }

    // Same text styling, usable from the editor.
    public static void StyleText(TMP_Text t, float min, float max) => Style(t, min, max);

    void Refresh(SessionPhase phase)
    {
        if (ppeItems == null) return;
        SessionManager s = SessionManager.Instance;
        ElectricalSafetyManager m = ElectricalSafetyManager.Instance;

        // PPE during the check, hazards only in the hazard stage, job equipment only during training.
        bool checking = phase == SessionPhase.PreCheck && s.Stage != PreCheckStage.Review;
        bool training = phase == SessionPhase.Training;
        foreach (PpeItem item in ppeItems)
        {
            if (item == null) continue;
            XRGrabInteractable grab = item.GetComponent<XRGrabInteractable>();
            if (grab != null) grab.enabled = checking;
        }
        foreach (Hazard hazard in hazards)
        {
            if (hazard == null) continue;
            XRSimpleInteractable select = hazard.GetComponent<XRSimpleInteractable>();
            if (select != null) select.enabled = s.HazardHuntActive && HazardBridge.PpeComplete;
        }
        foreach (TrainingTool tool in tools)
        {
            if (tool == null) continue;
            foreach (XRBaseInteractable i in tool.GetComponentsInChildren<XRBaseInteractable>(true))
                i.enabled = training;
        }

        if (welcomeGroup != null) welcomeGroup.SetActive(phase == SessionPhase.Briefing);

        bool done = s.PreCheckDone;
        preCheckButton.gameObject.SetActive(!done);
        preCheckButton.interactable = !done;
        preCheckButtonLabel.text = "Start Pre-Work Check";
        SetButtonLook(preCheckButton, !done, new Color(0.98f, 0.72f, 0.15f), new Color(0.1f, 0.1f, 0.1f));

        startTrainingButton.interactable = done;
        SetButtonLook(startTrainingButton, done, new Color(0.15f, 0.55f, 0.25f), Color.white);

        summaryText.text = Summary(s, m);
    }

    static void SetButtonLook(Button button, bool available, Color fill, Color textColor)
    {
        Image image = button.GetComponent<Image>();
        if (image != null) image.color = available ? fill : new Color(0.62f, 0.62f, 0.62f);
        TMP_Text label = button.GetComponentInChildren<TMP_Text>();
        if (label != null) label.color = available ? textColor : new Color(0.35f, 0.35f, 0.35f);
    }

    string Summary(SessionManager s, ElectricalSafetyManager m)
    {
        if (!s.PreCheckDone)
            return BuildFirstSummary(m.requiredPpeItems);

        var sb = new StringBuilder();
        int total = m.TotalHazards;
        if (m.HazardsFound >= total)
        {
            sb.Append("<color=#1E7B34><b>PRE-WORK CHECK COMPLETE</b>  PPE " + m.PpeWorn + "/" + m.requiredPpeItems +
                      "  |  all " + total + " hazards found and made safe.</color>\n");
        }
        else
        {
            sb.Append("<color=#B86E00><b>PRE-WORK CHECK FINISHED</b>  PPE " + m.PpeWorn + "/" + m.requiredPpeItems +
                      "  |  hazards found " + m.HazardsFound + " of " + total + ".</color>\n");
            var missed = new StringBuilder();
            foreach (var h in Hazard.All)
                if (h.MadeSafeForYou) { if (missed.Length > 0) missed.Append(", "); missed.Append(h.title.Replace("\n", " ")); }
            if (missed.Length > 0)
                sb.Append("<color=#B00020>Missed: " + missed + ". They have been made safe for you. This is recorded in your result.</color>\n");
        }
        sb.Append("<b>Press Start Training.</b>");
        return sb.ToString();
    }
}
