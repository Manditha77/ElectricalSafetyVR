using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
namespace MyJobFlowBackup {

// Hinged fuse cover: asks the safety manager before it opens.
public class CoverHinge : MonoBehaviour
{
    public XRSimpleInteractable handle;   // on the cover panel
    public float openAngle = 105f;
    public float speed = 240f;
    bool open;

    void Awake()
    {
        if (handle != null) handle.selectEntered.AddListener(_ => Toggle());
    }

    public void Toggle()
    {
        if (SessionManager.Instance == null || !SessionManager.Instance.IsRunning) return;
        ElectricalSafetyManager m = ElectricalSafetyManager.Instance;
        if (!open)
        {
            if (!m.TryOpenCover()) return;
            open = true;
        }
        else
        {
            open = false;
            m.CoverClosed();
            m.Say("Fuse cover closed.");
        }
    }

    void Update()
    {
        Quaternion target = Quaternion.Euler(0f, open ? openAngle : 0f, 0f);
        transform.localRotation = Quaternion.RotateTowards(transform.localRotation, target, speed * Time.deltaTime);
    }
}
}
