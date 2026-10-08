using UnityEngine;

public class BlinkingLight : MonoBehaviour
{
    public Light bulbLight;
    public Renderer bulb;
    public AudioSource warningSound;
    public float blinksPerSecond = 3f;
    public Color onColor = Color.red;
    public Color offColor = new Color(0.2f, 0f, 0f);

    void OnEnable()
    {
        if (warningSound != null) warningSound.Play();
    }

    void Update()
    {
        bool on = Mathf.FloorToInt(Time.time * blinksPerSecond * 2f) % 2 == 0;
        if (bulbLight != null) bulbLight.enabled = on;
        if (bulb != null) bulb.material.color = on ? onColor : offColor;
    }
}