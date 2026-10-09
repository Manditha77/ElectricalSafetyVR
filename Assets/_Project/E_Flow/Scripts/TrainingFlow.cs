using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class TrainingFlow : MonoBehaviour
{
    public Button preCheckButton;
    public TMP_Text preCheckButtonLabel;
    public Button startTrainingButton;
    public TMP_Text summaryText;
    public GameObject welcomeGroup;      // shown only while at the welcome board

    PpeItem[] ppeItems;
    Hazard[] hazards;
    TrainingTool[] tools;

    void OnEnable()  { SessionManager.PhaseChanged += Refresh; }
    void OnDisable() { SessionManager.PhaseChanged -= Refresh; }

    void Start()
    {
        ppeItems = FindObjectsByType<PpeItem>(FindObjectsSortMode.None);
        hazards = FindObjectsByType<Hazard>(FindObjectsSortMode.None);
        tools = FindObjectsByType<TrainingTool>(FindObjectsSortMode.None);
        preCheckButton.onClick.AddListener(() => SessionManager.Instance.StartPreCheck());
        startTrainingButton.onClick.AddListener(() => SessionManager.Instance.BeginTraining());
        Refresh(SessionManager.Instance.Phase);
    }

    void Refresh(SessionPhase phase)
    {
        if (ppeItems == null) return;
        SessionManager s = SessionManager.Instance;
        ElectricalSafetyManager m = ElectricalSafetyManager.Instance;

        // PPE and hazards only during the pre-work check; job equipment only during training.
        bool checking = phase == SessionPhase.PreCheck;
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
            if (select != null) select.enabled = checking;
        }
        foreach (TrainingTool tool in tools)
        {
            if (tool == null) continue;
            foreach (XRBaseInteractable i in tool.GetComponentsInChildren<XRBaseInteractable>(true))
                i.enabled = training;
        }

        if (welcomeGroup != null) welcomeGroup.SetActive(phase == SessionPhase.Briefing);

        bool checkedOnce = s.PreCheckAttempts > 0;
        bool allDone = m.PpeWorn >= m.requiredPpeItems && m.HazardsFound >= m.TotalHazards;
        startTrainingButton.interactable = checkedOnce;
        preCheckButton.interactable = !allDone;
        preCheckButtonLabel.text = checkedOnce ? "Redo Pre-Work Check" : "Start Pre-Work Check";
        SetButtonLook(startTrainingButton, checkedOnce, new Color(0.15f, 0.55f, 0.25f), Color.white);
        SetButtonLook(preCheckButton, !allDone, new Color(0.98f, 0.72f, 0.15f), new Color(0.1f, 0.1f, 0.1f));
        summaryText.text = Summary(s, m, allDone);
    }

    static void SetButtonLook(Button button, bool available, Color fill, Color textColor)
    {
        Image image = button.GetComponent<Image>();
        if (image != null) image.color = available ? fill : new Color(0.62f, 0.62f, 0.62f);
        TMP_Text label = button.GetComponentInChildren<TMP_Text>();
        if (label != null) label.color = available ? textColor : new Color(0.35f, 0.35f, 0.35f);
    }

    string Summary(SessionManager s, ElectricalSafetyManager m, bool allDone)
    {
        if (s.PreCheckAttempts == 0)
            return "Step 1: Pre-work check (" + Mathf.RoundToInt(s.preCheckSeconds) + " seconds)\n" +
                   "Put on your PPE and report every hazard you can find.";

        StringBuilder text = new StringBuilder();
        foreach (string line in s.PreCheckHistory) text.AppendLine(line);

        if (allDone)
        {
            text.Append("Pre-work check complete. Start the training.");
        }
        else
        {
            int ppeLeft = Mathf.Max(0, m.requiredPpeItems - m.PpeWorn);
            int hazardsLeft = Mathf.Max(0, m.TotalHazards - m.HazardsFound);
            text.Append("Still to do: " + ppeLeft + " PPE item(s), " + hazardsLeft + " hazard(s).\n" +
                        "Redo the check, or start training now (this is noted in your result).");
        }
        return text.ToString();
    }
}