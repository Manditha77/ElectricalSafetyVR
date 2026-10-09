using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

// Rechargeable emergency torch in a wall cradle.
// Grab = switches on. Trigger (activate) = on/off. Release near the cradle = docks and charges.
public class TorchControl : HazardGrabbable
{
    protected override string HazardKey => null;

    public Light beam;
    public Renderer lens;
    public Light chargeLed;
    public Renderer chargeLedRenderer;
    public Transform cradleSeat;
    public float dockRadius = 0.3f;

    public bool On { get; private set; }
    public bool Docked { get; private set; } = true;

    MaterialPropertyBlock mpb;

    protected override void Awake()
    {
        base.Awake();
        mpb = new MaterialPropertyBlock();
        if (grab != null) grab.activated.AddListener(_ => Switch(!On));
    }

    void Start()
    {
        Switch(false, silent: true);
        if (cradleSeat != null) Park(cradleSeat.position, cradleSeat.rotation);
    }

    protected override void OnGrab()
    {
        Docked = false;
        if (!On) Switch(true);
    }

    protected override void OnRelease()
    {
        if (cradleSeat != null && Vector3.Distance(transform.position, cradleSeat.position) < dockRadius)
        {
            Park(cradleSeat.position, cradleSeat.rotation);
            Docked = true;
            Switch(false);
            Sfx.PlayAt(Sfx.Click, cradleSeat.position, 0.6f);
            return;
        }
        Drop(); // stays on where it lands
    }

    protected override void Update()
    {
        base.Update();
        // green charging LED blinks slowly while docked: easy to find in the dark
        bool led = Docked && (Time.time % 1.6f) < 0.8f;
        if (chargeLed != null) { chargeLed.intensity = led ? 0.25f : 0.03f; chargeLed.range = 0.5f; }
        if (chargeLedRenderer != null)
        {
            chargeLedRenderer.GetPropertyBlock(mpb);
            mpb.SetColor("_EmissionColor", new Color(0.2f, 1f, 0.3f) * (led ? 1.2f : 0.1f));
            chargeLedRenderer.SetPropertyBlock(mpb);
        }
    }

    public void Switch(bool on, bool silent = false)
    {
        On = on;
        if (beam != null) beam.enabled = on;
        if (lens != null)
        {
            lens.GetPropertyBlock(mpb);
            mpb.SetColor("_EmissionColor", on ? new Color(1f, 0.95f, 0.8f) * 4f : Color.black);
            lens.SetPropertyBlock(mpb);
        }
        if (!silent) Sfx.PlayAt(Sfx.TorchClick, transform.position, 0.8f);
    }
}
