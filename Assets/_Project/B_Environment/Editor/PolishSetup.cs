using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// Tools > Workshop > Polish > ...
// Run AFTER "Build Realistic Hazards". Each item can be run again safely.
public static class PolishSetup
{
    const string MatFolder = "Assets/_Project/B_Environment/Materials";
    static readonly Vector3 BinPos = new Vector3(-1.12f, 0f, -2.72f);       // next to the spill kit
    static readonly Vector3 TorchSeatPos = new Vector3(3.84f, 1.32f, 1.85f); // east wall, under the beacon

    [MenuItem("Tools/Workshop/Polish/Build ALL (spill, plugs, sounds, blackout + torch, door)")]
    static void All()
    {
        UpgradeSpill();
        FixPlugs();
        AutoFixDamagedLead();
        MoveStripSocket();
        BuildSounds();
        BuildBlackout();
        BuildDoor();
        DiagnoseRepair();
        Debug.Log("[Polish] All done.");
    }

    // ---------------------------------------------------------------- 0. diagnose + repair
    [MenuItem("Tools/Workshop/Polish/0. Diagnose && Repair Hazards")]
    static void DiagnoseRepair()
    {
        var log = new List<string>();
        int fixes = 0, problems = 0;

        // duplicate script files (a copied folder in the wrong place breaks everything)
        foreach (var n in new[] { "Hazard", "HazardCallout", "HazardBridge", "Sfx", "MopControl", "PlugControl", "HazardGrabbable", "ElectricalSafetyManager", "SessionManager" })
        {
            int count = 0; var paths = new List<string>();
            foreach (var g in AssetDatabase.FindAssets(n + " t:MonoScript"))
            {
                var p = AssetDatabase.GUIDToAssetPath(g);
                if (Path.GetFileNameWithoutExtension(p) == n) { count++; paths.Add(p); }
            }
            if (count > 1) { problems++; log.Add("PROBLEM: " + count + " copies of " + n + ".cs -> delete the extra: " + string.Join(" | ", paths)); }
        }

        // runtime links to the managers
        var esmSay = typeof(ElectricalSafetyManager).GetMethod("Say", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic, null, new[] { typeof(string) }, null);
        log.Add(esmSay != null ? "OK: ElectricalSafetyManager has Say()" : "WARN: ElectricalSafetyManager has no Say() (hazard messages go to the Console only)");
        var phase = typeof(SessionManager).GetProperty("Phase") ?? typeof(SessionManager).GetProperty("CurrentPhase");
        log.Add(phase != null ? "OK: SessionManager phase = " + phase.Name : "WARN: SessionManager has no Phase property (using IsPreCheck/IsRunning)");

        // hazards
        var hazards = Object.FindObjectsByType<Hazard>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        log.Add("Hazards in scene: " + hazards.Length);
        var callouts = Object.FindObjectsByType<HazardCallout>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var h in hazards)
        {
            if (string.IsNullOrEmpty(h.title) || h.title == "HAZARD") { problems++; log.Add("PROBLEM: " + h.name + " has no texts -> run 'Build Realistic Hazards' again"); }
            if (h.callout == null)
            {
                foreach (var c in callouts) if (h.MatchesKey(c.name)) { Undo.RecordObject(h, "link"); h.callout = c; Record(h); fixes++; log.Add("FIXED: " + h.name + " callout -> " + c.name); break; }
                if (h.callout == null)
                {
                    var c = BuildCallout(h);
                    if (c != null) { Undo.RecordObject(h, "link"); h.callout = c; Record(h); fixes++; log.Add("FIXED: built a new callout card for " + h.name + " (" + c.name + ")"); }
                    else { problems++; log.Add("PROBLEM: could not build a callout for " + h.name); }
                }
            }
            if (h.GetComponent<XRSimpleInteractable>() == null) { problems++; log.Add("PROBLEM: " + h.name + " has no XR Simple Interactable (cannot be spotted with G)"); }
            foreach (var it in h.GetComponentsInChildren<XRBaseInteractable>(true))
                for (int i = it.selectEntered.GetPersistentEventCount() - 1; i >= 0; i--)
                    if (it.selectEntered.GetPersistentMethodName(i) == "Identify")
                    {
                        UnityEditor.Events.UnityEventTools.RemovePersistentListener(it.selectEntered, i);
                        Record(it); fixes++; log.Add("FIXED: removed old 'G = Identify' wiring on " + it.name);
                    }
        }

        // controls -> hazard links
        foreach (var g in Object.FindObjectsByType<HazardGrabbable>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (g is TorchControl) continue;
            if (g.hazard == null)
            {
                string key = g is MopControl || g is SpillBottle ? "Puddle" : g is LooseToolControl ? "MetalTool" : g.name;
                var h = Hazard.Find(key);
                if (h != null) { Undo.RecordObject(g, "link"); g.hazard = h; Record(g); fixes++; log.Add("FIXED: " + g.name + " -> " + h.name); }
                else { problems++; log.Add("PROBLEM: " + g.name + " has no hazard"); }
            }
            if (g.GetComponent<XRGrabInteractable>() == null) { problems++; log.Add("PROBLEM: " + g.name + " has no XR Grab Interactable"); }
        }
        foreach (var c in Object.FindObjectsByType<HazardConsequence>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (c.hazard == null)
            {
                var h = Hazard.Find(c.name);
                if (h != null) { Undo.RecordObject(c, "link"); c.hazard = h; Record(c); fixes++; log.Add("FIXED: " + c.name + " -> " + h.name); }
            }

        // mop <-> bottle, drop zones, radii
        var mop = Object.FindFirstObjectByType<MopControl>();
        var bottle = Object.FindFirstObjectByType<SpillBottle>();
        if (mop != null && bottle != null)
        {
            Undo.RecordObject(mop, "mop"); Undo.RecordObject(bottle, "bottle");
            mop.bottle = bottle; bottle.mop = mop; bottle.binRadius = 0.45f;
            if (mop.puddle == null && mop.hazard != null) mop.puddle = mop.hazard.transform;
            var rim = GameObject.Find("HazardControls/WasteBin/Rim");
            if (rim != null) bottle.zoneHighlight = rim.GetComponent<Renderer>();
            Record(mop); Record(bottle);
        }
        var tool = Object.FindFirstObjectByType<LooseToolControl>();
        if (tool != null)
        {
            Undo.RecordObject(tool, "tool");
            tool.returnRadius = 0.55f;
            var tray = GameObject.Find("HazardControls/ToolTrolley/TopTray");
            if (tray != null) tool.zoneHighlight = tray.GetComponent<Renderer>();
            Record(tool);
        }
        foreach (var p in Object.FindObjectsByType<PlugControl>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var col = p.GetComponent<BoxCollider>();
            if (col != null && col.size.x < 0.11f) { Undo.RecordObject(col, "col"); col.center = V(0, -0.01f, 0.012f); col.size = V(0.12f, 0.13f, 0.09f); fixes++; }
        }

        Dirty();
        string msg = "Fixed: " + fixes + "   Problems left: " + problems + "\n\n" + string.Join("\n", log);
        Debug.Log("[Polish] Diagnose & Repair\n" + msg);
        EditorUtility.DisplayDialog("Diagnose & Repair Hazards", msg.Length > 1800 ? msg.Substring(0, 1800) + "\n... (see Console)" : msg, "OK");
    }

