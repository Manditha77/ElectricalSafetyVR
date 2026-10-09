using UnityEngine;

// Grab and remove the plug from its socket to disconnect the item.
public class PlugControl : HazardGrabbable
{
    public Transform seat;
    public Transform leadAnchor;
    public Transform leadPoint;
    public LineRenderer lead;

    public float leadLength = 1.5f;
    public float pullOut = 0.06f;
    public Renderer socketIndicator;

    public bool Plugged { get; private set; } = true;

    void Start()
    {
        if (seat != null)
            Park(seat.position, seat.rotation);
    }

    protected override void OnGrab()
    {
        if (hazard != null && !hazard.Spotted)
            hazard.Spot();
    }

    protected override void Update()
    {
        base.Update();

        if (seat == null || !HazardBridge.Active)
            return;

        CheckUnplugged();

        // Release the plug if the player stretches the cable too far.
        if (held && leadAnchor != null && leadPoint != null &&
            Vector3.Distance(leadPoint.position, leadAnchor.position)
                > leadLength + 0.3f)
        {
            ForceRelease();
        }
    }

    void CheckUnplugged()
    {
        if (seat == null || !HazardBridge.Active || !Plugged)
            return;

        if (Vector3.Distance(transform.position, seat.position) > pullOut)
            Unplug();
    }

    void Unplug()
    {
        Plugged = false;

        Sfx.PlayAt(Sfx.Click, seat.position, 0.9f);

        if (socketIndicator != null)
            socketIndicator.enabled = false;

        if (hazard != null)
            hazard.Identify();
    }

    protected override void OnRelease()
    {
        CheckUnplugged();

        if (leadAnchor != null && leadPoint != null)
        {
            Vector3 a = leadAnchor.position;
            Vector3 p = leadPoint.position;
            float d = Vector3.Distance(a, p);

            if (d > leadLength)
            {
                transform.position +=
                    (a - p).normalized * (d - leadLength + 0.05f);
            }
        }

        Drop();
    }

    void LateUpdate()
    {
        CheckUnplugged();

        if (lead != null && leadAnchor != null && leadPoint != null)
        {
            DrawLead(
                lead,
                leadPoint.position,
                leadAnchor.position,
                leadLength);
        }
    }

    public static void DrawLead(
        LineRenderer lr,
        Vector3 a,
        Vector3 b,
        float length)
    {
        const int N = 24;

        if (lr.positionCount != N)
            lr.positionCount = N;

        float d = Vector3.Distance(a, b);
        float sag = Mathf.Max(0.02f, (length - d) * 0.5f);
        float floor = lr.widthMultiplier * 0.5f + 0.004f;

        for (int i = 0; i < N; i++)
        {
            float t = i / (N - 1f);
            Vector3 p = Vector3.Lerp(a, b, t);

            p.y -= sag * 4f * t * (1f - t);

            if (p.y < floor)
                p.y = floor;

            lr.SetPosition(i, p);
        }
    }
}