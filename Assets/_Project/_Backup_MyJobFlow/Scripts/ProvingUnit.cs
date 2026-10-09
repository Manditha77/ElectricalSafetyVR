using UnityEngine;
namespace MyJobFlowBackup {

// Proving unit: a known voltage source to check the tester works before trusting it.
public class ProvingUnit : MonoBehaviour
{
    float nextProve;

    void OnTriggerEnter(Collider other)
    {
        TesterProbe tester = other.GetComponentInParent<TesterProbe>();
        if (tester == null || Time.time < nextProve) return;
        if (SessionManager.Instance == null || !SessionManager.Instance.IsRunning) return;
        nextProve = Time.time + 1f;

        tester.Show("230 V OK", new Color(1f, 0.75f, 0.1f));
        ElectricalSafetyManager.Instance.ProveTester();
    }
}
}
