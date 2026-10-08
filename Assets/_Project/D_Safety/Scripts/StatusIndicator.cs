using UnityEngine;
using TMPro;

public class StatusIndicator : MonoBehaviour
{
    public Renderer lamp;
    public Light glow;
    public TMP_Text label;

    public Color liveColor = Color.red;
    public Color isolatedColor = new Color(1f, 0.6f, 0f);
    public Color safeColor = Color.green;
    public Color normalColor = Color.cyan;

    void OnEnable()
    {
        ElectricalSafetyManager.StateChanged += Show;
    }

    void OnDisable()
    {
        ElectricalSafetyManager.StateChanged -= Show;
    }

    void Start()
    {
        Show(ElectricalState.Energised);
    }

    void Show(ElectricalState state)
    {
        Color color;
        string text;

        switch (state)
        {
            case ElectricalState.Energised:
                color = liveColor;
                text = "LIVE";
                break;

            case ElectricalState.Isolated:
                color = isolatedColor;
                text = "ISOLATED - NOT VERIFIED";
                break;

            case ElectricalState.VerifiedSafe:
                color = safeColor;
                text = "VERIFIED SAFE";
                break;

            case ElectricalState.Repaired:
                color = safeColor;
                text = "REPAIRED - POWER OFF";
                break;

            default:
                color = normalColor;
                text = "NORMAL - POWER ON";
                break;
        }

        if (lamp != null)
            lamp.material.color = color;

        if (glow != null)
            glow.color = color;

        if (label != null)
            label.text = text;
    }
}