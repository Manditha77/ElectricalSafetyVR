using System.Collections;
using UnityEngine;

// Loose spanner left on top of Workstation 2. Pick it up and put it on the TOOL RETURN trolley.
public class LooseToolControl : HazardGrabbable
{
    public Transform trolleySeat;
    public float returnRadius = 0.45f;

    public bool Stored { get; private set; }
    public bool OnMachine => !Stored && Vector3.Distance(transform.position, homePos) < 0.5f;

    protected override void OnGrab()
    {
        if (hazard != null && !hazard.Spotted) hazard.Spot();
    }

    protected override void OnRelease()
    {
        if (!HazardBridge.Active)
        {
            Stored = false;
            ReturnHome();
            return;
        }
        if (trolleySeat != null)
        {
            Vector3 d = transform.position - trolleySeat.position;
            float dy = d.y;
            d.y = 0f;
            if (d.magnitude < returnRadius && dy > -0.5f && dy < 0.7f)
            {
                Park(trolleySeat.position, trolleySeat.rotation * Quaternion.Euler(0f, Random.Range(-20f, 20f), 0f));
                Sfx.PlayAt(Sfx.Click, trolleySeat.position, 0.7f);
                Stored = true;
                if (hazard != null) hazard.Identify();
                return;
            }
        }
        Stored = false;
        Drop();
        StartCoroutine(CheckFloor());
    }

    IEnumerator CheckFloor()
    {
        yield return new WaitForSeconds(1.2f);
        if (!held && !Stored && transform.position.y < 0.3f && HazardBridge.Active)
            HazardBridge.Say("A tool on the floor is a trip hazard. Put it on the TOOL RETURN trolley.");
    }
}
