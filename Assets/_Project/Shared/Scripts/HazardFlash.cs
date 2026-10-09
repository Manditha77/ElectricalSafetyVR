using UnityEngine;

// Pulsing red light over a missed hazard during the review (about 1 flash per second, photosensitivity-safe).
[RequireComponent(typeof(Light))]
public class HazardFlash : MonoBehaviour
{
    public float rate = 1f;
    public float maxIntensity = 2.2f;
    Light l;

    void Awake() { l = GetComponent<Light>(); }

    void Update()
    {
        float k = 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.PI * 2f * rate);
        l.intensity = Mathf.Lerp(0.15f, maxIntensity, k * k);
    }
}
