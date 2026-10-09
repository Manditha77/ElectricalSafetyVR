using System.Collections.Generic;
using UnityEngine;

// Isolating Workstation 2 also cuts the workshop lighting (it shares the old W2 sub-circuit):
// room lights + light panels go out, the beacon turns yellow and the W2 isolator lamp blinks green.
// The trainee finds the emergency torch (blinking green charging LED under the beacon) and carries on
// with the job by torchlight. Restoring the supply (or the end of the session) brings the lights back.
// Runs after other scripts (FaultAtmosphere) so its overrides win while the blackout lasts.
[DefaultExecutionOrder(1000)]
public class BlackoutController : MonoBehaviour
{
    public enum Trigger { W2Isolated, W1LeverOff }
    public Trigger trigger = Trigger.W2Isolated;
    public Transform w1Lever;            // only for Trigger.W1LeverOff
    public float offAngle = 30f;
    public float torchReminderAfter = 8f;
    public TorchControl torch;

    [Header("Stay on in the dark")]
    public Light beaconLight;
    public Renderer beaconLens;
    [Tooltip("Isolator lamp that blinks green in the dark (W2 lamp)")]
    [UnityEngine.Serialization.FormerlySerializedAs("w1Lamp")] public Light statusLamp;
    [UnityEngine.Serialization.FormerlySerializedAs("w1LampRenderer")] public Renderer statusLampRenderer;
    public Renderer[] exitSigns;

    [Header("Darkness")]
    public string[] keepUnder = { "StatusBeacon", "HazardControls", "BlackoutSystem", "BreakerPanel", "Workstation2", "PPE_Station" };
    public float ambientDark = 0.04f;
    public bool darkFog = true;
    public float fogDensity = 0.16f;

    public bool Blackout { get; private set; }

    Quaternion leverOn;
    readonly List<Light> roomLights = new List<Light>();
    readonly List<float> savedIntensity = new List<float>();
    readonly List<Renderer> panels = new List<Renderer>();
    MaterialPropertyBlock mpb;
    float startTime;

    // saved render settings
    float ambInt, reflInt, fogDens, beaconRange;
    Color ambLight, ambSky, ambEq, ambGround, fogCol;
    bool fogOn;
    FogMode fogMode;

    static readonly Color Yellow = new Color(1f, 0.72f, 0.1f);
    static readonly Color Green = new Color(0.2f, 1f, 0.35f);

