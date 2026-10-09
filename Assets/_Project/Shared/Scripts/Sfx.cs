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

    // ---------------- more workshop sounds ----------------
    static AudioClip clank, thunk, rustle, tick, hum, clunk, torchClick, lightsOn, powerDown, zap, padlock;
    static AudioClip[] steps, bootSteps;

    static float Partials(float t, float[] f, float[] decay, float amp)
    {
        float s = 0f;
        for (int i = 0; i < f.Length; i++) s += Sin(f[i], t) * Mathf.Exp(-t * decay[i]);
        return s * amp;
    }

    // Metal tool put down on the trolley
    public static AudioClip Clank
    {
        get
        {
            if (clank == null) clank = Make("sfx_clank", 0.7f, (t, r) =>
                Partials(t, new[] { 523f, 1187f, 1873f, 2711f }, new[] { 9f, 12f, 16f, 22f }, 0.18f) +
                Noise(r) * Mathf.Exp(-t * 120f) * 0.4f);
            return clank;
        }
    }

    // Plastic bottle dropped in the bin
    public static AudioClip Thunk
    {
        get
        {
            if (thunk == null)
            {
                float lp = 0f;
                thunk = Make("sfx_thunk", 0.45f, (t, r) =>
                {
                    lp += (Noise(r) - lp) * 0.3f;
                    float hit2 = t > 0.12f ? Mathf.Exp(-(t - 0.12f) * 35f) * 0.4f : 0f;
                    return Sin(150f, t) * Mathf.Exp(-t * 18f) * 0.6f + lp * (Mathf.Exp(-t * 30f) + hit2) * 0.8f;
                });
            }
            return thunk;
        }
    }

    // PPE put on (cloth / velcro)
    public static AudioClip Rustle
    {
        get
        {
            if (rustle == null)
            {
                float lp = 0f, am = 0f;
                rustle = Make("sfx_rustle", 0.55f, (t, r) =>
                {
                    float n = Noise(r);
                    lp += (n - lp) * 0.08f;
                    if (r.NextDouble() < 0.002) am = 0.6f + (float)r.NextDouble() * 0.4f;
                    am *= 0.9995f;
                    return (n - lp) * am * Mathf.Sin(Mathf.PI * t / 0.55f) * 0.35f;
                });
            }
            return rustle;
        }
    }

    // Countdown tick (last 10 s)
    public static AudioClip Tick
    {
        get
        {
            if (tick == null) tick = Make("sfx_tick", 0.06f, (t, r) => Sin(2000f, t) * Mathf.Exp(-t * 110f) * 0.4f);
            return tick;
        }
    }

    // Fluorescent / LED panel hum (1 s seamless loop)
    public static AudioClip Hum
    {
        get
        {
            if (hum == null) hum = Make("sfx_hum", 1f, (t, r) =>
                0.25f * Sin(100f, t) + 0.12f * Sin(200f, t) + 0.05f * Sin(300f, t) + 0.02f * Noise(r));
            return hum;
        }
    }

    // Isolator / breaker lever
    public static AudioClip Clunk
    {
        get
        {
            if (clunk == null) clunk = Make("sfx_clunk", 0.3f, (t, r) =>
            {
                float c2 = t > 0.035f ? Mathf.Exp(-(t - 0.035f) * 90f) : 0f;
                return Noise(r) * (Mathf.Exp(-t * 80f) + c2 * 0.6f) * 0.5f + Sin(170f, t) * Mathf.Exp(-t * 25f) * 0.5f;
            });
            return clunk;
        }
    }

    // Padlock / lockout clip
    public static AudioClip Padlock
    {
        get
        {
            if (padlock == null) padlock = Make("sfx_padlock", 0.3f, (t, r) =>
            {
                float t2 = t - 0.09f;
                float second = t2 > 0f ? Partials(t2, new[] { 2400f, 3700f }, new[] { 45f, 60f }, 0.25f) : 0f;
                return Partials(t, new[] { 1900f, 3100f }, new[] { 40f, 55f }, 0.2f) + second + Noise(r) * Mathf.Exp(-t * 200f) * 0.3f;
            });
            return padlock;
        }
    }

    public static AudioClip TorchClick
    {
        get
        {
            if (torchClick == null) torchClick = Make("sfx_torchclick", 0.08f, (t, r) =>
                Noise(r) * (Mathf.Exp(-t * 300f) + (t > 0.03f ? Mathf.Exp(-(t - 0.03f) * 300f) * 0.7f : 0f)) * 0.5f);
            return torchClick;
        }
    }

    // Lights coming back on: starter ticks then hum
    public static AudioClip LightsOn
    {
        get
        {
            if (lightsOn == null) lightsOn = Make("sfx_lightson", 1.1f, (t, r) =>
            {
                float s = 0f;
                foreach (var k in new[] { 0f, 0.16f, 0.37f })
                    if (t >= k) s += Noise(r) * Mathf.Exp(-(t - k) * 150f) * 0.5f;
                if (t > 0.4f) s += (0.2f * Sin(100f, t) + 0.1f * Sin(200f, t)) * Mathf.Clamp01((t - 0.4f) * 4f) * Mathf.Clamp01((1.1f - t) * 3f);
                return s;
            });
            return lightsOn;
        }
    }

    // Power going down
    public static AudioClip PowerDown
    {
        get
        {
            if (powerDown == null)
            {
                float ph = 0f;
                powerDown = Make("sfx_powerdown", 1.3f, (t, r) =>
                {
                    float f = Mathf.Lerp(220f, 35f, t / 1.3f);
                    ph += 2f * Mathf.PI * f / SR;
                    return Mathf.Sin(ph) * Mathf.Exp(-t * 2.2f) * 0.35f + Noise(r) * Mathf.Exp(-t * 40f) * 0.5f;
                });
            }
            return powerDown;
        }
    }

    // Small arc when a live plug is pulled
    public static AudioClip Zap
    {
        get
        {
            if (zap == null)
            {
                float f = 300f, ph = 0f;
                zap = Make("sfx_zap", 0.25f, (t, r) =>
                {
                    if (r.NextDouble() < 0.003) f = 150f + (float)r.NextDouble() * 900f;
                    ph += 2f * Mathf.PI * f / SR;
                    return (Noise(r) * 0.6f + Mathf.Sign(Mathf.Sin(ph)) * 0.3f) * Mathf.Exp(-t * 14f) * 0.5f;
                });
            }
            return zap;
        }
    }

    static AudioClip MakeStep(string name, bool boot)
    {
        float lp = 0f;
        float len = boot ? 0.3f : 0.18f;
        return Make(name, len, (t, r) =>
        {
            lp += (Noise(r) - lp) * (boot ? 0.12f : 0.2f);
            if (!boot)
                return lp * Mathf.Exp(-t * 35f) * 0.9f + Sin(85f, t) * Mathf.Exp(-t * 40f) * 0.45f;
            float heel = Noise(r) * Mathf.Exp(-t * 220f) * 0.45f;
            float toe = t > 0.07f ? lp * Mathf.Exp(-(t - 0.07f) * 30f) * 0.5f : 0f;
            return heel + lp * Mathf.Exp(-t * 20f) * 0.8f + Sin(60f, t) * Mathf.Exp(-t * 22f) * 0.7f + toe;
        });
    }

    // Footsteps: trainers (normal) or safety boots (heavier, heel click)
    public static AudioClip Step(bool boots, int i)
    {
        if (steps == null || steps[0] == null)
        {
            steps = new AudioClip[4]; bootSteps = new AudioClip[4];
            for (int k = 0; k < 4; k++) { steps[k] = MakeStep("sfx_step" + k, false); bootSteps[k] = MakeStep("sfx_boot" + k, true); }
        }
        var a = boots ? bootSteps : steps;
        return a[Mathf.Abs(i) % a.Length];
    }


    // ---------------- v5: drip, door, relay ----------------
    static AudioClip drip, creak, latch, doorShut, relay;

    // Water dripping from the spilled bottle ("plip")
    public static AudioClip Drip
    {
        get
        {
            if (drip == null) drip = Make("sfx_drip", 0.18f, (t, r) =>
            {
                float f = 900f + 2200f * Mathf.Clamp01(t / 0.035f);
                return Mathf.Sin(2f * Mathf.PI * f * t) * Mathf.Exp(-t * 45f) * 0.45f;
            });
            return drip;
        }
    }

    // Door hinge creak while opening
    public static AudioClip Creak
    {
        get
        {
            if (creak == null)
            {
                float ph = 0f, lp = 0f;
                creak = Make("sfx_creak", 0.9f, (t, r) =>
                {
                    float f = 170f + 60f * Mathf.Sin(t * 7f) + 25f * Mathf.Sin(t * 23f);
                    ph += 2f * Mathf.PI * f / SR;
                    float saw = (ph / Mathf.PI) % 2f - 1f;
                    lp += (saw - lp) * 0.25f;
                    float grain = 0.6f + 0.4f * Mathf.PerlinNoise(t * 60f, 0.5f);
                    float env = Mathf.Clamp01(t * 10f) * Mathf.Clamp01((0.9f - t) * 4f);
                    return lp * grain * env * 0.35f;
                });
            }
            return creak;
        }
    }

    // Door handle / latch click
    public static AudioClip Latch
    {
        get
        {
            if (latch == null) latch = Make("sfx_latch", 0.2f, (t, r) =>
                Noise(r) * Mathf.Exp(-t * 150f) * 0.5f +
                (t > 0.06f ? Noise(r) * Mathf.Exp(-(t - 0.06f) * 180f) * 0.35f : 0f) +
                Partials(t, new[] { 1900f, 3100f }, new[] { 60f, 80f }, 0.12f));
            return latch;
        }
    }

    // Door closing against the frame
    public static AudioClip DoorShut
    {
        get
        {
            if (doorShut == null)
            {
                float lp = 0f;
                doorShut = Make("sfx_doorshut", 0.6f, (t, r) =>
                {
                    lp += (Noise(r) - lp) * 0.08f;
                    return lp * Mathf.Exp(-t * 14f) * 1.4f + Sin(70f, t) * Mathf.Exp(-t * 16f) * 0.6f +
                           (t > 0.08f ? Noise(r) * Mathf.Exp(-(t - 0.08f) * 160f) * 0.3f : 0f);
                });
            }
            return doorShut;
        }
    }

    // Small relay "tick" when a panel lamp changes
    public static AudioClip Relay
    {
        get
        {
            if (relay == null) relay = Make("sfx_relay", 0.08f, (t, r) =>
                Noise(r) * Mathf.Exp(-t * 260f) * 0.4f + Sin(2600f, t) * Mathf.Exp(-t * 120f) * 0.15f);
            return relay;
        }
    }


    // ---------------- v5b: ventilation, glove snap, pass / fail ----------------
    static AudioClip vent, gloveSnap, passJingle, failTone;

    // Ventilation fan loop (soft air + low motor)
    public static AudioClip Vent
    {
        get
        {
            if (vent == null)
            {
                float lp = 0f, lp2 = 0f;
                vent = Make("sfx_vent", 3f, (t, r) =>
                {
                    lp += (Noise(r) - lp) * 0.05f;
                    lp2 += (lp - lp2) * 0.3f;
                    return lp2 * 0.9f + Sin(50f, t) * 0.04f + Sin(150f, t) * 0.02f;
                });
            }
            return vent;
        }
    }

    // Rubber glove pulled on (stretch + snap)
    public static AudioClip GloveSnap
    {
        get
        {
            if (gloveSnap == null)
            {
                float lp = 0f;
                gloveSnap = Make("sfx_glovesnap", 0.45f, (t, r) =>
                {
                    lp += (Noise(r) - lp) * 0.3f;
                    float stretch = t < 0.25f ? lp * Mathf.Sin(Mathf.PI * t / 0.25f) * 0.35f : 0f;
                    float snap = t > 0.27f ? (Noise(r) * Mathf.Exp(-(t - 0.27f) * 200f) * 0.7f + Sin(180f, t - 0.27f) * Mathf.Exp(-(t - 0.27f) * 40f) * 0.4f) : 0f;
                    return stretch + snap;
                });
            }
            return gloveSnap;
        }
    }

    // Results: passed (rising major arpeggio)
    public static AudioClip PassJingle
    {
        get
        {
            if (passJingle == null) passJingle = Make("sfx_pass", 1.6f, (t, r) =>
                0.22f * (Note(523f, t, 0f, 4f) + Note(659f, t, 0.15f, 4f) + Note(784f, t, 0.3f, 4f) + Note(1047f, t, 0.45f, 2.5f)));
            return passJingle;
        }
    }

    // Results: not passed (two falling low tones)
    public static AudioClip FailTone
    {
        get
        {
            if (failTone == null) failTone = Make("sfx_fail", 1.4f, (t, r) =>
                0.28f * (Note(392f, t, 0f, 3f) + Note(311f, t, 0.35f, 2.5f)));
            return failTone;
        }
    }

    // Plays a clip as a 3D (spatial) sound at a point.
    public static void PlayAt(AudioClip clip, Vector3 pos, float volume = 0.8f, float pitch = 1f)
    {
        if (clip == null) return;
        var go = new GameObject("Sfx_" + clip.name);
        go.transform.position = pos;
        var a = go.AddComponent<AudioSource>();
        a.clip = clip;
        a.volume = volume;
        a.pitch = pitch;
        a.spatialBlend = 1f;
        a.rolloffMode = AudioRolloffMode.Logarithmic;
        a.minDistance = 0.6f;
        a.maxDistance = 15f;
        a.dopplerLevel = 0f;
        a.Play();
        Object.Destroy(go, clip.length / Mathf.Max(0.1f, pitch) + 0.1f);
    }

    // A looping 3D source (hum, fizz...)
    public static AudioSource Loop(GameObject host, AudioClip clip, float volume, float maxDistance = 6f)
    {
        var a = host.AddComponent<AudioSource>();
        a.clip = clip; a.loop = true; a.volume = volume; a.spatialBlend = 1f;
        a.rolloffMode = AudioRolloffMode.Logarithmic; a.minDistance = 0.5f; a.maxDistance = maxDistance; a.dopplerLevel = 0f;
        a.Play();
        return a;
    }
}
