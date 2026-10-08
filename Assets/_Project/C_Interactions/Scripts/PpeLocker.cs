using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class PpeLocker : MonoBehaviour
{
    // Decoy items we have already warned about, so the message is not repeated.
    readonly HashSet<PpeItem> rejected = new HashSet<PpeItem>();

    // Runs every physics step while something is inside the locker zone.
    void OnTriggerStay(Collider other)
    {
        PpeItem item = other.GetComponentInParent<PpeItem>();
        if (item == null) return;
        if (SessionManager.Instance == null || !SessionManager.Instance.IsRunning) return;

        // Still in the user's hand: wait until it is released.
        XRGrabInteractable grab = item.GetComponent<XRGrabInteractable>();
        if (grab != null && grab.isSelected) return;

        if (!item.isRequired)
        {
            if (rejected.Add(item)) ElectricalSafetyManager.Instance.PpeItemWorn(false);
            return;
        }

        ElectricalSafetyManager.Instance.PpeItemWorn(true);
        item.gameObject.SetActive(false); // the user is now wearing it
    }

    void OnTriggerExit(Collider other)
    {
        PpeItem item = other.GetComponentInParent<PpeItem>();
        if (item != null) rejected.Remove(item);
    }
}