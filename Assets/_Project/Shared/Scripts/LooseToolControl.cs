using System.Collections;
using UnityEngine;

// Loose spanner left on top of Workstation 2. Pick it up and put it on the TOOL RETURN trolley.
public class LooseToolControl : HazardGrabbable
{
    protected override string HazardKey => "MetalTool";

    public Transform trolleySeat;
    public float returnRadius = 0.55f;
    public Renderer zoneHighlight;   // trolley tray: tints green while the tool is held over it

    public bool Stored { get; private set; }
    public bool OnMachine => !Stored && Vector3.Distance(transform.position, homePos) < 0.5f;

    protected override void OnGrab()
    {
        if (hazard != null && !hazard.Spotted) hazard.Spot();
    }

    protected override void OnRelease()
    {
        if (trolleySeat != null)
        {
            Vector3 d = transform.position - trolleySeat.position;
            float dy = d.y;
            d.y = 0f;
            if (d.magnitude < returnRadius && dy > -0.6f && dy < 1.2f)
            {
                Park(trolleySeat.position, trolleySeat.rotation * Quaternion.Euler(0f, Random.Range(-20f, 20f), 0f));
                Sfx.PlayAt(Sfx.Clank, trolleySeat.position, 0.7f);
                Stored = true;
                if (hazard != null) hazard.Identify();
                return;
            }
        }
        Stored = false;
        Drop();
        StartCoroutine(CheckFloor());
    }

    MaterialPropertyBlock zmpb;

    protected override void Update()
    {
        base.Update();
        if (zoneHighlight == null || trolleySeat == null) return;
        Vector3 d = transform.position - trolleySeat.position;
        float dy = d.y; d.y = 0f;
        bool over = held && d.magnitude < returnRadius && dy > -0.6f && dy < 1.2f;
        if (zmpb == null) zmpb = new MaterialPropertyBlock();
        if (over) { zmpb.SetColor("_BaseColor", new Color(0.2f, 0.8f, 0.3f)); zoneHighlight.SetPropertyBlock(zmpb); }
        else zoneHighlight.SetPropertyBlock(null);
    }

    IEnumerator CheckFloor()
    {
        yield return new WaitForSeconds(1.2f);
        if (!held && !Stored && transform.position.y < 0.3f && HazardBridge.Active)
            HazardBridge.Say("A tool on the floor is a trip hazard. Put it on the TOOL RETURN trolley.");
    }
}
