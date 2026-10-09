using UnityEngine;

// Status lamp on Workstation 2: amber "fault" while it needs repair, green once back in service.
public class MachineLamp : MonoBehaviour
{
    public Renderer lamp;
    MaterialPropertyBlock block;

    void Update()
    {
        SessionManager s = SessionManager.Instance;
        ElectricalSafetyManager m = ElectricalSafetyManager.Instance;
        if (lamp == null || s == null || m == null) return;
        if (block == null) block = new MaterialPropertyBlock();

        Color c = new Color(0.15f, 0.15f, 0.15f);
        bool glow = false;
        if (m.State == ElectricalState.Restored)
        {
            c = new Color(0.2f, 1f, 0.3f);
            glow = true;
        }
        else if (s.Phase == SessionPhase.Training && m.State == ElectricalState.Energised)
        {
            c = new Color(1f, 0.6f, 0.1f);
            glow = Mathf.FloorToInt(Time.time * 2f) % 2 == 0;
        }

        lamp.GetPropertyBlock(block);
        block.SetColor("_BaseColor", c);
        block.SetColor("_EmissionColor", glow ? c * 2f : Color.black);
        lamp.SetPropertyBlock(block);
    }
}