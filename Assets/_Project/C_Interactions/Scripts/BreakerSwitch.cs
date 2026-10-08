using UnityEngine;

public class BreakerSwitch : MonoBehaviour
{
    public bool isCorrectBreaker = true;

    // The team's existing script that turns the isolator lever smoothly.
    public MainSwitchController leverAnimation;

    // The shared indicator lamp above the switches.
    public PanelLamp lamp;

    public bool IsOn { get; private set; } = true;

    public void Toggle()
    {
        // Switches only work once training has started.
        if (SessionManager.Instance == null || !SessionManager.Instance.IsRunning) return;
        ElectricalSafetyManager manager = ElectricalSafetyManager.Instance;

        if (!isCorrectBreaker)
        {
            // A wrong isolator still moves, like a real one, but switching it OFF is flagged.
            if (IsOn)
            {
                manager.WrongBreaker();
                if (lamp != null) lamp.ShowWrong();
            }
            Flip();
            return;
        }

        // Ask the manager. It decides whether the correct switch is allowed to move.
        bool accepted = IsOn ? manager.TryIsolate() : manager.TryRestore();
        if (!accepted) return;

        Flip();
        // Green flashes while the supply is switched off, and stops when it is back on.
        if (lamp != null) lamp.SetCorrect(!IsOn);
    }

    void Flip()
    {
        IsOn = !IsOn;
        if (leverAnimation != null) leverAnimation.ToggleSwitch();
    }
}