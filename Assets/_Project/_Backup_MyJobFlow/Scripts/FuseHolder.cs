using UnityEngine;
namespace MyJobFlowBackup {

// Fuse holder in Workstation 2: accepts the new fuse once the blown one is out.
public class FuseHolder : MonoBehaviour
{
    public SnapItem newFuse;
    public Transform slot;
    public float snapDistance = 0.12f;

    bool empty, fitted;
    float nextMessage;

    public void SetEmpty() { empty = true; }

    void Update()
    {
        if (fitted || newFuse == null || slot == null || newFuse.IsHeld || newFuse.Snapped) return;
        if (Vector3.Distance(newFuse.transform.position, slot.position) > snapDistance) return;

        ElectricalSafetyManager m = ElectricalSafetyManager.Instance;
        if (m == null) return;
        if (!empty)
        {
            if (Time.time > nextMessage)
            {
                m.Say("Remove the blown fuse first, with the fuse puller.");
                nextMessage = Time.time + 3f;
            }
            return;
        }
        newFuse.SnapTo(slot);
        newFuse.Lock();
        fitted = true;
        m.FuseInserted();
    }
}
}
