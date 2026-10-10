using UnityEngine;

// Sound for the Workstation 2 equipment cover: latch + hinge when it opens, a metal shut + latch when it closes.
// Listens to the cover's hinge angle and to the safety manager's CoverOpen, so it works with either.
public class CoverSound : MonoBehaviour
{
    public Transform cover;            // the part that swings (hinge)
    public float openAngle = 20f;      // degrees from closed = open
    public float closedAngle = 5f;     // within this = closed

    Quaternion closedRot;
    bool open;
    float lastPlay = -10f;

    void Start()
    {
        if (cover != null) closedRot = cover.localRotation;
        var m = ElectricalSafetyManager.Instance;
        open = m != null && m.CoverOpen;
    }

    void Update()
    {
        bool nowOpen = open;
        if (cover != null)
        {
            float a = Quaternion.Angle(cover.localRotation, closedRot);
            if (!open && a > openAngle) nowOpen = true;
            else if (open && a < closedAngle) nowOpen = false;
        }
        var m = ElectricalSafetyManager.Instance;
        if (m != null && m.CoverOpen != open && (cover == null || Quaternion.Angle(cover.localRotation, closedRot) < 0.5f))
            nowOpen = m.CoverOpen;      // cover moved by script without rotating this transform

        if (nowOpen == open) return;
        open = nowOpen;
        if (Time.time - lastPlay < 0.4f) return;
        lastPlay = Time.time;

        Vector3 p = cover != null ? cover.position : transform.position;
        if (open)
        {
            Sfx.PlayAt(Sfx.Latch, p, 0.8f, 1.15f);
            Sfx.PlayAt(Sfx.Creak, p, 0.35f, 1.6f);
        }
        else
        {
            Sfx.PlayAt(Sfx.DoorShut, p, 0.6f, 1.7f);
            Sfx.PlayAt(Sfx.Latch, p, 0.7f, 1.25f);
        }
    }
}
