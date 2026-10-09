using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// Base for real objects used to control a hazard (mop, plug, spanner).
// They can only be used during the pre-work check and the training (not in the briefing).
[RequireComponent(typeof(Rigidbody))]
public abstract class HazardGrabbable : MonoBehaviour
{
    public Hazard hazard;

    protected XRGrabInteractable grab;
    protected Rigidbody rb;
    protected Vector3 homePos;
    protected Quaternion homeRot;
    protected bool held;

    public bool IsHeld => held;

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody>();
        grab = GetComponent<XRGrabInteractable>();
        homePos = transform.position;
        homeRot = transform.rotation;
        if (grab != null)
        {
            grab.selectEntered.AddListener(OnSelectEntered);
            grab.selectExited.AddListener(OnSelectExited);
        }
    }

    void OnSelectEntered(SelectEnterEventArgs args)
    {
        held = true;
        OnGrab();
    }

    void OnSelectExited(SelectExitEventArgs args)
    {
        held = false;
        if (isActiveAndEnabled) StartCoroutine(ReleaseNextStep());
    }

    // XRI restores the Rigidbody after release; act one physics step later.
    IEnumerator ReleaseNextStep()
    {
        yield return new WaitForFixedUpdate();
        if (!held) OnRelease();
    }

    protected virtual void OnGrab() { }
    protected virtual void OnRelease() { Drop(); }

    protected virtual void Update()
    {
        if (grab != null && !held)
        {
            bool on = HazardBridge.Active;
            if (grab.enabled != on) grab.enabled = on;
        }
        if (transform.position.y < -2f) ReturnHome();
    }

    protected void Park(Vector3 pos, Quaternion rot)
    {
        if (!rb.isKinematic)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        rb.isKinematic = true;
        transform.SetPositionAndRotation(pos, rot);
        rb.position = pos;
        rb.rotation = rot;
    }

    protected void Drop()
    {
        rb.isKinematic = false;
        rb.useGravity = true;
        rb.WakeUp();
    }

    protected void ReturnHome() => Park(homePos, homeRot);

    protected void ForceRelease()
    {
        if (grab != null && grab.isSelected && grab.interactionManager != null)
            grab.interactionManager.CancelInteractableSelection((IXRSelectInteractable)grab);
    }
}
