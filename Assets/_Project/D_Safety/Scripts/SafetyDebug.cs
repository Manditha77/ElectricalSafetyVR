
using UnityEngine;

public class SafetyDebug : MonoBehaviour
{
    ElectricalSafetyManager M =>
        ElectricalSafetyManager.Instance;

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

    // ---------- PPE TESTS ----------

    [ContextMenu("PPE 01 - Wear one item")]
    void WearOnePpe()
    {
        M.PpeItemWorn("Insulated Gloves", true);
    }

    [ContextMenu("PPE 02 - Wear remaining five")]
    void WearRemainingPpe()
    {
        M.PpeItemWorn("Safety Glasses", true);
        M.PpeItemWorn("Required PPE 3", true);
        M.PpeItemWorn("Required PPE 4", true);
        M.PpeItemWorn("Required PPE 5", true);
        M.PpeItemWorn("Required PPE 6", true);
    }

    [ContextMenu("PPE 03 - Wear incorrect PPE")]
    void WearIncorrectPpe()
    {
        M.PpeItemWorn("Incorrect PPE", false);
    }

    [ContextMenu("PPE 04 - Remove gloves")]
    void RemovePpe()
    {
        M.PpeItemRemoved("Insulated Gloves");
    }

    [ContextMenu("PPE 05 - Start work without PPE")]
    void StartWithoutPpe()
    {
        M.HazardIdentified("Test Hazard 1");
    }

    // ---------- HAZARD TESTS ----------

    [ContextMenu("Hazard 01 - Report water on floor")]
    void ReportHazard1()
    {
        M.HazardIdentified("Water on Floor");
    }

    [ContextMenu("Hazard 02 - Report damaged cable")]
    void ReportHazard2()
    {
        M.HazardIdentified("Damaged Cable");
    }

    [ContextMenu("Hazard 03 - Report overloaded socket")]
    void ReportHazard3()
    {
        M.HazardIdentified("Overloaded Socket");
    }

    [ContextMenu("Hazard 04 - Report metal tool hazard")]
    void ReportHazard4()
    {
        M.HazardIdentified("Metal Tool Hazard");
    }

    [ContextMenu("Hazard 05 - Report duplicate hazard")]
    void ReportDuplicateHazard()
    {
        M.HazardIdentified("Water on Floor");
    }

    // ---------- BREAKER ----------

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

    // ---------- COVER ----------

    [ContextMenu("06 Open cover")]
    void OpenCover()
    {
        M.TryOpenCover();
    }

    // ---------- FUSE ----------

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

    // ---------- STAGE 5: INCORRECT FUSE / TOOL ----------

    [ContextMenu("Stage 5 - Wrong fuse selected")]
    void TestWrongFuse()
    {
        M.WrongFuseSelected();
    }

    [ContextMenu("Stage 5 - Wrong tool selected")]
    void TestWrongTool()
    {
        M.WrongToolSelected();
    }

    // ---------- RESTORATION ----------

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
