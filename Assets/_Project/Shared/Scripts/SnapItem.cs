using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// A grabbable item that can rest in a holder (frozen) and falls normally once taken out.
[RequireComponent(typeof(Rigidbody))]
public class SnapItem : MonoBehaviour
{
    public bool Snapped { get; private set; }
    public bool IsHeld => grab != null && grab.isSelected;

    Rigidbody body;
    XRGrabInteractable grab;

    void Awake()
    {
        body = GetComponent<Rigidbody>();
        grab = GetComponent<XRGrabInteractable>();
        if (grab != null)
        {
            grab.selectEntered.AddListener(OnGrabbed);
            grab.selectExited.AddListener(OnReleased);
        }
    }

    public void SnapTo(Transform slot)
    {
        Snapped = true;
        if (!body.isKinematic)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }
        body.isKinematic = true;
        transform.SetPositionAndRotation(slot.position, slot.rotation);
    }

    // Permanently fitted: can no longer be picked up.
    public void Lock()
    {
        if (grab != null) grab.enabled = false;
        Destroy(GetComponent<TrainingTool>());
    }

    void OnGrabbed(SelectEnterEventArgs args) { Snapped = false; }

    void OnReleased(SelectExitEventArgs args)
    {
        // The grab restores the body to how it was when picked up (frozen if it was in a holder).
        // A released item should fall, unless a holder catches it again.
        body.isKinematic = false;
        body.useGravity = true;
    }
}