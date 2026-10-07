using UnityEngine;

public class PpeLocker : MonoBehaviour
{
    void OnTriggerEnter(Collider other)
    {
        PpeItem item = other.GetComponentInParent<PpeItem>();
        if (item == null) return;
        if (SessionManager.Instance == null || !SessionManager.Instance.IsRunning) return;

        ElectricalSafetyManager.Instance.PpeItemWorn(item.isRequired);
        if (item.isRequired) item.gameObject.SetActive(false);
    }
}