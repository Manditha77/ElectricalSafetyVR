using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// A small electrical fire at the damaged live lead: flames, dark smoke, sparks, crackle and an orange glow.
// The plug cannot be pulled while it burns: first put the fire out with the CO2 extinguisher
// (ExtinguisherSpray), then unplug. Once unplugged the lead gets a red "DANGER - DO NOT USE" tag.
// A missed hazard (made safe for you) puts the fire out by itself.
public class ElectricalFire : MonoBehaviour
{
    public Hazard hazard;
    public PlugControl plug;
    public float secondsToPutOut = 3f;

    [Header("Materials (set by the set-up menu)")]
    public Material smokeMat;
    public Material flameMat;
    public Material sparkMat;

    ParticleSystem smoke, flames, sparks;
    Light glow;
    AudioSource crackle;
    float health = 1f;
    float nextZap;
    bool tagged;
    XRGrabInteractable plugGrab;
    InteractionLayerMask plugLayers;
    float baseSmoke, baseFlame;

    public bool Burning { get; private set; } = true;
    public Vector3 FirePoint => transform.position + Vector3.up * 0.08f;

    void Start()
    {
        smoke = MakePS("Smoke", smokeMat, 2.6f, 0.35f, 0.15f, 0.8f, new Color(0.12f, 0.12f, 0.12f, 0.75f), 22f, -0.05f, 15f);
        flames = MakePS("Flames", flameMat, 0.45f, 0.5f, 0.06f, 0.16f, new Color(1f, 0.55f, 0.15f, 0.9f), 40f, -0.2f, 18f);
        sparks = MakePS("Sparks", sparkMat, 0.5f, 1.8f, 0.012f, 0.02f, new Color(1f, 0.85f, 0.45f, 1f), 0f, 1f, 60f);
        var em = sparks.emission;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, 6, 12, 0, 0.7f) });
        baseSmoke = smoke.emission.rateOverTime.constant;
        baseFlame = flames.emission.rateOverTime.constant;

        var lg = new GameObject("FireGlow");
        lg.transform.SetParent(transform, false);
        lg.transform.localPosition = Vector3.up * 0.15f;
        glow = lg.AddComponent<Light>();
        glow.type = LightType.Point; glow.color = new Color(1f, 0.5f, 0.15f); glow.range = 2.5f; glow.shadows = LightShadows.None;

        crackle = gameObject.AddComponent<AudioSource>();
        crackle.clip = Sfx.Crackle; crackle.loop = true; crackle.volume = 0.45f;
        crackle.spatialBlend = 1f; crackle.minDistance = 0.6f; crackle.maxDistance = 12f; crackle.dopplerLevel = 0f;
        crackle.Play();

        if (plug != null)
        {
            plugGrab = plug.GetComponent<XRGrabInteractable>();
            if (plugGrab != null) { plugLayers = plugGrab.interactionLayers; plugGrab.interactionLayers = 0; }
        }
        if (hazard != null && hazard.Controlled) Extinguish(true);
    }

    ParticleSystem MakePS(string n, Material mat, float life, float speed, float size0, float size1, Color c, float rate, float gravity, float angle)
    {
        var go = new GameObject(n);
        go.transform.SetParent(transform, false);
        go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);   // emit upwards
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true; main.startLifetime = life; main.startSpeed = speed;
        main.startSize = new ParticleSystem.MinMaxCurve(size0, size0 * 1.6f);
        main.startColor = c; main.gravityModifier = gravity; main.maxParticles = 300;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var em = ps.emission; em.rateOverTime = rate;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = angle; sh.radius = 0.05f;
        var sz = ps.sizeOverLifetime; sz.enabled = true;
        sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, size1 / Mathf.Max(0.001f, size0)));
        var col = ps.colorOverLifetime; col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.4f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
        var r = go.GetComponent<ParticleSystemRenderer>();
        if (mat != null) r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ps.Play();
        return ps;
    }

    void Update()
    {
        if (Burning)
        {
            if (hazard != null && hazard.Controlled) { Extinguish(true); return; }   // made safe for you
            glow.intensity = (1.2f + 0.6f * Mathf.PerlinNoise(Time.time * 8f, 0f)) * health;
            if (Time.time > nextZap)
            {
                nextZap = Time.time + Random.Range(1.2f, 3f);
                Sfx.PlayAt(Sfx.Zap, FirePoint, 0.35f * health + 0.1f, Random.Range(0.9f, 1.2f));
            }
        }
        else if (!tagged && plug != null && !plug.Plugged && hazard != null && hazard.Controlled)
        {
            tagged = true;
            AddDangerTag(plug.transform);
        }
    }

    // called by the extinguisher every frame the spray hits the fire
    public void Hit(float dt)
    {
        if (!Burning) return;
        if (hazard != null && !hazard.Spotted) hazard.Spot();
        health = Mathf.Max(0f, health - dt / Mathf.Max(0.5f, secondsToPutOut));
        SetRate(smoke, baseSmoke * (0.3f + 0.7f * health));
        SetRate(flames, baseFlame * health);
        if (hazard != null) hazard.ReportProgress("Putting the fire out... " + Mathf.RoundToInt((1f - health) * 100f) + "%");
        if (health <= 0f) Extinguish(false);
    }

    static void SetRate(ParticleSystem ps, float r) { var em = ps.emission; em.rateOverTime = r; }

    public void Extinguish(bool silent)
    {
        if (!Burning) return;
        Burning = false;
        health = 0f;
        flames.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        sparks.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        SetRate(smoke, baseSmoke * 0.25f);
        Invoke(nameof(StopSmoke), 4f);
        glow.enabled = false;
        crackle.Stop();
        if (plugGrab != null) plugGrab.interactionLayers = plugLayers;
        if (!silent)
        {
            Sfx.PlayAt(Sfx.Chime, FirePoint, 0.6f);
            if (hazard != null) hazard.ReportProgress("Fire out. Now pull the plug out of the wall socket.");
            HazardBridge.Say("Fire out. Now pull the damaged lead's plug out of the wall socket.");
        }
    }

    void StopSmoke() { if (smoke != null) smoke.Stop(true, ParticleSystemStopBehavior.StopEmitting); }

    static void AddDangerTag(Transform at)
    {
        var tag = new GameObject("DangerTag");
        tag.transform.SetParent(at, false);
        tag.transform.localPosition = new Vector3(0f, -0.06f, 0f);
        var card = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(card.GetComponent<Collider>());
        card.transform.SetParent(tag.transform, false);
        card.transform.localScale = new Vector3(0.07f, 0.1f, 0.003f);
        var r = card.GetComponent<Renderer>();
        r.material.color = new Color(0.75f, 0.06f, 0.05f);
        foreach (float y in new[] { 0f, 180f })
        {
            var tg = new GameObject("Text");
            tg.transform.SetParent(tag.transform, false);
            tg.transform.localRotation = Quaternion.Euler(0f, y, 0f);
            tg.transform.localPosition = tg.transform.localRotation * new Vector3(0f, 0f, -0.002f);
            var t = tg.AddComponent<TextMeshPro>();
            t.text = "<b>DANGER</b>\nDO NOT\nUSE";
            t.fontSize = 0.12f; t.alignment = TextAlignmentOptions.Center; t.color = Color.white;
            t.rectTransform.sizeDelta = new Vector2(0.065f, 0.09f);
        }
        Sfx.PlayAt(Sfx.Rustle, at.position, 0.5f, 1.4f);
    }
}
