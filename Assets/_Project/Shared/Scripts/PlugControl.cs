using UnityEngine;

// A mains plug in a wall socket. Grab it and pull it out = the item is disconnected.
// The lead is drawn as a sagging cable from the plug to the item.
public class PlugControl : HazardGrabbable
{
    public Transform seat;          // where the plug sits in the socket
    public Transform leadAnchor;    // where the lead joins the item (cable / strip)
    public Transform leadPoint;     // where the lead leaves the plug
    public LineRenderer lead;
    public float leadLength = 1.5f;
    public float pullOut = 0.06f;
    public Renderer socketIndicator; // red "ON" neon on the socket

    public bool Plugged { get; private set; } = true;

    void Start()
    {
        if (seat != null) Park(seat.position, seat.rotation);
    }

    protected override void OnGrab()
    {
        if (hazard != null && !hazard.Spotted) hazard.Spot();
    }

    protected override void Update()
    {
        base.Update();
        if (seat == null) return;

        if (held && Plugged && Vector3.Distance(transform.position, seat.position) > pullOut)
            Unplug();

        // The lead is only so long: if the hand goes too far, the plug slips out of it.
        if (held && leadAnchor != null && leadPoint != null &&
            Vector3.Distance(leadPoint.position, leadAnchor.position) > leadLength + 0.3f)
            ForceRelease();
    }

    void Unplug()
    {
        Plugged = false;
        Sfx.PlayAt(Sfx.Click, seat.position, 0.9f);
        if (socketIndicator != null) socketIndicator.enabled = false;
        if (hazard != null) hazard.Identify();
    }

    protected override void OnRelease()
    {
        if (leadAnchor != null && leadPoint != null)
        {
            Vector3 a = leadAnchor.position, p = leadPoint.position;
            float d = Vector3.Distance(a, p);
            if (d > leadLength) transform.position += (a - p).normalized * (d - leadLength + 0.05f);
        }
        Drop();
    }

    void LateUpdate()
    {
        if (lead != null && leadAnchor != null && leadPoint != null)
            DrawLead(lead, leadPoint.position, leadAnchor.position, leadLength);
    }

    public static void DrawLead(LineRenderer lr, Vector3 a, Vector3 b, float length)
    {
        const int N = 24;
        if (lr.positionCount != N) lr.positionCount = N;
        float d = Vector3.Distance(a, b);
        float sag = Mathf.Max(0.02f, (length - d) * 0.5f);
        float floor = lr.widthMultiplier * 0.5f + 0.004f;
        for (int i = 0; i < N; i++)
        {
            float t = i / (N - 1f);
            Vector3 p = Vector3.Lerp(a, b, t);
            p.y -= sag * 4f * t * (1f - t);
            if (p.y < floor) p.y = floor;
            lr.SetPosition(i, p);
        }
    }
}
