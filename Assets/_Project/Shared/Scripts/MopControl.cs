using System.Collections.Generic;
using UnityEngine;

// Mop from the spill kit. Rub the mop head over the spill to clean it.
// The puddle shrinks as you mop; when it is clean the wet-floor sign goes up.
public class MopControl : HazardGrabbable
{
    public Transform head;          // mop head (point that touches the floor)
    public Transform puddle;        // Hazard_Puddle
    public float cleanDistance = 1.6f;   // metres of mopping needed
    public float reach = 0.6f;           // how high above the floor still counts (easier in the simulator)

    readonly List<Transform> blobs = new List<Transform>();
    readonly List<Vector3> blobScale = new List<Vector3>();
    Bounds area;
    float progress, nextSwish, nextReport;
    Vector3 last;
    bool hasLast, done;

    protected override void Awake()
    {
        base.Awake();
        if (puddle == null && hazard != null) puddle = hazard.transform;
        if (puddle == null) return;

        foreach (var t in puddle.GetComponentsInChildren<Transform>(true))
            if (t.name.StartsWith("Blob")) { blobs.Add(t); blobScale.Add(t.localScale); }

        bool has = false;
        foreach (var b in blobs)
        {
            var r = b.GetComponent<Renderer>();
            if (r == null) continue;
            if (!has) { area = r.bounds; has = true; } else area.Encapsulate(r.bounds);
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
        if (!held || done || hazard == null || hazard.Controlled || head == null || !HazardBridge.Active)
        {
            hasLast = false;
            return;
        }

        Vector3 h = head.position;
        bool over = h.x > area.min.x - 0.15f && h.x < area.max.x + 0.15f &&
                    h.z > area.min.z - 0.15f && h.z < area.max.z + 0.15f &&
                    h.y < area.max.y + reach;
        if (!over) { hasLast = false; return; }

        if (!hazard.Spotted) hazard.Spot();

        if (hasLast)
        {
            Vector3 d = h - last;
            d.y = 0f;
            float m = d.magnitude;
            if (m < 0.5f)
            {
                progress += m;
                if (m > 0.003f && Time.time > nextSwish)
                {
                    Sfx.PlayAt(Sfx.Swish, h, 0.5f);
                    nextSwish = Time.time + 0.3f;
                }
            }
        }
        last = h;
        hasLast = true;

        float p = Mathf.Clamp01(progress / cleanDistance);
        float s = Mathf.Lerp(1f, 0.12f, p);
        for (int i = 0; i < blobs.Count; i++)
            blobs[i].localScale = new Vector3(blobScale[i].x * s, blobScale[i].y, blobScale[i].z * s);

        if (Time.time > nextReport)
        {
            hazard.ReportProgress("Mopping... " + Mathf.RoundToInt(p * 100f) + "%");
            nextReport = Time.time + 0.2f;
        }

        if (p >= 1f)
        {
            done = true;
            // leave only a faint damp patch
            for (int i = 0; i < blobs.Count; i++) blobs[i].gameObject.SetActive(i % 5 == 0);
            hazard.Identify();
        }
    }

    // Let go = the mop goes back in the bucket.
    protected override void OnRelease() => ReturnHome();
}
