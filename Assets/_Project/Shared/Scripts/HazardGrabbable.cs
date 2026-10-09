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

    // Name used to find the hazard if the reference was lost (e.g. after a scene merge).
    protected virtual string HazardKey => name;

    protected virtual void Awake()
    {
        if (hazard == null && !string.IsNullOrEmpty(HazardKey))
        {
            hazard = Hazard.Find(HazardKey);
            if (hazard != null) Debug.LogWarning("[Hazard] " + name + ": hazard link was missing, re-linked to " + hazard.name);
        }
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

    // Put this object in its "made safe" state without the trainee (missed hazard / restart training).
    public virtual void ForceSafe() { }
    protected virtual void OnRelease() { Drop(); }

    protected virtual void Update()
    {
        if (grab != null && !held)
        {
            // same rule as TrainingFlow: in the pre-work check, PPE first, then hazards
            bool on = HazardBridge.Active && (this is TorchControl ||
                      !HazardBridge.InPreCheck || (HazardBridge.PpeComplete && !HazardBridge.Reviewing));
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
