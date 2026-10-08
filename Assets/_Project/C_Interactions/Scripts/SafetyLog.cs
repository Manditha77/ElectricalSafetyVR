using UnityEngine;

public class SafetyLog : MonoBehaviour
{
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

    void OnState(ElectricalState state) { Debug.Log("STATE: " + state); }

    void OnMessage(string text, bool isUnsafe)
    {
        if (isUnsafe) Debug.LogWarning("UNSAFE: " + text);
        else Debug.Log(text);
    }
}