using UnityEngine;

public class Hazard : MonoBehaviour
{
    public string hazardName = "Damaged cable";
    public GameObject marker;

    bool found;

    void Start()
    {
        if (marker != null) marker.SetActive(false);
    }

    public void Identify()
    {
        Debug.Log("Identify called");

        if (found) return;
        if (SessionManager.Instance == null || !SessionManager.Instance.IsRunning) return;

        found = true;
        if (marker != null) marker.SetActive(true);
        ElectricalSafetyManager.Instance.HazardIdentified(hazardName);
    }
}