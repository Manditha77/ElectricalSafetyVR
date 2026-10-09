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
    public bool zapWhenLive = false;  // small arc when a live damaged lead is pulled out

    [Header("Using a socket/cable model")]
    [Tooltip("Model parts shown while plugged in (e.g. the model's own plug). Hidden when unplugged.")]
    public GameObject[] modelWhilePlugged;
    [Tooltip("Our plug's own meshes: hidden while the model's plug is shown, shown once unplugged.")]
    public Renderer[] ownVisuals;

    public bool Plugged { get; private set; } = true;

    void Start()
    {
        if (seat != null) Park(seat.position, seat.rotation);
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
        if (hazard != null && !hazard.Spotted) hazard.Spot();
        if (Plugged) Unplug();
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

    public override void ForceSafe()
    {
        if (!Plugged || seat == null) return;
        if (held) ForceRelease();
        Plugged = false;
        if (socketIndicator != null) socketIndicator.enabled = false;
        if (HasModel)
        {
            foreach (var g in modelWhilePlugged) if (g != null) g.SetActive(false);
            ShowOwn(true);
        }
        Park(seat.position - seat.forward * 0.12f + Vector3.down * 0.05f, seat.rotation);
        Drop();
    }

    void Unplug()
    {
        Plugged = false;
        Sfx.PlayAt(Sfx.Click, seat.position, 0.9f);
        if (zapWhenLive) Sfx.PlayAt(Sfx.Zap, seat.position, 0.7f);
        if (socketIndicator != null) socketIndicator.enabled = false;
        if (HasModel)
        {
            foreach (var g in modelWhilePlugged) if (g != null) g.SetActive(false);
            ShowOwn(true);
        }
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
