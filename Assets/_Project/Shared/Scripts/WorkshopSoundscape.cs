using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

// Event sounds placed where they happen (all 3D):
// ventilation, briefing button clicks, glove snap, padlock + tag rustle, pass/fail at the results,
// light-panel hum, isolator lever clunks, padlock on/off, PPE put on, unsafe buzz,
// last-10-second ticks, training start / session end chimes.
public class WorkshopSoundscape : MonoBehaviour
{
    public Transform[] levers;
    public Transform lockoutPoint;
    public AudioSource[] panelHums;
    public BlackoutController blackout;
    public float humVolume = 0.05f;
    public AudioSource vent;
    public float ventVolume = 0.06f;
    [Tooltip("PPE items whose name contains 'glove' (snap when put on)")]
    public GameObject[] gloves;
    bool[] gloveWorn;
    [Tooltip("Isolator panel lamps: a small relay tick when one changes colour")]
    public Light[] panelLamps;
    Color[] lampColor;
    bool[] lampOn;

    Quaternion[] rest;
    bool lastLock;
    int lastPpe;
    int lastTick = -1;
    string lastPhase = "";
    MemberInfo secondsLeft;

    void OnEnable() { ElectricalSafetyManager.MessageRaised += OnMessage; }
    void OnDisable() { ElectricalSafetyManager.MessageRaised -= OnMessage; }

    void Start()
    {
        if (vent != null) { vent.clip = Sfx.Vent; vent.loop = true; vent.volume = ventVolume; vent.Play(); }
        if (gloves != null) { gloveWorn = new bool[gloves.Length]; }
        foreach (var b in FindObjectsByType<UnityEngine.UI.Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var bt = b.transform;
            b.onClick.AddListener(() => Sfx.PlayAt(Sfx.Click, bt.position, 0.6f, 1.3f));
        }
        rest = new Quaternion[levers != null ? levers.Length : 0];
        for (int i = 0; i < rest.Length; i++) if (levers[i] != null) rest[i] = levers[i].localRotation;
        if (panelLamps != null)
        {
            lampColor = new Color[panelLamps.Length]; lampOn = new bool[panelLamps.Length];
            for (int i = 0; i < panelLamps.Length; i++)
                if (panelLamps[i] != null) { lampColor[i] = panelLamps[i].color; lampOn[i] = panelLamps[i].enabled; }
        }
        foreach (var h in panelHums) if (h != null) { h.clip = Sfx.Hum; h.loop = true; h.volume = humVolume; h.Play(); }
        const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        secondsLeft = (MemberInfo)typeof(SessionManager).GetProperty("PreCheckSecondsLeft", F)
                      ?? typeof(SessionManager).GetField("preCheckSecondsLeft", F);
    }

    void OnMessage(string text, bool unsafeMsg)
    {
        if (!unsafeMsg) return;
        var cam = Camera.main;
        if (cam != null) Sfx.PlayAt(Sfx.Buzz, cam.transform.position + cam.transform.forward * 0.5f, 0.6f);
    }

