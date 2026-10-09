using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

// Small bridge between the hazard scripts and the existing SessionManager /
// ElectricalSafetyManager. It reads the phase/state and calls Say / unsafe notes
// by name, so it keeps working even if those classes change a little.
public static class HazardBridge
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    static MemberInfo phaseMember, stateMember;
    static bool phaseSearched, stateSearched;

    static MemberInfo Find(Type t, params string[] names)
    {
        foreach (var n in names)
        {
            var p = t.GetProperty(n, F);
            if (p != null) return p;
            var f = t.GetField(n, F);
            if (f != null) return f;
        }
        return null;
    }

    static object Get(MemberInfo m, object o)
    {
        if (m is PropertyInfo p) return p.GetValue(o);
        if (m is FieldInfo f) return f.GetValue(o);
        return null;
    }

    static bool GetBool(object o, params string[] names)
    {
        if (o == null) return false;
        var m = Find(o.GetType(), names);
        return m != null && Get(m, o) is bool b && b;
    }

    // "Briefing", "PreCheck", "Training" or "Result"
    public static string Phase
    {
        get
        {
            var s = SessionManager.Instance;
            if (s == null) return "";
            if (!phaseSearched)
            {
                phaseMember = Find(typeof(SessionManager), "Phase", "CurrentPhase", "phase", "currentPhase");
                phaseSearched = true;
            }
            if (phaseMember != null)
            {
                var v = Get(phaseMember, s);
                return v != null ? v.ToString() : "";
            }
            var esm = ElectricalSafetyManager.Instance;
            if (GetBool(s, "IsPreCheck") || GetBool(esm, "PreCheck", "IsPreCheck")) return "PreCheck";
            if (GetBool(s, "IsRunning") || GetBool(esm, "Running", "IsRunning")) return "Training";
            return "Briefing";
        }
    }

    public static bool InPreCheck => Phase == "PreCheck";
    public static bool InTraining => Phase == "Training";
    //public static bool Active { get { var p = Phase; return p == "PreCheck" || p == "Training"; } }

    public static bool PpeComplete
    {
        get
        {
            var manager = ElectricalSafetyManager.Instance;
            return manager != null &&
                   manager.PpeWorn >= manager.requiredPpeItems;
        }
    }

    public static bool Active
    {
        get
        {
            var phase = Phase;

            return phase == "Training" ||
                   (phase == "PreCheck" && PpeComplete);
        }
    }

    // "Energised", "Isolated", "VerifiedSafe", "Repaired", "Restored"
    public static string State
    {
        get
        {
            var e = ElectricalSafetyManager.Instance;
            if (e == null) return "";
            if (!stateSearched)
            {
                stateMember = Find(typeof(ElectricalSafetyManager), "State", "CurrentState", "state", "currentState");
                stateSearched = true;
            }
            var v = stateMember != null ? Get(stateMember, e) : null;
            return v != null ? v.ToString() : "";
        }
    }

    public static void Say(string msg)
    {
        var e = ElectricalSafetyManager.Instance;
        if (e != null)
        {
            var m = typeof(ElectricalSafetyManager).GetMethod("Say", F, null, new[] { typeof(string) }, null);
            if (m != null) { m.Invoke(e, new object[] { msg }); return; }
        }
        Debug.Log("[Hazard] " + msg);
    }

    // Adds a line to the unsafe notes shown on the results board.
    public static void AddUnsafe(string note)
    {
        var s = SessionManager.Instance;
        object[] owners = { s, ElectricalSafetyManager.Instance };
        foreach (var o in owners)
        {
            if (o == null) continue;
            foreach (var name in new[] { "AddUnsafeNote", "AddUnsafe", "FlagUnsafe", "Unsafe" })
            {
                var m = o.GetType().GetMethod(name, F, null, new[] { typeof(string) }, null);
                if (m != null) { m.Invoke(o, new object[] { note }); return; }
            }
        }
        foreach (var o in owners)
        {
            if (o == null) continue;
            var mem = Find(o.GetType(), "UnsafeNotes", "unsafeNotes");
            if (mem != null && Get(mem, o) is IList list)
            {
                if (!list.Contains(note)) list.Add(note);
                return;
            }
        }
        Debug.LogWarning("[Hazard] Unsafe: " + note);
    }

    // Marks one of the six decisions (e.g. "Hazards") as unsafe, if SessionManager.RegisterAction(enum, bool) exists.
    public static void FailDecision(string decision)
    {
        var s = SessionManager.Instance;
        if (s == null) return;
        foreach (var m in typeof(SessionManager).GetMethods(F))
        {
            if (m.Name != "RegisterAction") continue;
            var ps = m.GetParameters();
            if (ps.Length == 2 && ps[0].ParameterType.IsEnum && ps[1].ParameterType == typeof(bool)
                && Enum.IsDefined(ps[0].ParameterType, decision))
            {
                m.Invoke(s, new object[] { Enum.Parse(ps[0].ParameterType, decision), false });
                return;
            }
        }
    }

    public static float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = 0; b.y = 0;
        return Vector3.Distance(a, b);
    }
}
