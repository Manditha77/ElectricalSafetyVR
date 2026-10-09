using UnityEngine;
namespace MyJobFlowBackup {

// The blown fuse in Workstation 2. Only the fuse puller removes it, and only when it is safe.
public class BlownFuse : MonoBehaviour
{
    public FuseHolder holder;
    float nextMessage;

    void OnTriggerEnter(Collider other)
    {
        FusePullerTool puller = other.GetComponentInParent<FusePullerTool>();
        if (puller == null) return;
        if (SessionManager.Instance == null || !SessionManager.Instance.IsRunning) return;

        ElectricalSafetyManager m = ElectricalSafetyManager.Instance;
        if (!m.TryRemoveFuse())
        {
            if (Time.time > nextMessage)
            {
                m.Say("You can't reach the fuse: open the cover first.");
                nextMessage = Time.time + 2f;
            }
            return;
        }
        puller.Hold(gameObject);
        if (holder != null) holder.SetEmpty();
    }
}
}
