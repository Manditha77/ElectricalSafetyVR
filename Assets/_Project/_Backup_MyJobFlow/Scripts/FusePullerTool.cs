using UnityEngine;
namespace MyJobFlowBackup {

// Marks the fuse puller and carries the removed fuse in its jaws.
public class FusePullerTool : MonoBehaviour
{
    public Transform jaw;
    GameObject carried;

    public void Hold(GameObject oldFuse)
    {
        carried = oldFuse;
        foreach (Collider c in oldFuse.GetComponentsInChildren<Collider>()) c.enabled = false;
        oldFuse.transform.SetParent(jaw != null ? jaw : transform, false);
        oldFuse.transform.localPosition = Vector3.zero;
        oldFuse.transform.localRotation = Quaternion.identity;
    }

    public void DisposeOldFuse()
    {
        if (carried != null) carried.SetActive(false);
        carried = null;
    }
}
}
