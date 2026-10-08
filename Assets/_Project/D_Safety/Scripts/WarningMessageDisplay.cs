
using UnityEngine;
using TMPro;

public class WarningMessageDisplay : MonoBehaviour
{
    public TMP_Text messageText;
    public float displaySeconds = 5f;

    float timer;

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
            messageText.text = "";
    }

    void OnMessage(string text, bool isUnsafe)
    {
        if (messageText == null)
            return;

        messageText.text = text;

        // Red for unsafe actions, green for normal feedback
        messageText.color = isUnsafe ? Color.red : Color.green;

        timer = displaySeconds;
    }

    void Update()
    {
        if (timer > 0f)
        {
            timer -= Time.deltaTime;

            if (timer <= 0f && messageText != null)
                messageText.text = "";
        }
    }
}
