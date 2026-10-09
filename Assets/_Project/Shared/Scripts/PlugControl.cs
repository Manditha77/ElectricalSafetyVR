using UnityEngine;

// Grab and remove the plug from its socket to disconnect the item.
// The lead is drawn as a sagging cable from the plug to the item.
public class PlugControl : HazardGrabbable
{
    public Transform seat;          // where the plug sits in the socket
    public Transform leadAnchor;    // where the lead joins the item (cable / strip)
    public Transform leadPoint;     // where the lead leaves the plug
    public LineRenderer lead;

    public float leadLength = 1.5f;
    public float pullOut = 0.06f;
    public Renderer socketIndicator;  // red "ON" neon on the socket
    public bool zapWhenLive = false;  // small arc when a live damaged lead is pulled out

    [Header("Using a socket/cable model")]
    [Tooltip("Model parts shown while plugged in (e.g. the model's own plug). Hidden when unplugged.")]
    public GameObject[] modelWhilePlugged;
    [Tooltip("Our plug's own meshes: hidden while the model's plug is shown, shown once unplugged.")]
    public Renderer[] ownVisuals;

    public bool Plugged { get; private set; } = true;

    void Start()
    {
        if (seat != null)
            Park(seat.position, seat.rotation);
        ShowOwn(!HasModel);
    }

    bool HasModel
    {
        get
        {
            if (modelWhilePlugged == null) return false;
            foreach (var g in modelWhilePlugged) if (g != null) return true;
            return false;
        }
    }

    void ShowOwn(bool show)
    {
        if (ownVisuals != null) foreach (var r in ownVisuals) if (r != null) r.enabled = show;
        if (lead != null) lead.enabled = show;
    }

    // Pressing grip on the plug pulls it out straight away (reliable with the simulator ray).
    protected override void OnGrab()
    {
        if (hazard != null && !hazard.Spotted)
            hazard.Spot();

        if (Plugged && HazardBridge.Active)
            Unplug();
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
        if (!Plugged) return;
        Plugged = false;

        Sfx.PlayAt(Sfx.Click, seat.position, 0.9f);
        if (zapWhenLive)
            Sfx.PlayAt(Sfx.Zap, seat.position, 0.7f);

        if (socketIndicator != null)
            socketIndicator.enabled = false;

        // swap the model's plug for ours once it is pulled out
        if (HasModel)
        {
            foreach (var g in modelWhilePlugged) if (g != null) g.SetActive(false);
            ShowOwn(true);
        }

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