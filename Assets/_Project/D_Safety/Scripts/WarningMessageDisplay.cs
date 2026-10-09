
using UnityEngine;
using TMPro;

public class WarningMessageDisplay : MonoBehaviour
{
    public TMP_Text messageText;
    public float displaySeconds = 5f;

    float timer;
    bool showingUnsafe;

    void OnEnable()
    {
        ElectricalSafetyManager.MessageRaised += OnMessage;
    }

    void OnDisable()
    {
        ElectricalSafetyManager.MessageRaised -= OnMessage;
    }

    void Start()
    {
        if (messageText != null)
        {
            messageText.text = "";
        }
    }

    void OnMessage(string text, bool isUnsafe)
    {
        if (messageText == null)
            return;

        // Do not replace an active red warning
        // with a success or guidance message.
        if (showingUnsafe && !isUnsafe && timer > 0f)
            return;

        bool isGuidance =
            !isUnsafe && text.StartsWith("[GUIDANCE] ");

        messageText.text = isGuidance
            ? text.Replace("[GUIDANCE] ", "")
            : text;

        if (isUnsafe)
        {
            messageText.color = Color.red;
            showingUnsafe = true;
        }
        else if (isGuidance)
        {
            messageText.color = Color.yellow;
            showingUnsafe = false;
        }
        else
        {
            messageText.color = Color.green;
            showingUnsafe = false;
        }

        timer = displaySeconds;
    }

    
    void Update()
    {
        if (timer > 0f)
        {
            timer -= Time.deltaTime;

            if (timer <= 0f)
            {
                if (messageText != null)
                    messageText.text = "";

                showingUnsafe = false;
            }
        }
    }

}
