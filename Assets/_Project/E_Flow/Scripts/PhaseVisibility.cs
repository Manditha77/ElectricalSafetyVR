using UnityEngine;

public class PhaseVisibility : MonoBehaviour
{
    public GameObject content;
    public SessionPhase showDuring;

    void OnEnable()
    {
        SessionManager.PhaseChanged += Apply;
    }

    void OnDisable()
    {
        SessionManager.PhaseChanged -= Apply;
    }

    void Start()
    {
        if (SessionManager.Instance != null)
            Apply(SessionManager.Instance.Phase);
    }

    void Apply(SessionPhase phase)
    {
        content.SetActive(phase == showDuring);
    }
}