    // Rebuilds one red/amber/green risk card above a hazard (same design as Build Realistic Hazards).
    static HazardCallout BuildCallout(Hazard h)
    {
        var group = GameObject.Find("HazardControls");
        if (group == null) { group = new GameObject("HazardControls"); Undo.RegisterCreatedObjectUndo(group, "group"); }

        string n = h.name.Contains("Puddle") ? "Callout_WetFloor" : h.name.Contains("DamagedCable") ? "Callout_DamagedLead"
                 : h.name.Contains("OverloadedStrip") ? "Callout_PowerStrip" : h.name.Contains("MetalTool") ? "Callout_LooseTool" : "Callout_" + h.name;
        var old = group.transform.Find(n);
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

        Vector3 p = h.transform.position;
        float y = h.name.Contains("Puddle") ? 1.45f : h.name.Contains("DamagedCable") ? 1.4f : Mathf.Max(1.5f, p.y + 0.55f);
        float push = h.name.Contains("MetalTool") ? 0.45f : 0.3f;
        var flat = new Vector3(-p.x, 0, -p.z);
        if (flat.sqrMagnitude > 0.001f) flat.Normalize();
        Vector3 pos = new Vector3(p.x, y, p.z) + flat * push;

        var bg = Mat("Mat_CalloutBg", new Color(0.06f, 0.07f, 0.09f), unlit: true);
        var barMat = Mat("Mat_CalloutBar", Color.white, unlit: true);

        var root = Node(n, group.transform, pos, Vector3.zero);
        var card = Node("Card", root.transform, Vector3.zero, Vector3.zero, local: true);
        Box("Background", card.transform, V(0, 0, 0), V(0.82f, 0.46f, 0.01f), bg);
        var bar = Box("Bar", card.transform, V(0, 0.195f, -0.004f), V(0.82f, 0.07f, 0.006f), barMat);
        var header = Label("Header", card.transform, V(0, 0.195f, -0.012f), Vector3.zero, "HAZARD", 0.42f, V2(0.78f, 0.06f), Color.black);
        header.fontSizeMin = 0.15f; header.fontSizeMax = 0.42f;
        var body = Label("Body", card.transform, V(0, -0.04f, -0.008f), Vector3.zero, "", 0.3f, V2(0.76f, 0.36f), Color.white);
        body.alignment = TextAlignmentOptions.TopLeft;
        body.fontSizeMin = 0.12f; body.fontSizeMax = 0.3f;
        var c = root.AddComponent<HazardCallout>();
        c.card = card.transform; c.bar = bar.GetComponent<Renderer>(); c.header = header; c.body = body;
        card.SetActive(false);
        Undo.RegisterCreatedObjectUndo(root, "callout");
        return c;
    }

    // ---------------------------------------------------------------- 1. spill
    [MenuItem("Tools/Workshop/Polish/1. Upgrade Spill (gradual mop + bottle + waste bin)")]
    static void UpgradeSpill()
    {
        var group = GameObject.Find("HazardControls");
        var mop = Object.FindFirstObjectByType<MopControl>();
        if (group == null || mop == null || mop.hazard == null) { Fail("Run 'Build Realistic Hazards' first."); return; }
        var puddle = mop.hazard;

        Remove(group.transform, "WasteBin");
        Remove(group.transform, "SpillBottle");

        // waste bin
        var mBin = Mat("Mat_BinGrey", new Color(0.22f, 0.24f, 0.26f), smooth: 0.5f);
        var mBlack = Mat("Mat_SocketHole", new Color(0.02f, 0.02f, 0.02f), unlit: true);
        var mWhite = Mat("Mat_PlasticWhite", new Color(0.93f, 0.93f, 0.9f), smooth: 0.55f);
        var bin = Node("WasteBin", group.transform, BinPos, Vector3.zero);
        Cyl("Body", bin.transform, V(0, 0.275f, 0), V(0.34f, 0.275f, 0.34f), mBin);
        Cyl("Rim", bin.transform, V(0, 0.555f, 0), V(0.36f, 0.012f, 0.36f), mBin);
        Cyl("Opening", bin.transform, V(0, 0.568f, 0), V(0.31f, 0.002f, 0.31f), mBlack);
        Box("LabelPlate", bin.transform, V(0, 0.36f, 0.168f), V(0.16f, 0.06f, 0.004f), mWhite);
        Label("Label", bin.transform, V(0, 0.36f, 0.172f), V(0, 180, 0), "<b>WASTE</b>", 0.35f, V2(0.15f, 0.05f), Color.black);
        var drop = Node("Drop", bin.transform, V(0, 0.56f, 0), Vector3.zero, local: true);
        var binCol = bin.AddComponent<CapsuleCollider>();
        binCol.center = V(0, 0.28f, 0); binCol.height = 0.56f; binCol.radius = 0.17f;

        // bottle: use one already in the scene near the spill if there is one, else build one
        GameObject source = null;
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (!t.name.ToLowerInvariant().Contains("bottle") || IsUnder(t, "HazardControls")) continue;
            if (Vector3.Distance(t.position, puddle.transform.position) > 2.5f) continue;
            if (t.GetComponentInChildren<Renderer>() == null) continue;
            source = t.gameObject; break;
        }

