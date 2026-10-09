using UnityEngine;

// The bottle that caused the spill. Pick it up and drop it in the WASTE bin (remove the source).
public class SpillBottle : HazardGrabbable
{
    protected override string HazardKey => "Puddle";

    public Transform bin;            // bin centre (at floor level)
    public Transform binDrop;        // where the bottle rests inside the bin
    public float binRadius = 0.45f;
    public Renderer zoneHighlight;   // bin rim: tints green while the bottle is held over it
    MaterialPropertyBlock zmpb;
    public MopControl mop;

    public bool Binned { get; private set; }
    float nextDrip;

    protected override void OnGrab()
    {
        if (hazard != null && !hazard.Spotted) hazard.Spot();
    }

    protected override void Update()
    {
        if (Binned)
        {
            if (grab != null && grab.enabled && !held) grab.enabled = false;
            if (zoneHighlight != null) zoneHighlight.SetPropertyBlock(null);
            return;
        }
        base.Update();
        // it is still leaking: soft drips you can hear (a clue during the check)
        if (HazardBridge.Active && Time.time > nextDrip)
        {
            Sfx.PlayAt(Sfx.Drip, transform.position, 0.35f, Random.Range(0.85f, 1.2f));
            nextDrip = Time.time + Random.Range(1.1f, 2.3f);
        }
        if (zoneHighlight == null || bin == null) return;
        Vector3 d = transform.position - bin.position;
        float dy = d.y; d.y = 0f;
        if (zmpb == null) zmpb = new MaterialPropertyBlock();
        if (held && d.magnitude < binRadius && dy < 1.6f) { zmpb.SetColor("_BaseColor", new Color(0.2f, 0.8f, 0.3f)); zoneHighlight.SetPropertyBlock(zmpb); }
        else zoneHighlight.SetPropertyBlock(null);
    }

    protected override void OnRelease()
    {
        if (bin != null)
        {
            Vector3 d = transform.position - bin.position;
            float dy = d.y; d.y = 0f;
            if (d.magnitude < binRadius && dy < 1.6f)
            {
                Vector3 p = binDrop != null ? binDrop.position : bin.position + Vector3.up * 0.5f;
                Park(p, Quaternion.Euler(Random.Range(60f, 80f), Random.Range(0f, 360f), 0f));
                Sfx.PlayAt(Sfx.Thunk, p, 0.8f);
                Binned = true;
                if (hazard != null && !hazard.Controlled)
                {
                    if (mop != null && mop.Done) mop.TryComplete();
                    else if (mop == null) hazard.Identify();
                    else hazard.ReportProgress("Bottle binned. Now mop the spill with the SPILL KIT mop.");
                }
                return;
            }
        }
        Drop();
    }

    public override void ForceSafe()
    {
        if (Binned) return;
        if (held) ForceRelease();
        Vector3 p = binDrop != null ? binDrop.position : (bin != null ? bin.position + Vector3.up * 0.5f : transform.position);
        Park(p, Quaternion.Euler(70f, Random.Range(0f, 360f), 0f));
        Binned = true;
        if (grab != null) grab.enabled = false;
    }
}
