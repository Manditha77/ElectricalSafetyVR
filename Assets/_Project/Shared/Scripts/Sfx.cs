using UnityEngine;

// Procedural 3D sound effects (no audio files needed, so nothing to licence).
public static class Sfx
{
    const int SR = 44100;
    static AudioClip ding, alert, chime, buzz, crackle, click, swish, splash, fizz;

    delegate float Wave(float t, System.Random r);

    static AudioClip Make(string name, float seconds, Wave w)
    {
        int n = Mathf.CeilToInt(seconds * SR);
        var data = new float[n];
        var rnd = new System.Random(name.GetHashCode());
        for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(w(i / (float)SR, rnd), -1f, 1f);
        var c = AudioClip.Create(name, n, 1, SR, false);
        c.SetData(data, 0);
        c.hideFlags = HideFlags.DontUnloadUnusedAsset;
        return c;
    }

    static float Sin(float f, float t) => Mathf.Sin(2f * Mathf.PI * f * t);
    static float Noise(System.Random r) => (float)(r.NextDouble() * 2.0 - 1.0);

    static float Note(float f, float t, float start, float decay)
    {
        if (t < start) return 0f;
        float u = t - start;
        return (Sin(f, u) + 0.3f * Sin(f * 2f, u)) * Mathf.Exp(-u * decay) * Mathf.Clamp01(u * 400f);
    }

    // Spotted a caution hazard
    public static AudioClip Ding
    {
        get
        {
            if (ding == null) ding = Make("sfx_ding", 0.8f, (t, r) => 0.35f * (Note(880f, t, 0f, 9f) + Note(1320f, t, 0.12f, 7f)));
            return ding;
        }
    }

    // Spotted a danger hazard (two low warning tones)
    public static AudioClip Alert
    {
        get
        {
            if (alert == null) alert = Make("sfx_alert", 0.8f, (t, r) => 0.35f * (Note(587f, t, 0f, 8f) + Note(440f, t, 0.18f, 6f)));
            return alert;
        }
    }

    // Hazard controlled
    public static AudioClip Chime
    {
        get
        {
            if (chime == null) chime = Make("sfx_chime", 1.1f, (t, r) =>
                0.28f * (Note(660f, t, 0f, 6f) + Note(880f, t, 0.1f, 6f) + Note(1320f, t, 0.2f, 5f)));
            return chime;
        }
    }

    // Unsafe consequence
    public static AudioClip Buzz
    {
        get
        {
            if (buzz == null) buzz = Make("sfx_buzz", 0.55f, (t, r) =>
            {
                float env = Mathf.Clamp01(t * 60f) * Mathf.Clamp01((0.55f - t) * 12f);
                return env * 0.22f * (Mathf.Sign(Sin(110f, t)) + 0.5f * Sin(220f, t));
            });
            return buzz;
        }
    }

    // Electrical arcing crackle
    public static AudioClip Crackle
    {
        get
        {
            if (crackle == null)
            {
                float e = 0f, lp = 0f;
                crackle = Make("sfx_crackle", 0.7f, (t, r) =>
                {
                    if (r.NextDouble() < 0.004) e = 0.6f + (float)r.NextDouble() * 0.4f;
                    e *= 0.996f;
                    lp += (Noise(r) - lp) * 0.6f;
                    float env = Mathf.Clamp01((0.7f - t) * 4f);
                    return lp * e * env + 0.05f * Sin(100f, t) * env * e;
                });
            }
            return crackle;
        }
    }

    public static AudioClip Click
    {
        get
        {
            if (click == null) click = Make("sfx_click", 0.12f, (t, r) =>
                Noise(r) * Mathf.Exp(-t * 90f) * 0.6f + Sin(1800f, t) * Mathf.Exp(-t * 70f) * 0.25f);
            return click;
        }
    }

    // Mop swish
    public static AudioClip Swish
    {
        get
        {
            if (swish == null)
            {
                float lp = 0f;
                swish = Make("sfx_swish", 0.35f, (t, r) =>
                {
                    lp += (Noise(r) - lp) * 0.15f;
                    return lp * Mathf.Sin(Mathf.PI * t / 0.35f) * 0.9f;
                });
            }
            return swish;
        }
    }

    // Stepping into water
    public static AudioClip Splash
    {
        get
        {
            if (splash == null)
            {
                float lp = 0f;
                splash = Make("sfx_splash", 0.7f, (t, r) =>
                {
                    lp += (Noise(r) - lp) * (0.35f - 0.3f * t / 0.7f);
                    return lp * Mathf.Exp(-t * 5f) * 1.2f + Sin(90f, t) * Mathf.Exp(-t * 14f) * 0.4f;
                });
            }
            return splash;
        }
    }

    // Overheating loop (hiss with pops)
    public static AudioClip Fizz
    {
        get
        {
            if (fizz == null)
            {
                float lp = 0f, e = 0f;
                fizz = Make("sfx_fizz", 2f, (t, r) =>
                {
                    lp += (Noise(r) - lp) * 0.25f;
                    if (r.NextDouble() < 0.0006) e = 0.8f;
                    e *= 0.993f;
                    return lp * 0.35f + Noise(r) * e * 0.5f + 0.06f * Sin(100f, t);
                });
            }
            return fizz;
        }
    }

    // Plays a clip as a 3D (spatial) sound at a point.
    public static void PlayAt(AudioClip clip, Vector3 pos, float volume = 0.8f)
    {
        if (clip == null) return;
        var go = new GameObject("Sfx_" + clip.name);
        go.transform.position = pos;
        var a = go.AddComponent<AudioSource>();
        a.clip = clip;
        a.volume = volume;
        a.spatialBlend = 1f;
        a.rolloffMode = AudioRolloffMode.Logarithmic;
        a.minDistance = 0.6f;
        a.maxDistance = 15f;
        a.dopplerLevel = 0f;
        a.Play();
        Object.Destroy(go, clip.length + 0.1f);
    }
}
