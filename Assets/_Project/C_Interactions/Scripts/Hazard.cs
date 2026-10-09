using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public enum HazardLevel { Caution, Danger }

// A workshop hazard, handled in two real steps:
// 1. SPOT it: point at it and press the trigger/grip (G). A callout explains the risk and how to make it safe.
// 2. MAKE IT SAFE: do the control action (mop, unplug, remove the tool). Only then does it count.
public class Hazard : MonoBehaviour
{
    public string hazardName = "Damaged cable";
    [Tooltip("Shown when the hazard is made safe (wet-floor sign, DANGER tag...)")]
    public GameObject marker;

    [Header("What the trainee learns when they spot it")]
    public HazardLevel level = HazardLevel.Danger;
    public string title = "HAZARD";
    [TextArea(2, 4)] public string risk = "";
    [TextArea(2, 4)] public string control = "";
    [TextArea(2, 4)] public string controlledText = "Made safe.";
    public HazardCallout callout;
    [Header("Hazard instruction voice")]
    public AudioClip instructionClip;
    public AudioSource instructionSource;

    [Header("Options")]
    public bool hideMarkerWhenControlled;
    [Tooltip("Turn off this object's colliders once spotted, so the real item underneath can be grabbed")]
    public bool disableCollidersWhenSpotted;

    public static readonly List<Hazard> All = new List<Hazard>();

    public bool Spotted { get; private set; }
    public bool Controlled { get; private set; }
    public bool ControlledLate { get; private set; }
    public bool Found => Controlled;        // kept for older scripts
    public string Outcome { get; set; }     // what went wrong in training, for the report

    void OnEnable() { if (!All.Contains(this)) All.Add(this); }
    void OnDisable() { All.Remove(this); }

    // name keys: hazard object name part  <->  matching control / callout name part
    static readonly string[][] Keys =
    {
        new[] { "Puddle", "WetFloor", "Spill", "Mop", "Bottle" },
        new[] { "DamagedCable", "DamagedLead" },
        new[] { "OverloadedStrip", "PowerStrip" },
        new[] { "MetalTool", "LooseTool", "Spanner" },
    };

    public bool MatchesKey(string other)
    {
        foreach (var set in Keys)
        {
            if (!name.Contains(set[0])) continue;
            foreach (var k in set) if (other.Contains(k)) return true;
        }
        return false;
    }

    // Finds the hazard that belongs to a control / callout / consequence by its name.
    public static Hazard Find(string otherName)
    {
        if (string.IsNullOrEmpty(otherName)) return null;
        foreach (var h in FindObjectsByType<Hazard>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (h.MatchesKey(otherName)) return h;
        return null;
    }

    void Awake()
    {
        if (callout == null)
        {
            foreach (var c in FindObjectsByType<HazardCallout>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (MatchesKey(c.name)) { callout = c; Debug.LogWarning("[Hazard] " + name + ": callout was missing, re-linked to " + c.name); break; }
        }

        var simple = GetComponent<XRSimpleInteractable>();
        if (simple != null) simple.selectEntered.AddListener(OnSelected);
    }

    void Start()
    {
        if (marker != null) marker.SetActive(false);
    }

    void OnSelected(SelectEnterEventArgs args) => Spot();

    // Step 1: the trainee recognised the hazard.
    public void Spot()
    {
        if (!HazardBridge.Active) return;

        if (Controlled)
        {
            if (callout != null) callout.ShowControlled(controlledText, ControlledLate);
            return;
        }

        if (!Spotted)
        {
            Spotted = true;
            bool danger = level == HazardLevel.Danger;
            Sfx.PlayAt(danger ? Sfx.Alert : Sfx.Ding, transform.position, 0.7f);
            HazardBridge.Say((danger ? "DANGER: " : "CAUTION: ") + Plain(title) + ". " + control);

            if (instructionClip != null && instructionSource != null)
            {
                instructionSource.Stop();
                instructionSource.PlayOneShot(instructionClip);
            }

            if (disableCollidersWhenSpotted)
                foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
        }

        if (callout != null) callout.ShowSpotted(level, title, risk, control);
    }

    // Step 2: the control action is done. (Name kept so old wiring still compiles.)
    public void Identify()
    {
        if (Controlled || !HazardBridge.Active) return;
        if (!Spotted) Spot();

        bool inCheck = HazardBridge.InPreCheck;
        Controlled = true;
        ControlledLate = !inCheck;

        if (marker != null) marker.SetActive(!hideMarkerWhenControlled);
        if (callout != null) callout.ShowControlled(controlledText, ControlledLate);
        Sfx.PlayAt(Sfx.Chime, transform.position, 0.7f);

        if (inCheck)
            ElectricalSafetyManager.Instance.HazardIdentified(hazardName);
        else
            HazardBridge.Say(Plain(title) + " made safe, but late. Find and fix hazards in the pre-work check, before the job starts.");
    }

    public void ReportProgress(string text)
    {
        if (callout != null && !Controlled) callout.ShowProgress(text);
    }

    static string Plain(string s) => s.Replace("\n", " ");
}
