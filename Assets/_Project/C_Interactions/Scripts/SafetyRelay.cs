using UnityEngine;

public class SafetyRelay : MonoBehaviour
{
    public void TagPlaced()    { ElectricalSafetyManager.Instance.SetLockout(true); }
    public void TagRemoved()   { ElectricalSafetyManager.Instance.SetLockout(false); }

    public void ToolReturned() { ElectricalSafetyManager.Instance.SetToolsClear(true); }
    public void ToolTaken()    { ElectricalSafetyManager.Instance.SetToolsClear(false); }

    public void FuseInserted() { ElectricalSafetyManager.Instance.FuseInserted(); }
}