    void Update()
    {
        // lever clunks
        for (int i = 0; i < rest.Length; i++)
        {
            var l = levers[i];
            if (l == null) continue;
            if (Quaternion.Angle(l.localRotation, rest[i]) > 20f)
            {
                rest[i] = l.localRotation;
                Sfx.PlayAt(Sfx.Clunk, l.position, 0.9f);
            }
        }

        // panel lamp relay ticks (ignored during the blackout, when the lamp blinks)
        if (panelLamps != null && lampColor != null && !(blackout != null && blackout.Blackout))
            for (int i = 0; i < panelLamps.Length; i++)
            {
                var l = panelLamps[i];
                if (l == null) continue;
                Color c = l.color; bool on = l.enabled;
                float dc = Mathf.Abs(c.r - lampColor[i].r) + Mathf.Abs(c.g - lampColor[i].g) + Mathf.Abs(c.b - lampColor[i].b);
                if (dc > 0.3f || on != lampOn[i]) Sfx.PlayAt(Sfx.Relay, l.transform.position, 0.5f);
                lampColor[i] = c; lampOn[i] = on;
            }

        var esm = ElectricalSafetyManager.Instance;
        if (esm != null)
        {
            if (esm.LockoutApplied != lastLock)
            {
                lastLock = esm.LockoutApplied;
                if (lockoutPoint != null)
                {
                    Sfx.PlayAt(Sfx.Padlock, lockoutPoint.position, 0.8f);
                    Sfx.PlayAt(Sfx.Rustle, lockoutPoint.position + Vector3.down * 0.08f, 0.45f, 1.4f); // the tag
                }
            }
            if (esm.PpeWorn > lastPpe)
            {
                var cam = Camera.main;
                if (cam != null) Sfx.PlayAt(Sfx.Rustle, cam.transform.position - Vector3.up * 0.4f, 0.7f);
            }
            lastPpe = esm.PpeWorn;
        }

        // countdown ticks in the last 10 s of the pre-work check
        string phase = HazardBridge.Phase;
        if (phase == "PreCheck" && secondsLeft != null && SessionManager.Instance != null)
        {
            object v = secondsLeft is PropertyInfo pi ? pi.GetValue(SessionManager.Instance)
                     : ((FieldInfo)secondsLeft).GetValue(SessionManager.Instance);
            float s = v is float f ? f : v is int n ? n : 999f;
            int whole = Mathf.CeilToInt(s);
            if (whole <= 10 && whole > 0 && whole != lastTick)
            {
                lastTick = whole;
                var cam = Camera.main;
                if (cam != null) Sfx.PlayAt(Sfx.Tick, cam.transform.position + cam.transform.forward * 0.4f, whole <= 3 ? 0.9f : 0.6f, whole <= 3 ? 1.3f : 1f);
            }
        }

        // phase changes
        if (phase != lastPhase)
        {
            var cam = Camera.main;
            if (cam != null)
            {
                if (phase == "PreCheck" || phase == "Training") Sfx.PlayAt(Sfx.Ding, cam.transform.position + cam.transform.forward, 0.6f);
                else if (phase == "Result")
                {
                    bool? passed = Passed();
                    var clip = passed == null ? Sfx.Chime : passed.Value ? Sfx.PassJingle : Sfx.FailTone;
                    Sfx.PlayAt(clip, cam.transform.position + cam.transform.forward, 0.75f);
                }
            }
            lastPhase = phase;
            lastTick = -1;
        }

        // glove snap when gloves are put on (the item disappears when worn)
        if (gloves != null && gloveWorn != null)
            for (int i = 0; i < gloves.Length; i++)
            {
                if (gloves[i] == null) continue;
                bool worn = !gloves[i].activeInHierarchy;
                if (worn && !gloveWorn[i])
                {
                    var cam = Camera.main;
                    if (cam != null) Sfx.PlayAt(Sfx.GloveSnap, cam.transform.position + cam.transform.forward * 0.3f - Vector3.up * 0.3f, 0.7f);
                }
                gloveWorn[i] = worn;
            }

        // panel hum and ventilation stop in a blackout (same circuit)
        bool dark = blackout != null && blackout.Blackout;
        float target = dark ? 0f : humVolume;
        foreach (var h in panelHums) if (h != null) h.volume = Mathf.MoveTowards(h.volume, target, Time.deltaTime * 0.2f);
        if (vent != null) vent.volume = Mathf.MoveTowards(vent.volume, dark ? 0f : ventVolume, Time.deltaTime * 0.1f);
    }

    // Did the trainee pass? Reads SessionManager by name; falls back to "no unsafe notes".
    static bool? Passed()
    {
        var sm = SessionManager.Instance;
        if (sm == null) return null;
        const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var t = sm.GetType();
        foreach (var n in new[] { "Passed", "IsPassed", "Pass", "passed", "HasPassed" })
        {
            var p = t.GetProperty(n, F);
            if (p != null && p.PropertyType == typeof(bool)) return (bool)p.GetValue(sm);
            var f = t.GetField(n, F);
            if (f != null && f.FieldType == typeof(bool)) return (bool)f.GetValue(sm);
        }
        object score = null, mark = null;
        foreach (var n in new[] { "Score", "score", "CorrectCount", "SafeDecisions" })
        {
            var p = t.GetProperty(n, F); if (p != null) { score = p.GetValue(sm); break; }
            var f = t.GetField(n, F); if (f != null) { score = f.GetValue(sm); break; }
        }
        var pm = t.GetField("passMark", F);
        if (pm != null) mark = pm.GetValue(sm);
        if (score is int si && mark is int mi) return si >= mi;
        var un = t.GetProperty("UnsafeNotes", F);
        if (un != null && un.GetValue(sm) is System.Collections.ICollection c) return c.Count == 0;
        var uf = t.GetField("UnsafeNotes", F);
        if (uf != null && uf.GetValue(sm) is System.Collections.ICollection c2) return c2.Count == 0;
        return null;
    }
}
