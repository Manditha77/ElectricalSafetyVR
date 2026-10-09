using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// Put on NewFuse. When the trainee lets go of the fuse near the F2 slot (the hint says
// "RELEASE TO FIT"), the fuse is seated in the holder, locked there and the job step is counted.
// Works on its own - it does not depend on the old FuseHolder snap distance.
// Seat pose = where the blown fuse sat at the start (so the new one sits exactly the same way).
[DisallowMultipleComponent]
public class FuseSnapAssist : MonoBehaviour
{
    public Transform slot;                  // FuseSlot
    public Transform blownFuse;             // its start pose is the seat pose (optional)
    public float fitRadius = 0.2f;          // release within this distance of the seat = fitted
    public bool needCoverOpen = true;

    public bool Seated { get; private set; }

    XRGrabInteractable grab;
    Rigidbody body;
    Vector3 seatPos;
    Quaternion seatRot;
    float holdUntil;

    void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();
        body = GetComponent<Rigidbody>();
    }

    void Start()
    {
        if (blownFuse != null) { seatPos = blownFuse.position; seatRot = blownFuse.rotation; }
        else if (slot != null) { seatPos = slot.position; seatRot = slot.rotation; }
        else { enabled = false; return; }
        if (grab != null) grab.selectExited.AddListener(OnReleased);
    }

    void OnDestroy()
    {
        if (grab != null) grab.selectExited.RemoveListener(OnReleased);
    }

    public Vector3 SeatPosition => seatPos;

    void OnReleased(SelectExitEventArgs args)
    {
        if (Seated) return;
        float d = Vector3.Distance(transform.position, seatPos);
        if (slot != null) d = Mathf.Min(d, Vector3.Distance(transform.position, slot.position));
        if (d > fitRadius) return;

        var m = ElectricalSafetyManager.Instance;
        if (m != null)
        {
            if (needCoverOpen && !m.CoverOpen) { m.Say("Open the cover first, then fit the new fuse."); return; }
            if (!OldFuseRemoved(m)) { m.Say("[GUIDANCE] Remove the blown fuse with the fuse puller first."); return; }
        }
        StartCoroutine(Seat());
    }

    static bool OldFuseRemoved(ElectricalSafetyManager m)
    {
        var f = typeof(ElectricalSafetyManager).GetField("fuseRemoved", BindingFlags.NonPublic | BindingFlags.Instance);
        return f == null || (bool)f.GetValue(m);
    }

    IEnumerator Seat()
    {
        Seated = true;
        // let XRI / SnapItem finish their own release first, then take over
        yield return null;
        yield return new WaitForFixedUpdate();

        // stop anything that would drop it again
        foreach (var mb in GetComponents<MonoBehaviour>())
            if (mb != null && mb != this && mb.GetType().Name == "SnapItem") mb.enabled = false;
        if (grab != null)
        {
            if (grab.isSelected && grab.interactionManager != null)
                grab.interactionManager.CancelInteractableSelection((IXRSelectInteractable)grab);
            grab.enabled = false;           // fitted = stays in the holder
        }
        if (body != null)
        {
            if (!body.isKinematic) { body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
            body.useGravity = false;
            body.isKinematic = true;
        }

        PlaceAtSeat();
        holdUntil = Time.time + 1f;

        // count the step once (FuseHolder may already have done it)
        var m = ElectricalSafetyManager.Instance;
        if (m != null && m.State != ElectricalState.Repaired) m.FuseInserted();
    }

    void PlaceAtSeat()
    {
        if (body != null) { body.position = seatPos; body.rotation = seatRot; }
        transform.SetPositionAndRotation(seatPos, seatRot);
    }

    // keep it in place for a moment in case another script tries to move it
    void LateUpdate()
    {
        if (Seated && Time.time < holdUntil) PlaceAtSeat();
    }
}
