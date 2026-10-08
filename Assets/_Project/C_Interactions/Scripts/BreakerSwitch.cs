using UnityEngine;

public class BreakerSwitch : MonoBehaviour
{
    public bool isCorrectBreaker = true;

    // The team's existing script that turns the isolator lever smoothly.
    public MainSwitchController leverAnimation;

    // The shared indicator lamp above the two switches.
    public PanelLamp lamp;

    public bool IsOn { get; private set; } = true;

    public void Toggle()
    {
        ElectricalSafetyManager manager = ElectricalSafetyManager.Instance;

        if (!isCorrectBreaker)
        {
            manager.WrongBreaker();
            if (lamp != null) lamp.ShowWrong();
            return;
        }

        // Ask the manager. It decides whether the switch is allowed to move.
        bool accepted = IsOn ? manager.TryIsolate() : manager.TryRestore();
        if (!accepted) return;

        IsOn = !IsOn;
        if (leverAnimation != null) leverAnimation.ToggleSwitch();
        // Green flashes while the supply is switched off, and stops when it is back on.
        if (lamp != null) lamp.SetCorrect(!IsOn);
    }
}