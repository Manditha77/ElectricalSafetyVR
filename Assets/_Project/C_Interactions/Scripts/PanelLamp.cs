using UnityEngine;

public class PanelLamp : MonoBehaviour
{
    public Renderer bulb;
    public Light glow;
    public AudioSource warningSound;

    public Color offColor = Color.gray;
    public Color correctColor = Color.green;
    public Color wrongColor = Color.red;
    public float wrongSeconds = 2f;
    public float blinksPerSecond = 3f;

    bool correctActive;   // the correct switch is currently switched off
    float wrongTimer;     // above 0 while the red warning is flashing

    // Called by the correct switch every time it changes.
    public void SetCorrect(bool active)
    {
        correctActive = active;
    }

    // Called when a wrong switch has been pressed.
    public void ShowWrong()
    {
        wrongTimer = wrongSeconds;
        if (warningSound != null) warningSound.Play();
    }

    void Update()
    {
        bool blinkOn = Mathf.FloorToInt(Time.time * blinksPerSecond * 2f) % 2 == 0;

        if (wrongTimer > 0f)
        {
            wrongTimer -= Time.deltaTime;
            Apply(blinkOn ? wrongColor : offColor, blinkOn);     // red flashing
        }
        else if (correctActive)
        {
            Apply(blinkOn ? correctColor : offColor, blinkOn);   // green flashing
        }
        else
        {
            Apply(offColor, false);                              // lamp off
        }
    }

    void Apply(Color color, bool lightOn)
    {
        if (bulb != null) bulb.material.color = color;
        if (glow != null) { glow.color = color; glow.enabled = lightOn; }
    }
}