        GameObject bottle;
        if (source != null)
        {
            bottle = Object.Instantiate(source, source.transform.position, source.transform.rotation);
            bottle.name = "SpillBottle";
            bottle.transform.SetParent(group.transform, true);
            StripToVisuals(bottle);
            foreach (var r in source.GetComponentsInChildren<Renderer>(true)) { Undo.RecordObject(r, "hide"); r.enabled = false; PrefabUtility.RecordPrefabInstancePropertyModifications(r); }
            foreach (var c in source.GetComponentsInChildren<Collider>(true)) { Undo.RecordObject(c, "off"); c.enabled = false; PrefabUtility.RecordPrefabInstancePropertyModifications(c); }
            var b = WorldBounds(bottle);
            var bc = bottle.AddComponent<BoxCollider>();
            bc.center = bottle.transform.InverseTransformPoint(b.center);
            bc.size = Vector3.Max(bottle.transform.InverseTransformVector(b.size).Abs(), V(0.08f, 0.08f, 0.08f));
            Debug.Log("[Polish] Using your bottle '" + source.name + "' (visual copy; original hidden).");
        }
        else
        {
            var mBottle = Mat("Mat_BottleBlue", new Color(0.12f, 0.42f, 0.72f), smooth: 0.75f);
            var mCap = Mat("Mat_Danger", new Color(0.8f, 0.1f, 0.1f), smooth: 0.4f);
            bottle = Node("SpillBottle", group.transform, puddle.transform.position + V(0.45f, 0.036f, 0.32f), V(0, 30, 90));
            Cyl("Body", bottle.transform, V(0, 0.09f, 0), V(0.07f, 0.09f, 0.07f), mBottle);
            Cyl("LabelBand", bottle.transform, V(0, 0.09f, 0), V(0.072f, 0.04f, 0.072f), mWhite);
            Cyl("Shoulder", bottle.transform, V(0, 0.19f, 0), V(0.05f, 0.02f, 0.05f), mBottle);
            Cyl("Neck", bottle.transform, V(0, 0.22f, 0), V(0.03f, 0.025f, 0.03f), mBottle);
            Cyl("Cap", bottle.transform, V(0, 0.25f, 0), V(0.034f, 0.012f, 0.034f), mCap);
            var cc = bottle.AddComponent<CapsuleCollider>();
            cc.direction = 1; cc.center = V(0, 0.12f, 0); cc.height = 0.27f; cc.radius = 0.05f;
        }
        Grabbable(bottle, null, 0.3f);
        var sb = bottle.AddComponent<SpillBottle>();
        sb.hazard = puddle; sb.bin = bin.transform; sb.binDrop = drop.transform; sb.mop = mop;
        Undo.RegisterCreatedObjectUndo(bin, "bin"); Undo.RegisterCreatedObjectUndo(bottle, "bottle");

        Undo.RecordObject(mop, "mop");
        mop.bottle = sb;
        Record(mop);

        Undo.RecordObject(puddle, "texts");
        puddle.control = "Bin the spilled bottle, then mop the floor with the SPILL KIT by the door.";
        puddle.controlledText = "Source removed and spill mopped. Wet-floor sign out while the floor dries.";
        puddle.disableCollidersWhenSpotted = true; // the puddle's box no longer hides the bottle after spotting
        Record(puddle);

