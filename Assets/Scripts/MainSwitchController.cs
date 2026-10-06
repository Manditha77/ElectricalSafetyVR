using UnityEngine;

public class MainSwitchController : MonoBehaviour
{
    [SerializeField] private Transform leverPivot;
    [SerializeField] private bool startsOn = true;
    [SerializeField] private float rotationSpeed = 360f;

    public bool IsOn { get; private set; }

    private void Awake()
    {
        IsOn = startsOn;

        if (leverPivot != null)
            leverPivot.localRotation = TargetRotation();
    }

    private void Update()
    {
        if (leverPivot == null)
            return;

        leverPivot.localRotation = Quaternion.RotateTowards(
            leverPivot.localRotation,
            TargetRotation(),
            rotationSpeed * Time.deltaTime);
    }

    [ContextMenu("Toggle Switch")]
    public void ToggleSwitch()
    {
        if (!Application.isPlaying)
            return;

        IsOn = !IsOn;
        Debug.Log(IsOn ? "Main switch ON" : "Main switch OFF");
    }

    private Quaternion TargetRotation()
    {
        return Quaternion.Euler(IsOn ? -30f : -150f, 0f, 0f);
    }
}