    void Start()
    {
        mpb = new MaterialPropertyBlock();
        if (w1Lever != null) leverOn = w1Lever.localRotation;

        foreach (var l in FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (!IsKept(l.transform) && l != beaconLight && l != statusLamp) roomLights.Add(l);

        foreach (var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            if (r.sharedMaterial != null && r.sharedMaterial.name.StartsWith("Mat_LightPanel")) panels.Add(r);
    }

    bool IsKept(Transform t)
    {
        for (var p = t; p != null; p = p.parent)
            foreach (var k in keepUnder) if (p.name == k) return true;
        return false;
    }

    void LateUpdate()
    {
        bool off;
        if (trigger == Trigger.W2Isolated)
        {
            string st = HazardBridge.State;
            off = HazardBridge.Phase == "Training" && (st == "Isolated" || st == "VerifiedSafe" || st == "Repaired");
        }
        else
        {
            if (w1Lever == null) return;
            off = Quaternion.Angle(w1Lever.localRotation, leverOn) > offAngle && HazardBridge.Phase != "Result";
        }
        if (off && !Blackout) Begin();
        else if (!off && Blackout) End();
        if (Blackout)
        {
            Apply();
            if (!reminded && Time.time - startTime > torchReminderAfter && (torch == null || torch.Docked))
            {
                reminded = true;
                HazardBridge.Say("It is too dark to work safely. Take the EMERGENCY TORCH: follow the blinking green light under the yellow beacon.");
            }
        }
    }

    bool reminded;

    void Begin()
    {
        Blackout = true;
        startTime = Time.time;
        savedIntensity.Clear();
        foreach (var l in roomLights) savedIntensity.Add(l != null ? l.intensity : 0f);

        ambInt = RenderSettings.ambientIntensity; ambLight = RenderSettings.ambientLight;
        ambSky = RenderSettings.ambientSkyColor; ambEq = RenderSettings.ambientEquatorColor; ambGround = RenderSettings.ambientGroundColor;
        reflInt = RenderSettings.reflectionIntensity;
        fogOn = RenderSettings.fog; fogCol = RenderSettings.fogColor; fogMode = RenderSettings.fogMode; fogDens = RenderSettings.fogDensity;
        if (beaconLight != null) beaconRange = beaconLight.range;

        RenderSettings.ambientIntensity = ambientDark;
        RenderSettings.ambientLight = ambLight * ambientDark;
        RenderSettings.ambientSkyColor = ambSky * ambientDark;
        RenderSettings.ambientEquatorColor = ambEq * ambientDark;
        RenderSettings.ambientGroundColor = ambGround * ambientDark;
        RenderSettings.reflectionIntensity = 0.1f;
        if (darkFog)
        {
            RenderSettings.fog = true; RenderSettings.fogColor = Color.black;
            RenderSettings.fogMode = FogMode.ExponentialSquared; RenderSettings.fogDensity = fogDensity;
        }

        reminded = false;
        Vector3 at = w1Lever != null ? w1Lever.position : (statusLamp != null ? statusLamp.transform.position : transform.position);
        Sfx.PlayAt(Sfx.PowerDown, at, 0.9f);
        foreach (var p in panels) if (p != null) Sfx.PlayAt(Sfx.Click, p.bounds.center, 0.4f);
        HazardBridge.Say(trigger == Trigger.W2Isolated
            ? "The lights went out: the workshop lighting is on the same circuit as W2. Get the EMERGENCY TORCH (blinking green light under the beacon) and carry on safely."
            : "The lights went out! W1 also feeds the workshop lighting. Find the EMERGENCY TORCH (green light by the beacon), then switch W1 back ON.");
    }

    void Apply()
    {
        float t = Time.time - startTime;
        // brief dying flicker, then dark
        float k = t < 0.5f ? (Mathf.PerlinNoise(t * 25f, 0f) > 0.55f ? 0.6f : 0f) * (1f - t / 0.5f) : 0f;
        for (int i = 0; i < roomLights.Count; i++)
            if (roomLights[i] != null) roomLights[i].intensity = savedIntensity[i] * k;

        foreach (var p in panels)
        {
            if (p == null) continue;
            p.GetPropertyBlock(mpb);
            mpb.SetColor("_EmissionColor", Color.black);
            mpb.SetColor("_BaseColor", new Color(0.25f, 0.25f, 0.25f));
            p.SetPropertyBlock(mpb);
        }

        // rotating-beacon style yellow pulse (slow, < 1 flash per second)
        float pulse = 0.6f + 0.4f * Mathf.Sin(Time.time * Mathf.PI * 1.6f);
        if (beaconLight != null)
        {
            beaconLight.color = Yellow;
            beaconLight.intensity = 1.6f * pulse;
            beaconLight.range = Mathf.Max(beaconRange, 5f);
            beaconLight.enabled = true;
        }
        SetEmission(beaconLens, Yellow * (2.5f * pulse));

        // isolator lamp blinks green (1 Hz)
        bool onBlink = (Time.time % 1f) < 0.5f;
        if (statusLamp != null) { statusLamp.enabled = true; statusLamp.color = Green; statusLamp.intensity = onBlink ? 1.2f : 0.15f; statusLamp.range = Mathf.Max(statusLamp.range, 1.5f); }
        SetEmission(statusLampRenderer, Green * (onBlink ? 3f : 0.3f));

        // emergency EXIT sign stays lit
        if (exitSigns != null) foreach (var e in exitSigns) SetEmission(e, new Color(0.3f, 1f, 0.45f) * 1.5f);
    }

    void SetEmission(Renderer r, Color c)
    {
        if (r == null) return;
        r.GetPropertyBlock(mpb);
        mpb.SetColor("_EmissionColor", c);
        mpb.SetColor("_BaseColor", new Color(Mathf.Clamp01(c.r), Mathf.Clamp01(c.g), Mathf.Clamp01(c.b)));
        r.SetPropertyBlock(mpb);
    }

    void End()
    {
        Blackout = false;
        for (int i = 0; i < roomLights.Count; i++)
            if (roomLights[i] != null) roomLights[i].intensity = savedIntensity[i];

        foreach (var p in panels) if (p != null) { p.SetPropertyBlock(null); Sfx.PlayAt(Sfx.LightsOn, p.bounds.center, 0.5f); }
        if (beaconLens != null) beaconLens.SetPropertyBlock(null);
        if (statusLampRenderer != null) statusLampRenderer.SetPropertyBlock(null);
        if (exitSigns != null) foreach (var e in exitSigns) if (e != null) e.SetPropertyBlock(null);
        if (beaconLight != null) beaconLight.range = beaconRange;

        RenderSettings.ambientIntensity = ambInt; RenderSettings.ambientLight = ambLight;
        RenderSettings.ambientSkyColor = ambSky; RenderSettings.ambientEquatorColor = ambEq; RenderSettings.ambientGroundColor = ambGround;
        RenderSettings.reflectionIntensity = reflInt;
        RenderSettings.fog = fogOn; RenderSettings.fogColor = fogCol; RenderSettings.fogMode = fogMode; RenderSettings.fogDensity = fogDens;

        HazardBridge.Say(trigger == Trigger.W2Isolated
            ? "Lights back on. Put the torch back in its charger."
            : "Lights restored. Now isolate the correct circuit: W2.");
    }
}