        Dirty();
        Debug.Log("[Polish] Spill upgraded: gradual mopping with damp remnants, spilled bottle + WASTE bin.");
    }

    // ---------------------------------------------------------------- 2. plugs
    [MenuItem("Tools/Workshop/Polish/2. Fix Plugs (easier to grab)")]
    static void FixPlugs()
    {
        int n = 0;
        foreach (var p in Object.FindObjectsByType<PlugControl>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var col = p.GetComponent<BoxCollider>();
            if (col != null) { Undo.RecordObject(col, "col"); col.center = V(0, -0.01f, 0.012f); col.size = V(0.12f, 0.13f, 0.09f); }
            Undo.RecordObject(p, "plug");
            p.zapWhenLive = p.name.Contains("DamagedLead");
            Record(p);
            if (p.hazard != null)
            {
                Undo.RecordObject(p.hazard, "hz");
                p.hazard.disableCollidersWhenSpotted = true; // the hazard's own box no longer blocks the ray to the plug
                Record(p.hazard);
            }
            n++;
        }
        Dirty();
        Debug.Log("[Polish] " + n + " plugs: bigger grab area, grip = unplug at once, hazard box no longer blocks the plug after spotting.");
    }

    // ---------------------------------------------------------------- 3. align with socket model
    [MenuItem("Tools/Workshop/Polish/3a. Use SELECTED model as the Damaged-Lead wall socket")]
    static void AlignDamaged() => AlignToSelected("DamagedLead");

    [MenuItem("Tools/Workshop/Polish/3b. Use SELECTED model as the Power-Strip wall socket")]
    static void AlignStrip() => AlignToSelected("PowerStrip");

    static void AlignToSelected(string key)
    {
        var sel = Selection.activeGameObject;
        var socket = GameObject.Find("HazardControls/Socket_" + key);
        var plugGo = GameObject.Find("HazardControls/Plug_" + key);
        if (sel == null || socket == null || plugGo == null) { Fail("Select your socket model in the Hierarchy first (and run Build Realistic Hazards)."); return; }
        var plug = plugGo.GetComponent<PlugControl>();
        var seat = socket.transform.Find("PlugSeat");
        var b = WorldBounds(sel);
        if (b.size == Vector3.zero) { Fail("The selected object has no visible mesh."); return; }

        // hide a plug that is part of the model (ours is the one you pull out)
        var hidden = new List<string>();
        foreach (var r in sel.GetComponentsInChildren<Renderer>(true))
            if (r.gameObject != sel && r.name.ToLowerInvariant().Contains("plug"))
            {
                Undo.RecordObject(r, "hide"); r.enabled = false; Record(r); hidden.Add(r.name);
                foreach (var c in r.GetComponents<Collider>()) { Undo.RecordObject(c, "off"); c.enabled = false; }
            }
        if (hidden.Count > 0) b = WorldBounds(sel);

        Vector3 f = socket.transform.forward;              // out of the wall
        float front = float.MinValue;
        for (int i = 0; i < 8; i++)
        {
            var e = b.extents;
            var corner = b.center + new Vector3((i & 1) == 0 ? -e.x : e.x, (i & 2) == 0 ? -e.y : e.y, (i & 4) == 0 ? -e.z : e.z);
            front = Mathf.Max(front, Vector3.Dot(corner, f));
        }
        Vector3 target = b.center + f * (front - Vector3.Dot(b.center, f) + 0.015f);

        Undo.RecordObject(socket.transform, "move socket");
        socket.transform.position += target - seat.position;
        foreach (var r in socket.GetComponentsInChildren<Renderer>(true)) { Undo.RecordObject(r, "hide"); r.enabled = false; }

        Undo.RecordObject(plugGo.transform, "move plug");
        plugGo.transform.SetPositionAndRotation(target, socket.transform.rotation);

        if (plug != null && plug.leadAnchor != null && plug.leadPoint != null && plug.lead != null)
        {
            Undo.RecordObject(plug, "lead");
            plug.leadLength = Vector3.Distance(plug.leadPoint.position, plug.leadAnchor.position) + 0.6f;
            Undo.RecordObject(plug.lead, "lead");
            PlugControl.DrawLead(plug.lead, plug.leadPoint.position, plug.leadAnchor.position, plug.leadLength);
            Record(plug);
        }
        Dirty();
        Debug.Log("[Polish] Plug_" + key + " now sits in '" + sel.name + "'. Our small socket is hidden." +
                  (hidden.Count > 0 ? " Hid the model's own plug part(s): " + string.Join(", ", hidden) : ""));
    }

    // ---------------------------------------------------------------- 4. sounds
    [MenuItem("Tools/Workshop/Polish/4. Build Sounds (hum, vent, levers, padlock, PPE, buttons, ticks, footsteps, pass/fail)")]
    static void BuildSounds()
    {
        var old = GameObject.Find("WorkshopSounds");
        if (old != null) Undo.DestroyObjectImmediate(old);
        var root = new GameObject("WorkshopSounds");
        Undo.RegisterCreatedObjectUndo(root, "sounds");

        var hums = new List<AudioSource>();
        foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
        {
            if (r.sharedMaterial == null || !r.sharedMaterial.name.StartsWith("Mat_LightPanel")) continue;
            var h = Node("Hum_" + r.name, root.transform, r.bounds.center, Vector3.zero);
            var a = h.AddComponent<AudioSource>();
            a.playOnAwake = false; a.loop = true; a.spatialBlend = 1f; a.minDistance = 0.5f; a.maxDistance = 5f;
            a.rolloffMode = AudioRolloffMode.Logarithmic; a.dopplerLevel = 0f;
            hums.Add(a);
            if (hums.Count >= 8) break;
        }

        var levers = new List<Transform>();
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            if (t.name == "Lever") levers.Add(t);

        Transform lockout = null;
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            if (t.name.Contains("Lockout")) { lockout = t; break; }
        if (lockout == null) { var bp = GameObject.Find("BreakerPanel"); if (bp != null) lockout = bp.transform; }

        var lamps = new List<Light>();
        var panel = FindDeep("BreakerPanel");
        if (panel != null)
            foreach (var l in panel.GetComponentsInChildren<Light>(true))
                if (l.name.Contains("Lamp") || l.name.Contains("Glow")) lamps.Add(l);

        var ss = root.AddComponent<WorkshopSoundscape>();
        ss.panelLamps = lamps.ToArray();

        // ventilation: a ceiling vent sound in the middle of the room
        var ventGo = Node("Ventilation", root.transform, V(0f, 2.8f, 0f), Vector3.zero);
        var va = ventGo.AddComponent<AudioSource>();
        va.playOnAwake = false; va.loop = true; va.spatialBlend = 1f; va.minDistance = 1.5f; va.maxDistance = 12f;
        va.rolloffMode = AudioRolloffMode.Linear; va.dopplerLevel = 0f;
        ss.vent = va;

        // gloves (snap when put on)
        var gl = new List<GameObject>();
        var items = GameObject.Find("PPE_Station/PPEItems");
        if (items != null)
            foreach (Transform c in items.transform)
                if (c.name.ToLowerInvariant().Contains("glove")) gl.Add(c.gameObject);
        ss.gloves = gl.ToArray();
        ss.levers = levers.ToArray(); ss.lockoutPoint = lockout; ss.panelHums = hums.ToArray();
        ss.blackout = Object.FindFirstObjectByType<BlackoutController>();

        var fs = root.AddComponent<FootstepAudio>();
        var mop = Object.FindFirstObjectByType<MopControl>();
        if (mop != null) fs.puddle = mop.hazard;
        var exactBoots = GameObject.Find("PPE_Station/PPEItems/SafetyBoots");
        if (exactBoots == null) { var tb = FindDeep("SafetyBoots"); if (tb != null) exactBoots = tb.gameObject; }
        fs.bootsItem = exactBoots;
        if (fs.bootsItem == null)
        foreach (var key in new[] { "boot", "shoe" })
        {
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t.name.ToLowerInvariant().Contains(key) && !IsUnder(t, "HazardControls")) { fs.bootsItem = t.gameObject; break; }
            if (fs.bootsItem != null) break;
        }

        Dirty();
        Debug.Log("[Polish] Sounds: " + hums.Count + " light hums, " + levers.Count + " levers, padlock at " +
                  (lockout != null ? lockout.name : "none") + ", footsteps (boots item: " +
                  (fs.bootsItem != null ? fs.bootsItem.name : "NOT FOUND - assign it on WorkshopSounds") + ").");
    }

    // ---------------------------------------------------------------- 5. blackout + torch
    [MenuItem("Tools/Workshop/Polish/5. Build Blackout (W2 isolation) + Emergency Torch")]
    static void BuildBlackout()
    {
        var old = GameObject.Find("BlackoutSystem");
        if (old != null) Undo.DestroyObjectImmediate(old);

        Transform w1 = FindDeep("Isolator_W1");
        Transform w2 = FindDeep("Isolator_W2");
        if (w2 == null) { Fail("Could not find BreakerPanel/Isolator_W2."); return; }

        var root = new GameObject("BlackoutSystem");
        Undo.RegisterCreatedObjectUndo(root, "blackout");
        var bc = root.AddComponent<BlackoutController>();
        bc.trigger = BlackoutController.Trigger.W2Isolated;
        bc.w1Lever = w1 != null ? FindChild(w1, "Lever") : null;

        var beacon = FindDeep("StatusBeacon");
        foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (l.name == "BeaconLight") bc.beaconLight = l;
        if (beacon != null)
            foreach (var r in beacon.GetComponentsInChildren<Renderer>(true))
                if (r.sharedMaterial != null && r.sharedMaterial.name.StartsWith("Mat_BeaconLens")) bc.beaconLens = r;
        bc.statusLamp = w2.GetComponentInChildren<Light>(true);
        foreach (var r in w2.GetComponentsInChildren<MeshRenderer>(true))
            if (r.name.Contains("Lamp") || (r.sharedMaterial != null && r.sharedMaterial.name.Contains("Lamp"))) { bc.statusLampRenderer = r; break; }
        var exits = new List<Renderer>();
        foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            if (r.sharedMaterial != null && r.sharedMaterial.name.StartsWith("Mat_Exit")) exits.Add(r);
        bc.exitSigns = exits.ToArray();

        // ----- emergency torch + wall cradle -----
        var mYellow = Mat("Mat_SafetyYellow", new Color(1f, 0.78f, 0.05f), smooth: 0.45f);
        var mBlack = Mat("Mat_PlugBlack", new Color(0.05f, 0.05f, 0.05f), smooth: 0.5f);
        var mGrey = Mat("Mat_DarkSteel", new Color(0.2f, 0.21f, 0.22f), smooth: 0.5f, metal: 0.8f);
        var mRed = Mat("Mat_Danger", new Color(0.8f, 0.1f, 0.1f), smooth: 0.4f);
        var mLens = Mat("Mat_TorchLens", new Color(0.9f, 0.9f, 0.85f), smooth: 0.95f, emissive: true);
        var mLed = Mat("Mat_LedGreen", new Color(0.1f, 0.6f, 0.2f), smooth: 0.8f, emissive: true);

        Vector3 s = TorchSeatPos;
        var cradle = Node("TorchCradle", root.transform, s, V(0, -90, 0));
        Box("BackPlate", root.transform, V(3.925f, s.y + 0.03f, s.z), V(0.008f, 0.12f, 0.3f), mGrey).transform.SetParent(cradle.transform, true);
        Box("HookFront", root.transform, V(s.x - 0.07f, s.y - 0.035f, s.z), V(0.02f, 0.03f, 0.06f), mGrey).transform.SetParent(cradle.transform, true);
        Box("HookBack", root.transform, V(s.x + 0.04f, s.y - 0.035f, s.z), V(0.02f, 0.03f, 0.06f), mGrey).transform.SetParent(cradle.transform, true);
        Box("Arm", root.transform, V(s.x + 0.0f, s.y - 0.05f, s.z), V(0.17f, 0.01f, 0.03f), mGrey).transform.SetParent(cradle.transform, true);
        var led = Prim(PrimitiveType.Sphere, "ChargeLED", root.transform, V(3.918f, s.y + 0.06f, s.z + 0.11f), V(0.014f, 0.014f, 0.014f), mLed);
        led.transform.SetParent(cradle.transform, true);
        var ledLight = PointLight("ChargeLEDLight", cradle.transform, led.transform.position + V(-0.04f, 0, 0), new Color(0.2f, 1f, 0.3f), 1.0f, 0.7f);
        var plate = Box("SignPlate", root.transform, V(3.926f, s.y + 0.15f, s.z), V(0.005f, 0.06f, 0.28f), mYellow);
        plate.transform.SetParent(cradle.transform, true);
        var lbl = Label("SignText", root.transform, V(3.92f, s.y + 0.15f, s.z), V(0, 90, 0), "<b>EMERGENCY TORCH</b>", 0.3f, V2(0.26f, 0.05f), Color.black);
        lbl.transform.SetParent(cradle.transform, true);
        var seat = Node("Seat", cradle.transform, s, V(0, -90, 0));

        var torch = Node("EmergencyTorch", root.transform, s, V(0, -90, 0));
        Cyl("Body", torch.transform, V(0, 0, 0), V(0.04f, 0.08f, 0.04f), mYellow).transform.localEulerAngles = V(90, 0, 0);
        Cyl("Head", torch.transform, V(0, 0, 0.1f), V(0.06f, 0.025f, 0.06f), mBlack).transform.localEulerAngles = V(90, 0, 0);
        var lens = Cyl("Lens", torch.transform, V(0, 0, 0.126f), V(0.05f, 0.002f, 0.05f), mLens);
        lens.transform.localEulerAngles = V(90, 0, 0);
        Cyl("Tail", torch.transform, V(0, 0, -0.085f), V(0.042f, 0.01f, 0.042f), mBlack).transform.localEulerAngles = V(90, 0, 0);
        Box("Button", torch.transform, V(0, 0.022f, 0.03f), V(0.012f, 0.006f, 0.02f), mRed);
        var beamGo = Node("Beam", torch.transform, V(0, 0, 0.13f), Vector3.zero, local: true);
        var beam = beamGo.AddComponent<Light>();
        beam.type = LightType.Spot; beam.range = 8f; beam.spotAngle = 55f; beam.innerSpotAngle = 28f;
        beam.intensity = 4f; beam.color = new Color(1f, 0.96f, 0.88f); beam.shadows = LightShadows.Hard; beam.enabled = false;
        var grip = Node("Grip", torch.transform, V(0, 0, -0.02f), Vector3.zero, local: true);
        var col = torch.AddComponent<CapsuleCollider>();
        col.direction = 2; col.center = V(0, 0, 0.02f); col.height = 0.27f; col.radius = 0.045f;
        Grabbable(torch, grip.transform, 0.4f);
        var tc = torch.AddComponent<TorchControl>();
        tc.beam = beam; tc.lens = lens.GetComponent<Renderer>(); tc.chargeLed = ledLight;
        tc.chargeLedRenderer = led.GetComponent<Renderer>(); tc.cradleSeat = seat.transform;
        bc.torch = tc;

        var snd = Object.FindFirstObjectByType<WorkshopSoundscape>();
        if (snd != null) { Undo.RecordObject(snd, "link"); snd.blackout = bc; }

        Dirty();
        Debug.Log("[Polish] Blackout + torch built. Lights go off when W2 is isolated (until the supply is restored). Beacon: " +
                  (bc.beaconLight != null ? "ok" : "NOT FOUND") + ", W2 lamp: " + (bc.statusLamp != null ? "ok" : "NOT FOUND") +
                  ", EXIT signs: " + exits.Count);
    }


    // ---------------------------------------------------------------- 3c. damaged lead: one socket, one plug
    [MenuItem("Tools/Workshop/Polish/3c. Auto-fix Damaged-Lead plug (one socket + one plug)")]
    static void AutoFixDamagedLead()
    {
        var plugGo = GameObject.Find("HazardControls/Plug_DamagedLead");
        var socket = GameObject.Find("HazardControls/Socket_DamagedLead");
        if (plugGo == null || socket == null) { Debug.LogWarning("[Polish] 3c: Plug_DamagedLead / Socket_DamagedLead not found (run Build Realistic Hazards)."); return; }
        var plug = plugGo.GetComponent<PlugControl>();
        var hazard = plug != null && plug.hazard != null ? plug.hazard : Hazard.Find("DamagedCable");
        var seat = socket.transform.Find("PlugSeat");
        if (plug == null || hazard == null || seat == null) { Debug.LogWarning("[Polish] 3c: plug/hazard/seat missing."); return; }

        // the uploaded cable model inside the hazard (e.g. "PC-Cable")
        Transform model = null;
        foreach (var t in hazard.GetComponentsInChildren<Transform>(true))
        {
            if (t == hazard.transform || !t.name.ToLowerInvariant().Contains("cable")) continue;
            if (hazard.marker != null && t.IsChildOf(hazard.marker.transform)) continue;
            if (t.GetComponentInChildren<Renderer>(true) == null) continue;
            model = t; break;
        }

        // the wall socket model: a small flat plate on the east wall near the lead (not one of ours)
        Renderer sock = null; float best = 2.2f;
        Vector3 hp = hazard.transform.position;
        foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
        {
            if (!r.enabled || r is ParticleSystemRenderer || r is LineRenderer || IsUnder(r.transform, "HazardControls") || IsUnder(r.transform, "BlackoutSystem")) continue;
            var b = r.bounds; var sz = b.size;
            float big = Mathf.Max(sz.x, Mathf.Max(sz.y, sz.z));
            if (big > 0.2f || big < 0.04f || sz.x > 0.045f) continue;   // small, flat against the east wall
            if (b.center.x < 3.6f || b.center.y < 0.1f || b.center.y > 1.4f) continue;
            float d = HazardBridge.FlatDistance(b.center, hp);
            if (d < best) { best = d; sock = r; }
        }
        if (sock == null)
        {
            Debug.LogWarning("[Polish] 3c: no wall-socket model found near the damaged lead. Select your socket model and use Polish > 3a instead.");
            return;
        }

        var sb = sock.bounds;
        Vector3 target = new Vector3(sb.min.x - 0.016f, sb.center.y, sb.center.z);
        Undo.RecordObject(socket.transform, "move socket");
        socket.transform.position += target - seat.position;
        foreach (var r in socket.GetComponentsInChildren<Renderer>(true)) { Undo.RecordObject(r, "hide"); r.enabled = false; }
        Undo.RecordObject(plugGo.transform, "move plug");
        plugGo.transform.SetPositionAndRotation(target, socket.transform.rotation);

        // which part of the model is its plug? It shows while plugged in; ours takes over once pulled out.
        var parts = new List<GameObject>();
        bool whole = false;
        Renderer plugPart = null;
        if (model != null)
        {
            var rends = model.GetComponentsInChildren<Renderer>(true);
            float bd = 0.18f;
            if (rends.Length >= 2)
                foreach (var r in rends)
                {
                    var z = r.bounds.size;
                    if (Mathf.Max(z.x, Mathf.Max(z.y, z.z)) > 0.25f) continue;
                    float d = Vector3.Distance(r.bounds.center, target);
                    if (d < bd) { bd = d; plugPart = r; }
                }
            if (plugPart != null) parts.Add(plugPart.gameObject);
            else { parts.Add(model.gameObject); whole = true; }
        }

        Vector3 anchorPos;
        if (plugPart != null) { var pb = plugPart.bounds; anchorPos = new Vector3(pb.center.x, pb.min.y, pb.center.z); }
        else
        {
            var dp = FindChild(hazard.transform, "DamagePoint");
            anchorPos = dp != null ? dp.position : hazard.transform.position + V(0, 0.02f, 0);
        }
        if (plug.leadAnchor != null) { Undo.RecordObject(plug.leadAnchor, "anchor"); plug.leadAnchor.position = anchorPos; }

        Undo.RecordObject(plug, "plug");
        plug.modelWhilePlugged = parts.ToArray();
        var own = new List<Renderer>(plugGo.GetComponentsInChildren<Renderer>(true));
        plug.ownVisuals = own.ToArray();
        foreach (var r in own) { Undo.RecordObject(r, "hide"); r.enabled = parts.Count == 0; }
        if (plug.leadPoint != null) plug.leadLength = Vector3.Distance(plug.leadPoint.position, anchorPos) + (whole ? 0.7f : 0.9f);
        if (plug.lead != null)
        {
            Undo.RecordObject(plug.lead, "lead");
            plug.lead.enabled = parts.Count == 0;
            if (plug.leadPoint != null) PlugControl.DrawLead(plug.lead, plug.leadPoint.position, anchorPos, plug.leadLength);
        }
        var col = plugGo.GetComponent<BoxCollider>();
        if (col != null) { Undo.RecordObject(col, "col"); col.center = V(0, -0.01f, 0.012f); col.size = V(0.13f, 0.14f, 0.1f); }
        Record(plug);
        Dirty();
        Debug.Log("[Polish] 3c: Damaged-lead plug now sits in '" + sock.name + "'. Our extra socket is hidden. " +
                  (plugPart != null ? "Model plug part '" + plugPart.name + "' is swapped for ours when pulled out."
                   : model != null ? "Model '" + model.name + "' is one mesh: when unplugged it is replaced by our plug + lead from the damage point."
                   : "No cable model found: our own plug + lead are used."));
    }

    // ---------------------------------------------------------------- 3d. power strip socket in the open
    [MenuItem("Tools/Workshop/Polish/3d. Move Power-Strip socket to an open spot")]
    static void MoveStripSocket()
    {
        var plugGo = GameObject.Find("HazardControls/Plug_PowerStrip");
        var socket = GameObject.Find("HazardControls/Socket_PowerStrip");
        if (plugGo == null || socket == null) return;
        bool aligned = true;
        foreach (var r in socket.GetComponentsInChildren<Renderer>(true)) if (r.enabled) aligned = false;
        if (aligned) { Debug.Log("[Polish] 3d: power-strip plug is aligned to your own socket model; not moved."); return; }

        var plug = plugGo.GetComponent<PlugControl>();
        var seat = socket.transform.Find("PlugSeat");
        Vector3 sp = plug != null && plug.hazard != null ? plug.hazard.transform.position : new Vector3(2.6f, 0.95f, 1.7f);
        Vector3 target = new Vector3(3.92f, 0.95f, Mathf.Clamp(sp.z + 0.45f, 1.6f, 2.15f));
        Undo.RecordObject(socket.transform, "move socket");
        socket.transform.position = target;
        Undo.RecordObject(plugGo.transform, "move plug");
        plugGo.transform.SetPositionAndRotation(seat.position, socket.transform.rotation);
        var col = plugGo.GetComponent<BoxCollider>();
        if (col != null) { Undo.RecordObject(col, "col"); col.center = V(0, -0.01f, 0.012f); col.size = V(0.13f, 0.14f, 0.1f); }
        if (plug != null && plug.leadAnchor != null && plug.leadPoint != null)
        {
            Undo.RecordObject(plug, "lead");
            plug.leadLength = Vector3.Distance(plug.leadPoint.position, plug.leadAnchor.position) + 0.6f;
            if (plug.lead != null) { Undo.RecordObject(plug.lead, "lead"); PlugControl.DrawLead(plug.lead, plug.leadPoint.position, plug.leadAnchor.position, plug.leadLength); }
            Record(plug);
        }
        Dirty();
        Debug.Log("[Polish] 3d: power-strip socket moved to " + target + " (out of the corner, above the stool, below the torch).");
    }

    // ---------------------------------------------------------------- 6. door
    [MenuItem("Tools/Workshop/Polish/6. Build Swinging Entrance Door")]
    static void BuildDoor()
    {
        var oldDoor = GameObject.Find("HazardControls/EntranceDoorSwing");
        if (oldDoor != null) Undo.DestroyObjectImmediate(oldDoor);
        var oldGap = GameObject.Find("HazardControls/Doorway");
        if (oldGap != null) Undo.DestroyObjectImmediate(oldGap);

        Renderer leaf = null; float bd = 0.8f;
        Vector3 want = V(-2.5f, 1.05f, -2.98f);
        var all = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
        foreach (var r in all)
        {
            if (r is ParticleSystemRenderer || r is LineRenderer || IsUnder(r.transform, "HazardControls")) continue;
            var sz = r.bounds.size;
            if (sz.y < 1.7f || sz.y > 2.4f) continue;
            float w = Mathf.Max(sz.x, sz.z), th = Mathf.Min(sz.x, sz.z);
            if (w < 0.7f || w > 1.3f || th > 0.12f) continue;
            float d = Vector3.Distance(r.bounds.center, want);
            if (d < bd) { bd = d; leaf = r; }
        }
        if (leaf == null && Selection.activeGameObject != null) leaf = Selection.activeGameObject.GetComponent<Renderer>();
        if (leaf == null) { Fail("Could not find the entrance door panel. Select the door panel in the Hierarchy and run 6 again."); return; }

        var lb = leaf.bounds;
        bool alongX = lb.size.x >= lb.size.z;
        Vector3 axis = alongX ? Vector3.right : Vector3.forward;
        Vector3 normal = alongX ? Vector3.forward : Vector3.right;
        if (Vector3.Dot(-lb.center, normal) < 0f) normal = -normal;           // points into the room
        float half = (alongX ? lb.size.x : lb.size.z) * 0.5f;
        float thick = alongX ? lb.size.z : lb.size.x;

        // handle, kick plate, window... = small parts inside the door outline
        var box = lb;
        box.Expand(new Vector3(alongX ? 0.04f : 0.3f, 0.04f, alongX ? 0.3f : 0.04f));
        var parts = new List<Transform> { leaf.transform };
        Transform handle = null; float hOff = 0f;
        foreach (var r in all)
        {
            if (r == leaf || r is ParticleSystemRenderer || r is LineRenderer) continue;
            if (r.transform.IsChildOf(leaf.transform) || leaf.transform.IsChildOf(r.transform) || IsUnder(r.transform, "HazardControls")) continue;
            var b = r.bounds;
            if (!box.Contains(b.min) || !box.Contains(b.max) || b.size.y > 1.6f) continue;
            parts.Add(r.transform);
            float off = Vector3.Dot(b.center - lb.center, axis);
            if (b.size.magnitude < 0.4f && Mathf.Abs(off) > 0.15f && Mathf.Abs(off) > Mathf.Abs(hOff)) { hOff = off; handle = r.transform; }
        }
        // don't move a part twice (child of another moved part)
        var snapshot = new List<Transform>(parts);
        parts.RemoveAll(pt => pt != leaf.transform && snapshot.Exists(other => other != pt && pt.IsChildOf(other)));

        float side = handle != null ? -Mathf.Sign(hOff) : -1f;                 // hinge on the side away from the handle
        Vector3 hingePos = lb.center + axis * side * half; hingePos.y = lb.min.y;
        Vector3 free = lb.center - axis * side * half;
        float ang = 95f, bestD = float.MaxValue;
        foreach (var a in new[] { 95f, -95f })
        {
            Vector3 p = hingePos + Quaternion.Euler(0f, a, 0f) * (free - hingePos);
            float d = new Vector2(p.x, p.z).magnitude;
            if (d < bestD) { bestD = d; ang = a; }
        }

        // moving parts must not be "static" (static meshes can't move in Play mode)
        foreach (var p in parts)
            foreach (var t in p.GetComponentsInChildren<Transform>(true))
                if (GameObjectUtility.GetStaticEditorFlags(t.gameObject) != 0)
                {
                    Undo.RecordObject(t.gameObject, "not static");
                    GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(t.gameObject);
                }

        // grab box outside any prefab: point at the door + grip
        var group = GameObject.Find("HazardControls");
        var root = new GameObject("EntranceDoorSwing");
        Undo.RegisterCreatedObjectUndo(root, "door");
        if (group != null) root.transform.SetParent(group.transform, false);
        root.transform.SetPositionAndRotation(lb.center, Quaternion.identity);
        var col = root.AddComponent<BoxCollider>();
        col.size = alongX ? V(half * 2f, lb.size.y, Mathf.Max(thick, 0.06f)) : V(Mathf.Max(thick, 0.06f), lb.size.y, half * 2f);
        root.AddComponent<XRSimpleInteractable>();
        var door = root.AddComponent<SwingDoor>();
        door.parts = parts.ToArray(); door.hingePoint = hingePos; door.handle = handle; door.openAngle = ang;

        // dark doorway behind the panel (hidden while closed, seen when the door opens)
        var mDark = Mat("Mat_Doorway", new Color(0.05f, 0.05f, 0.06f), unlit: true);
        var gap = Box("Doorway", root.transform.parent, Vector3.zero, Vector3.one, mDark);
        gap.transform.position = lb.center - normal * (thick * 0.5f - 0.004f);
        gap.transform.rotation = Quaternion.identity;
        gap.transform.localScale = alongX ? V(half * 2f - 0.02f, lb.size.y - 0.02f, 0.002f) : V(0.002f, lb.size.y - 0.02f, half * 2f - 0.02f);
        Undo.RegisterCreatedObjectUndo(gap, "doorway");

        Dirty();
        Debug.Log("[Polish] 6: door '" + leaf.name + "' swings around its hinge (" + parts.Count + " parts move, handle: " +
                  (handle != null ? handle.name : "none") + "). Point + grip to open; it closes by itself after 5 s.");
    }

    // ================================================================ helpers
    static void Fail(string msg) => EditorUtility.DisplayDialog("Workshop Polish", msg, "OK");
    static void Dirty() => EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    static void Record(Object o) { EditorUtility.SetDirty(o); PrefabUtility.RecordPrefabInstancePropertyModifications(o); }

    static void Remove(Transform parent, string name)
    {
        var t = parent.Find(name);
        if (t != null) Undo.DestroyObjectImmediate(t.gameObject);
    }

    static bool IsUnder(Transform t, string name)
    {
        for (var p = t.parent; p != null; p = p.parent) if (p.name == name) return true;
        return false;
    }

    static Transform FindDeep(string name)
    {
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t.name == name) return t;
        return null;
    }

    static Transform FindChild(Transform root, string name)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
        return null;
    }

    static Bounds WorldBounds(GameObject go)
    {
        bool has = false; var b = new Bounds();
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            if (!r.enabled) continue;
            if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds);
        }
        return b;
    }

    static Vector3 Abs(this Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

    static void StripToVisuals(GameObject root)
    {
        for (int pass = 0; pass < 8; pass++)
        {
            bool any = false;
            foreach (var c in root.GetComponentsInChildren<Component>(true))
            {
                if (c == null || c is Transform || c is MeshFilter || c is MeshRenderer || c is SkinnedMeshRenderer) continue;
                bool required = false;
                foreach (var o in c.GetComponents<Component>())
                {
                    if (o == null || o == c) continue;
                    foreach (RequireComponent rc in o.GetType().GetCustomAttributes(typeof(RequireComponent), true))
                        foreach (var rt in new[] { rc.m_Type0, rc.m_Type1, rc.m_Type2 })
                            if (rt != null && rt.IsAssignableFrom(c.GetType())) required = true;
                }
                if (required) continue;
                Object.DestroyImmediate(c);
                any = true;
            }
            if (!any) break;
        }
        foreach (var r in root.GetComponentsInChildren<Renderer>(true)) r.enabled = true;
    }

    static XRGrabInteractable Grabbable(GameObject go, Transform attach, float mass)
    {
        var rb = go.GetComponent<Rigidbody>();
        if (rb == null) rb = go.AddComponent<Rigidbody>();
        rb.mass = mass; rb.isKinematic = true; rb.useGravity = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        var g = go.AddComponent<XRGrabInteractable>();
        g.movementType = XRBaseInteractable.MovementType.Kinematic;
        g.throwOnDetach = false;
        if (attach != null) g.attachTransform = attach;
        return g;
    }

    static Light PointLight(string name, Transform parent, Vector3 pos, Color c, float range, float intensity)
    {
        var go = Node(name, parent, pos, Vector3.zero);
        var l = go.AddComponent<Light>();
        l.type = LightType.Point; l.color = c; l.range = range; l.intensity = intensity; l.shadows = LightShadows.None;
        return l;
    }

    static GameObject Node(string name, Transform parent, Vector3 pos, Vector3 euler, bool local = false)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        if (local) { go.transform.localPosition = pos; go.transform.localEulerAngles = euler; }
        else { go.transform.position = pos; go.transform.eulerAngles = euler; }
        return go;
    }

    static GameObject Prim(PrimitiveType t, string name, Transform parent, Vector3 lp, Vector3 ls, Material m)
    {
        var go = GameObject.CreatePrimitive(t);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = lp;
        go.transform.localScale = ls;
        go.GetComponent<Renderer>().sharedMaterial = m;
        return go;
    }

    static GameObject Box(string n, Transform p, Vector3 lp, Vector3 ls, Material m) => Prim(PrimitiveType.Cube, n, p, lp, ls, m);
    static GameObject Cyl(string n, Transform p, Vector3 lp, Vector3 ls, Material m) => Prim(PrimitiveType.Cylinder, n, p, lp, ls, m);

    static TextMeshPro Label(string name, Transform parent, Vector3 lp, Vector3 euler, string text, float size, Vector2 rect, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = lp;
        go.transform.localEulerAngles = euler;
        var t = go.AddComponent<TextMeshPro>();
        t.text = text; t.fontSize = size; t.enableAutoSizing = true; t.fontSizeMin = size * 0.3f; t.fontSizeMax = size;
        t.alignment = TextAlignmentOptions.Center; t.textWrappingMode = TextWrappingModes.Normal; t.color = color;
        t.rectTransform.sizeDelta = rect;
        return t;
    }

    static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);
    static Vector2 V2(float x, float y) => new Vector2(x, y);

    static Material Mat(string name, Color c, bool unlit = false, float smooth = 0.3f, float metal = 0f, bool emissive = false)
    {
        foreach (var g in AssetDatabase.FindAssets(name + " t:Material"))
        {
            var p = AssetDatabase.GUIDToAssetPath(g);
            if (Path.GetFileNameWithoutExtension(p) == name)
            {
                var existing = AssetDatabase.LoadAssetAtPath<Material>(p);
                if (emissive && !existing.IsKeywordEnabled("_EMISSION")) { existing.EnableKeyword("_EMISSION"); EditorUtility.SetDirty(existing); }
                return existing;
            }
        }
        if (!AssetDatabase.IsValidFolder(MatFolder))
        {
            var parts = MatFolder.Split('/'); string cur = parts[0];
            for (int i = 1; i < parts.Length; i++) { string nx = cur + "/" + parts[i]; if (!AssetDatabase.IsValidFolder(nx)) AssetDatabase.CreateFolder(cur, parts[i]); cur = nx; }
        }
        var m = new Material(Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit"));
        m.SetColor("_BaseColor", c);
        if (!unlit) { m.SetFloat("_Smoothness", smooth); m.SetFloat("_Metallic", metal); }
        if (emissive)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", Color.black);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        AssetDatabase.CreateAsset(m, MatFolder + "/" + name + ".mat");
        return m;
    }
}