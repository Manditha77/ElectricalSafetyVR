using UnityEngine;

// Stops the player from walking through walls and furniture.
public class PlayerCollisionBlocker : MonoBehaviour
{
    public Transform rig;    // XR Origin (XR Rig)
    public Transform head;   // Main Camera
    public float bodyRadius = 0.2f;
    public float lowestCheckHeight = 0.5f;   // ignore the floor and low objects
    public LayerMask blockingLayers = Physics.DefaultRaycastLayers;

    Vector3 lastSafeHead;
    readonly Collider[] hits = new Collider[16];

    void Start() { lastSafeHead = head.position; }

    void LateUpdate()
    {
        if (IsBlocked())
        {
            // Move the whole rig back so the head returns to the last free spot.
            Vector3 back = lastSafeHead - head.position;
            back.y = 0f;
            rig.position += back;
        }
        else
        {
            lastSafeHead = head.position;
        }
    }

    bool IsBlocked()
    {
        Vector3 top = head.position;
        Vector3 bottom = new Vector3(top.x, rig.position.y + lowestCheckHeight, top.z);
        if (bottom.y > top.y) bottom.y = top.y;

        int count = Physics.OverlapCapsuleNonAlloc(
            bottom, top, bodyRadius, hits, blockingLayers, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < count; i++)
        {
            if (hits[i].attachedRigidbody != null) continue;    // things you can pick up
            if (hits[i].transform.IsChildOf(rig)) continue;     // the player's own body
            return true;
        }
        return false;
    }
}