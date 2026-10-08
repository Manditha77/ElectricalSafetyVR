using System.Collections.Generic;
using UnityEngine;

// Shows the electrical fault in the room: flickering lights and a buzz while the
// workstation is live, and a status beacon (red / amber / green) that follows the safety state.
public class FaultAtmosphere : MonoBehaviour
{
    [Header("Room lighting")]
    public Transform lightingRoot;                    // Workshop_Main
    public string panelMaterialName = "Mat_LightPanel";

    [Header("Status beacon")]
    public Light beaconLight;
    public Renderer beaconLens;

    [Header("Sound (optional, 3D)")]
    public AudioSource faultHum;                      // looping buzz while the fault is live
    public AudioSource isolateSound;                  // one-shot when the supply is isolated

    [Header("Colours")]
    public Color liveColor = new Color(1f, 0.1f, 0.05f);
    public Color isolatedColor = new Color(1f, 0.65f, 0.05f);
    public Color safeColor = new Color(0.1f, 1f, 0.3f);

    enum Mode { Off, Live, Isolated, Safe, Done, Failed }
    Mode mode = Mode.Off;

    readonly List<Light> lights = new List<Light>();
    readonly List<float> baseIntensity = new List<float>();
    readonly List<Renderer> panels = new List<Renderer>();
    Color panelEmission = Color.white;
    MaterialPropertyBlock panelBlock;
    MaterialPropertyBlock lensBlock;
    bool flickering;
    float dipUntil, nextDip;

    void Start()
    {
        panelBlock = new MaterialPropertyBlock();
        lensBlock = new MaterialPropertyBlock();

        if (lightingRoot != null)
        {
            foreach (Light l in lightingRoot.GetComponentsInChildren<Light>(true))
            {
                if (l == beaconLight) continue;
                lights.Add(l);
                baseIntensity.Add(l.intensity);
            }
            foreach (Renderer r in lightingRoot.GetComponentsInChildren<Renderer>(true))
            {
                if (r.sharedMaterial == null || r.sharedMaterial.name != panelMaterialName) continue;
                panels.Add(r);
                panelEmission = r.sharedMaterial.GetColor("_EmissionColor");
            }
        }
        Refresh();
    }

    void OnEnable()
    {
        ElectricalSafetyManager.StateChanged += OnState;
        SessionManager.PhaseChanged += OnPhase;
    }

    void OnDisable()
    {
        ElectricalSafetyManager.StateChanged -= OnState;
        SessionManager.PhaseChanged -= OnPhase;
    }

    void OnState(ElectricalState state) { Refresh(); }
    void OnPhase(SessionPhase phase) { Refresh(); }

    void Refresh()
    {
        SessionManager s = SessionManager.Instance;
        ElectricalSafetyManager m = ElectricalSafetyManager.Instance;
        if (s == null || m == null) return;

        Mode next;
        if (s.Phase == SessionPhase.Result) next = s.Completed ? Mode.Done : Mode.Failed;
        else if (s.Phase != SessionPhase.Training) next = Mode.Off;
        else if (m.State == ElectricalState.Energised) next = Mode.Live;
        else if (m.State == ElectricalState.Isolated) next = Mode.Isolated;
        else if (m.State == ElectricalState.Restored) next = Mode.Done;
        else next = Mode.Safe;   // VerifiedSafe or Repaired

        if (mode == Mode.Live && next == Mode.Isolated && isolateSound != null) isolateSound.Play();
        mode = next;

        bool hum = mode == Mode.Live || mode == Mode.Failed;
        if (faultHum != null)
        {
            if (hum && !faultHum.isPlaying) faultHum.Play();
            if (!hum && faultHum.isPlaying) faultHum.Stop();
        }
    }

    void Update()
    {
        bool shouldFlicker = mode == Mode.Live || mode == Mode.Failed;
        if (shouldFlicker) ApplyLights(FlickerFactor());
        else if (flickering) ApplyLights(1f);    // back to normal once
        flickering = shouldFlicker;

        UpdateBeacon();
    }

    // Mostly unsteady light with random brown-out dips: a failing supply, not a strobe.
    float FlickerFactor()
    {
        float t = Time.time;
        if (t >= nextDip)
        {
            dipUntil = t + Random.Range(0.05f, 0.25f);
            nextDip = t + Random.Range(0.4f, 2.0f);
        }
        if (t < dipUntil) return Random.Range(0.05f, 0.35f);
        return 0.8f + 0.2f * Mathf.PerlinNoise(t * 10f, 0.5f);
    }

    void ApplyLights(float k)
    {
        for (int i = 0; i < lights.Count; i++)
            if (lights[i] != null) lights[i].intensity = baseIntensity[i] * k;

        foreach (Renderer r in panels)
        {
            if (r == null) continue;
            r.GetPropertyBlock(panelBlock);
            panelBlock.SetColor("_EmissionColor", panelEmission * k);
            r.SetPropertyBlock(panelBlock);
        }
    }

    void UpdateBeacon()
    {
        Color c = Color.black;
        bool on = false;

        switch (mode)
        {
            case Mode.Live:
            case Mode.Failed:
                c = liveColor;
                on = Mathf.FloorToInt(Time.time * 3f) % 2 == 0;   // 1.5 flashes per second
                break;
            case Mode.Isolated:
                c = isolatedColor; on = true; break;
            case Mode.Safe:
            case Mode.Done:
                c = safeColor; on = true; break;
        }

        if (beaconLight != null)
        {
            beaconLight.enabled = on;
            beaconLight.color = c;
        }
        if (beaconLens != null)
        {
            beaconLens.GetPropertyBlock(lensBlock);
            lensBlock.SetColor("_BaseColor", on ? c : c * 0.25f + Color.gray * 0.2f);
            lensBlock.SetColor("_EmissionColor", on ? c * 2.5f : Color.black);
            beaconLens.SetPropertyBlock(lensBlock);
        }
    }
}