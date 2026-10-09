using UnityEngine;

// Outdoor night sound: crickets + a soft breeze, made in code (nothing to license). 3D, so it is quiet indoors.
[RequireComponent(typeof(AudioSource))]
public class NightAmbience : MonoBehaviour
{
    public float volume = 0.25f;

    void Start()
    {
        const int SR = 44100;
        int n = SR * 4;
        var d = new float[n];
        var rnd = new System.Random(3);
        float lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SR;
            // two crickets: chirp trains of 4 pulses at ~4.3 / 4.9 kHz
            float c1 = Chirp(t, 0.9f, 0.0f, 4300f);
            float c2 = Chirp(t, 1.3f, 0.37f, 4900f) * 0.7f;
            lp += ((float)(rnd.NextDouble() * 2 - 1) - lp) * 0.02f;   // breeze
            d[i] = (c1 + c2) * 0.18f + lp * 0.6f;
        }
        var clip = AudioClip.Create("night_ambience", n, 1, SR, false);
        clip.SetData(d, 0);
        var a = GetComponent<AudioSource>();
        a.clip = clip; a.loop = true; a.volume = volume; a.spatialBlend = 1f;
        a.rolloffMode = AudioRolloffMode.Linear; a.minDistance = 4f; a.maxDistance = 22f; a.dopplerLevel = 0f;
        a.Play();
    }

    static float Chirp(float t, float period, float offset, float f)
    {
        float local = (t + offset) % period;
        if (local > 0.16f) return 0f;
        float pulse = local % 0.04f;
        if (pulse > 0.025f) return 0f;
        float env = Mathf.Sin(Mathf.PI * pulse / 0.025f);
        return Mathf.Sin(2f * Mathf.PI * f * t) * env;
    }
}
