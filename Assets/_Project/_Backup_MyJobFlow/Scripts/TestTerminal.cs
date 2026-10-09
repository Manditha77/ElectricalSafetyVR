using UnityEngine;
namespace MyJobFlowBackup {

// Test terminals on Workstation 2: touch the tester here to check for voltage.
public class TestTerminal : MonoBehaviour
{
    float nextTest;

    void OnTriggerEnter(Collider other)
    {
        TesterProbe tester = other.GetComponentInParent<TesterProbe>();
        if (tester == null || Time.time < nextTest) return;
        if (SessionManager.Instance == null || !SessionManager.Instance.IsRunning) return;
        nextTest = Time.time + 1f;

        bool dead = ElectricalSafetyManager.Instance.TryVerify();
        if (dead) tester.Show("0 V", new Color(0.2f, 1f, 0.3f));
        else tester.Show("230 V", new Color(1f, 0.2f, 0.15f));
    }
}
}
