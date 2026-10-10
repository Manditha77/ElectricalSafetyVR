using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// CO2 fire extinguisher: grab it, aim with the controller, hold the trigger (Activate) to spray.
// A white CO2 cloud and a hiss; an ElectricalFire in the spray goes out after a few seconds.
[RequireComponent(typeof(XRGrabInteractable))]
public class ExtinguisherSpray : MonoBehaviour
{
    public float range = 4f;
    public Material sprayMat;

    XRGrabInteractable grab;
    Transform aim;
    bool spraying;
    ParticleSystem cloud;
    AudioSource hiss;
    Renderer[] rends;
    ElectricalFire[] fires;

    void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();
        grab.activated.AddListener(a => { aim = a.interactorObject.transform; spraying = true; });
        grab.deactivated.AddListener(_ => spraying = false);
        grab.selectExited.AddListener(_ => spraying = false);
        rends = GetComponentsInChildren<Renderer>();
    }

    void Start()
    {
        fires = FindObjectsByType<ElectricalFire>(FindObjectsSortMode.None);

        var go = new GameObject("CO2Spray");
        cloud = go.AddComponent<ParticleSystem>();
        cloud.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = cloud.main;
        main.loop = true; main.startLifetime = 0.6f; main.startSpeed = 7f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.12f);
        main.startColor = new Color(0.92f, 0.95f, 1f, 0.7f);
        main.simulationSpace = ParticleSystemSimulationSpace.World; main.maxParticles = 400;
        var em = cloud.emission; em.rateOverTime = 0f;
        var sh = cloud.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 10f; sh.radius = 0.02f;
        var sz = cloud.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 6f));
        var col = cloud.colorOverLifetime; col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
        var r = go.GetComponent<ParticleSystemRenderer>();
        if (sprayMat != null) r.sharedMaterial = sprayMat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        cloud.Play();

        hiss = gameObject.AddComponent<AudioSource>();
        hiss.clip = HissClip(); hiss.loop = true; hiss.volume = 0f;
        hiss.spatialBlend = 1f; hiss.minDistance = 0.6f; hiss.maxDistance = 12f; hiss.dopplerLevel = 0f;
        hiss.Play();
    }

    void Update()
    {
        bool on = spraying && grab.isSelected && aim != null;
        var em = cloud.emission;
        em.rateOverTime = on ? 160f : 0f;
        hiss.volume = Mathf.MoveTowards(hiss.volume, on ? 0.55f : 0f, Time.deltaTime * 4f);
        if (!on) return;

        Vector3 dir = aim.forward;
        Bounds b = rends.Length > 0 ? rends[0].bounds : new Bounds(transform.position, Vector3.one * 0.3f);
        foreach (var rr in rends) if (rr != null) b.Encapsulate(rr.bounds);
        Vector3 origin = b.center + dir * (b.extents.magnitude * 0.7f);
        cloud.transform.SetPositionAndRotation(origin, Quaternion.LookRotation(dir));

        foreach (var f in fires)
        {
            if (f == null || !f.Burning) continue;
            Vector3 to = f.FirePoint - origin;
            float along = Vector3.Dot(to, dir);
            if (along <= 0f || along > range) continue;
            float off = (to - dir * along).magnitude;
            if (off < 0.35f + along * 0.12f) f.Hit(Time.deltaTime);
        }
    }

    static AudioClip HissClip()
    {
        const int SR = 44100;
        int n = SR;
        var d = new float[n];
        var rnd = new System.Random(7);
        float lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float w = (float)(rnd.NextDouble() * 2 - 1);
            lp += (w - lp) * 0.55f;
            d[i] = (w - lp) * 0.5f + lp * 0.25f;      // bright hiss with some body
        }
        var c = AudioClip.Create("co2_hiss", n, 1, SR, false);
        c.SetData(d, 0);
        return c;
    }
}
