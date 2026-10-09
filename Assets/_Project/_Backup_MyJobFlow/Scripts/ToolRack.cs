using UnityEngine;
namespace MyJobFlowBackup {

// Shadow board on the bench: the fuse puller hangs here. Power can only be restored with it back.
public class ToolRack : MonoBehaviour
{
    public SnapItem tool;
    public Transform slot;
    public float snapDistance = 0.25f;

    void Start()
    {
        if (tool != null && slot != null) tool.SnapTo(slot);
        if (ElectricalSafetyManager.Instance != null) ElectricalSafetyManager.Instance.SetToolsClear(true);
    }

    void Update()
    {
        ElectricalSafetyManager m = ElectricalSafetyManager.Instance;
        if (tool == null || slot == null || m == null || tool.Snapped) return;

        if (tool.IsHeld)
        {
            if (m.ToolsClear) m.SetToolsClear(false);   // taken off the board
            return;
        }

        if (Vector3.Distance(tool.transform.position, slot.position) < snapDistance)
        {
            tool.SnapTo(slot);
            m.SetToolsClear(true);
            FusePullerTool puller = tool.GetComponent<FusePullerTool>();
            if (puller != null) puller.DisposeOldFuse();
            m.Say("Fuse puller back on the tool board.");
        }
    }
}
}
