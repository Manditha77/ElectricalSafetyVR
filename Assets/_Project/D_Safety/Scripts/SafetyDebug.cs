using UnityEngine;

public class SafetyDebug : MonoBehaviour
{
    ElectricalSafetyManager M => ElectricalSafetyManager.Instance;

    void OnEnable()
    {
        ElectricalSafetyManager.StateChanged += OnState;
        ElectricalSafetyManager.MessageRaised += OnMessage;
    }

    void OnDisable()
    {
        ElectricalSafetyManager.StateChanged -= OnState;
        ElectricalSafetyManager.MessageRaised -= OnMessage;
    }

    void OnState(ElectricalState state)
    {
        Debug.Log("STATE: " + state);
    }

    void OnMessage(string text, bool isUnsafe)
    {
        if (isUnsafe)
            Debug.LogWarning("UNSAFE: " + text);
        else
            Debug.Log(text);
    }

    [ContextMenu("01 Wear both PPE items")]
    void WearPpe()
    {
        M.PpeItemWorn(true);
        M.PpeItemWorn(true);
    }

    [ContextMenu("02 Report a hazard")]
    void ReportHazard()
    {
        M.HazardIdentified("Test hazard");
    }

    [ContextMenu("03 Breaker OFF")]
    void BreakerOff()
    {
        M.TryIsolate();
    }

    [ContextMenu("04 Tag ON")]
    void TagOn()
    {
        M.SetLockout(true);
    }

    [ContextMenu("05 Test for voltage")]
    void TestVoltage()
    {
        M.TryVerify();
    }

    [ContextMenu("06 Open cover")]
    void OpenCover()
    {
        M.TryOpenCover();
    }

    [ContextMenu("07 Pull damaged fuse")]
    void PullFuse()
    {
        M.TryRemoveFuse();
    }

    [ContextMenu("08 Fit new fuse")]
    void FitFuse()
    {
        M.FuseInserted();
    }

    [ContextMenu("09 Close cover")]
    void CloseCover()
    {
        M.CoverClosed();
    }

    [ContextMenu("10 Return tool")]
    void ReturnTool()
    {
        M.SetToolsClear(true);
    }

    [ContextMenu("11 Tag OFF")]
    void TagOff()
    {
        M.SetLockout(false);
    }

    [ContextMenu("12 Breaker ON")]
    void BreakerOn()
    {
        M.TryRestore();
    }

    [ContextMenu("13 Wrong breaker")]
    void WrongBreaker()
    {
        M.WrongBreaker();
    }
}