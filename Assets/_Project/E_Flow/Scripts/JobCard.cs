using UnityEngine;
using TMPro;

public class JobCard : MonoBehaviour
{
    public TMP_Text taskText;
    public TMP_Text messageText;
    public TMP_Text timerText;

    public float messageSeconds = 6f;
    float messageTimer;
    bool showingUnsafe;

    void Awake()
    {
        taskText.text = "Read the briefing, then press the green Start button.";
        messageText.text = "";
    }

    void OnEnable()
    {
        ElectricalSafetyManager.StateChanged += OnState;
        ElectricalSafetyManager.MessageRaised += OnMessage;
    }

    void OnDisable()
    {
        ElectricalSafetyManager.StateChanged -= OnState;
        ElectricalSafetyManager.MessageRaised -= OnMessage;
    }

    void OnState(ElectricalState state)
    {
        switch (state)
        {
            case ElectricalState.Energised:
                taskText.text =
                    "JOB 0412: fault reported on WORKSTATION 2.\n" +
                    "Put on your PPE and report the hazards you can see.\n" +
                    "Then make the workstation safe before any repair.";
                break;

            case ElectricalState.Isolated:
                taskText.text =
                    "The supply is off.\n" +
                    "Secure the isolation, then prove the workstation is dead.";
                break;

            case ElectricalState.VerifiedSafe:
                taskText.text =
                    "Verified safe.\n" +
                    "Replace the damaged fuse using the correct tool.";
                break;

            case ElectricalState.Repaired:
                taskText.text =
                    "Repair complete.\n" +
                    "Leave the workstation safe, clear your tools, then restore the supply.";
                break;

            case ElectricalState.Restored:
                taskText.text =
                    "Supply restored. Go to the result board.";
                break;
        }
    }

    void OnMessage(string text, bool isUnsafe)
    {
        if (!isUnsafe && showingUnsafe && messageTimer > 0f)
            return;

        messageText.text = text;
        messageText.color = isUnsafe ? Color.red : Color.white;
        showingUnsafe = isUnsafe;
        messageTimer = messageSeconds;
    }

    void Update()
    {
        if (messageTimer > 0f)
        {
            messageTimer -= Time.deltaTime;

            if (messageTimer <= 0f)
                messageText.text = "";
        }

        SessionManager session = SessionManager.Instance;

        if (session != null && timerText != null)
        {
            int seconds = Mathf.FloorToInt(session.ElapsedSeconds);

            timerText.text =
                (seconds / 60).ToString("00") + ":" +
                (seconds % 60).ToString("00");
        }
    }
}