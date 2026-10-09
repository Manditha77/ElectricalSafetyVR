using System.Collections.Generic;
using UnityEngine;

// Mop from the spill kit. Wherever the mop head passes, that part of the spill dries up gradually
// (shrinks and fades). A few small damp patches stay, so it looks "almost wiped out".
// The hazard is only made safe when the floor is mopped AND the spilled bottle is in the bin.
public class MopControl : HazardGrabbable
{
    protected override string HazardKey => "Puddle";

    public Transform head;                // mop head (touches the floor)
    public Transform puddle;              // Hazard_Puddle
    public SpillBottle bottle;            // optional: the source of the spill
    public float wipeRadius = 0.22f;      // how wide the mop head wipes
    public float wipeRate = 3.0f;         // wetness removed per metre of mopping
    public int remnants = 3;              // small damp patches left behind
    public float reach = 0.6f;            // height above the floor that still counts (simulator friendly)

    public bool Done { get; private set; }

    readonly List<Transform> blobs = new List<Transform>();
    readonly List<Vector3> scale0 = new List<Vector3>();
    readonly List<Renderer> rends = new List<Renderer>();
    readonly List<float> radius = new List<float>();
    readonly HashSet<int> keep = new HashSet<int>();
    float[] wet;
    Color baseColor = new Color(0.6f, 0.7f, 0.75f, 0.7f);
    MaterialPropertyBlock mpb;
    Bounds area;
    float nextSwish, nextReport;
    Vector3 last;
    bool hasLast;

    protected override void Awake()
    {
        base.Awake();
        mpb = new MaterialPropertyBlock();
        if (puddle == null && hazard != null) puddle = hazard.transform;
        if (puddle == null) return;

        foreach (var t in puddle.GetComponentsInChildren<Transform>(true))
        {
            // puddle patches are called "Water" (or "Blob" in older builds); skip the wet-floor sign
            if (!(t.name.StartsWith("Water") || t.name.StartsWith("Blob"))) continue;
            if (hazard != null && hazard.marker != null && t.IsChildOf(hazard.marker.transform)) continue;
            var r = t.GetComponent<Renderer>();
            if (r == null) continue;
            blobs.Add(t); scale0.Add(t.localScale); rends.Add(r);
            radius.Add(r != null ? Mathf.Max(r.bounds.extents.x, r.bounds.extents.z) : 0.1f);
        }
        wet = new float[blobs.Count];
        for (int i = 0; i < wet.Length; i++) wet[i] = 1f;

        // keep the smallest few as damp remnants
        var order = new List<int>();
        for (int i = 0; i < blobs.Count; i++) order.Add(i);
        order.Sort((a, b) => radius[a].CompareTo(radius[b]));
        for (int i = 0; i < Mathf.Min(remnants, order.Count); i++) keep.Add(order[i]);

        bool has = false;
        foreach (var r in rends)
        {
            if (r == null) continue;
            if (!has) { area = r.bounds; has = true; } else area.Encapsulate(r.bounds);
            if (r.sharedMaterial != null && r.sharedMaterial.HasProperty("_BaseColor")) baseColor = r.sharedMaterial.GetColor("_BaseColor");
        }
        if (!has)
        {
            var c = puddle.GetComponent<Collider>();
            area = c != null ? c.bounds : new Bounds(puddle.position, new Vector3(1.2f, 0.1f, 0.8f));
        }
    }

    protected override void Update()
    {
        base.Update();
        if (!held || Done || hazard == null || head == null || !HazardBridge.Active || blobs.Count == 0)
        {
            hasLast = false;
            return;
        }

        Vector3 h = head.position;
        bool over = h.x > area.min.x - wipeRadius && h.x < area.max.x + wipeRadius &&
                    h.z > area.min.z - wipeRadius && h.z < area.max.z + wipeRadius &&
                    h.y < area.max.y + reach;
        if (!over) { hasLast = false; return; }

        if (!hazard.Spotted) hazard.Spot();

        // remove the source first: mopping while the bottle still leaks does nothing
        if (bottle != null && !bottle.Binned)
        {
            if (Time.time > nextReport)
            {
                hazard.ReportProgress("Still leaking! Put the dripping bottle in the WASTE bin first.");
                nextReport = Time.time + 0.5f;
            }
            hasLast = false;
            return;
        }

        float moved = 0f;
        if (hasLast)
        {
            Vector3 d = h - last; d.y = 0f;
            moved = d.magnitude;
            if (moved > 0.5f) moved = 0f; // teleport / jump
        }
        last = h; hasLast = true;
        if (moved <= 0.0005f) return;

        if (moved > 0.003f && Time.time > nextSwish)
        {
            Sfx.PlayAt(Sfx.Swish, h, 0.55f, Random.Range(0.9f, 1.1f));
            nextSwish = Time.time + 0.28f;
        }

        // wipe the blobs under the mop head
        for (int i = 0; i < blobs.Count; i++)
        {
            if (wet[i] <= 0f) continue;
            Vector3 c = blobs[i].position;
            float dist = Vector2.Distance(new Vector2(h.x, h.z), new Vector2(c.x, c.z));
            float reachR = radius[i] + wipeRadius;
            if (dist > reachR) continue;
            float floor = keep.Contains(i) ? 0.2f : 0f;
            wet[i] = Mathf.Max(floor, wet[i] - moved * wipeRate * (1.2f - dist / reachR));
            Apply(i);
        }

        float sum = 0f; int n = 0;
        for (int i = 0; i < wet.Length; i++) if (!keep.Contains(i)) { sum += wet[i]; n++; }
        float avg = n > 0 ? sum / n : 0f;
        float pct = Mathf.Clamp01(1f - avg);

        if (Time.time > nextReport)
        {
            hazard.ReportProgress("Mopping... " + Mathf.RoundToInt(pct * 100f) + "%");
            nextReport = Time.time + 0.2f;
        }

        if (avg < 0.12f) Finish();
    }

    void Apply(int i)
    {
        float w = wet[i];
        if (w <= 0.02f && !keep.Contains(i)) { blobs[i].gameObject.SetActive(false); return; }
        float s = Mathf.Lerp(0.2f, 1f, w);
        blobs[i].localScale = new Vector3(scale0[i].x * s, scale0[i].y, scale0[i].z * s);
        var r = rends[i];
        if (r != null)
        {
            r.GetPropertyBlock(mpb);
            var c = baseColor; c.a = baseColor.a * Mathf.Lerp(0.3f, 1f, w);
            mpb.SetColor("_BaseColor", c);
            r.SetPropertyBlock(mpb);
        }
    }

    void Finish()
    {
        Done = true;
        for (int i = 0; i < blobs.Count; i++)
        {
            if (keep.Contains(i)) { wet[i] = 0.2f; Apply(i); }
            else blobs[i].gameObject.SetActive(false);
        }
        TryComplete();
    }

    public void TryComplete()
    {
        if (hazard == null || hazard.Controlled) return;
        if (bottle == null || bottle.Binned) hazard.Identify();
        else hazard.ReportProgress("Floor mopped. Now put the spilled bottle in the WASTE bin.");
    }

    // Let go = the mop goes back in the bucket.
    protected override void OnRelease() => ReturnHome();
}
