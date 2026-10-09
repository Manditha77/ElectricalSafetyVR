using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// Tools > Workshop > Build Realistic Hazards
// Turns the 4 pre-check hazards into "spot it -> make it safe" tasks with real consequences.
// Safe to run again: it rebuilds the HazardControls group each time.
public static class HazardSetup
{
    const string MatFolder = "Assets/_Project/B_Environment/Materials";
    const string GroupName = "HazardControls";

    // ---------- positions (change here if something clashes in your scene) ----------
    static readonly Vector3 SpillKitPos = new Vector3(-1.6f, 0f, -2.72f);    // by the entrance door
    static readonly Vector3 TrolleyPos = new Vector3(-0.55f, 0f, -2.62f);    // south wall
    static readonly Vector3 CableSocketPos = new Vector3(3.92f, 0.3f, 0f);   // z is taken from the cable
    static readonly Vector3 StripSocketPos = new Vector3(3.92f, 1.05f, 2.3f);
    static readonly Vector3 ReportPos = new Vector3(0.2f, 1.6f, -2.965f);
    const float EastWallYaw = -90f; // socket faces into the room (-x)

    [MenuItem("Tools/Workshop/Build Realistic Hazards")]
    public static void Build()
    {
        Hazard puddle = null, cable = null, strip = null, tool = null;
        foreach (var h in Object.FindObjectsByType<Hazard>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            string n = h.gameObject.name;
            if (n.StartsWith("Hazard_Puddle")) puddle = h;
            else if (n.StartsWith("Hazard_DamagedCable")) cable = h;
            else if (n.StartsWith("Hazard_OverloadedStrip")) strip = h;
            else if (n.StartsWith("Hazard_MetalTool")) tool = h;
        }
        if (puddle == null || cable == null || strip == null || tool == null)
        {
            EditorUtility.DisplayDialog("Build Realistic Hazards",
                "Could not find all 4 hazards in the open scene:\nHazard_Puddle, Hazard_DamagedCable, Hazard_OverloadedStrip, Hazard_MetalTool.\n\nOpen ElectricalWorkshop and try again.", "OK");
            return;
        }

        var old = GameObject.Find(GroupName);
        if (old != null) Undo.DestroyObjectImmediate(old);
        var group = new GameObject(GroupName);
        Undo.RegisterCreatedObjectUndo(group, "Build Realistic Hazards");

        // ---------- materials ----------
        var mCalloutBg = Mat("Mat_CalloutBg", new Color(0.06f, 0.07f, 0.09f), unlit: true);
        var mCalloutBar = Mat("Mat_CalloutBar", Color.white, unlit: true);
        var mYellow = Mat("Mat_SafetyYellow", new Color(1f, 0.78f, 0.05f), smooth: 0.45f);
        var mRed = Mat("Mat_TrolleyRed", new Color(0.62f, 0.07f, 0.06f), smooth: 0.55f, metal: 0.3f);
        var mSteel = Mat("Mat_Steel", new Color(0.72f, 0.74f, 0.76f), smooth: 0.8f, metal: 1f);
        var mDarkSteel = Mat("Mat_DarkSteel", new Color(0.2f, 0.21f, 0.22f), smooth: 0.5f, metal: 0.8f);
        var mWhite = Mat("Mat_PlasticWhite", new Color(0.93f, 0.93f, 0.9f), smooth: 0.55f);
        var mBlack = Mat("Mat_PlugBlack", new Color(0.05f, 0.05f, 0.05f), smooth: 0.5f);
        var mHole = Mat("Mat_SocketHole", new Color(0.02f, 0.02f, 0.02f), unlit: true);
        var mNeon = Mat("Mat_NeonRed", new Color(1f, 0.15f, 0.1f), unlit: true);
        var mLeadGrey = Mat("Mat_LeadGrey", new Color(0.55f, 0.56f, 0.58f), smooth: 0.35f);
        var mLeadBlack = Mat("Mat_LeadBlack", new Color(0.07f, 0.07f, 0.08f), smooth: 0.4f);
        var mMopHead = Mat("Mat_MopHead", new Color(0.82f, 0.8f, 0.74f), smooth: 0.1f);
        var mMopHandle = Mat("Mat_MopHandle", new Color(0.1f, 0.35f, 0.75f), smooth: 0.6f);
        var mWater = Mat("Mat_BucketWater", new Color(0.22f, 0.3f, 0.33f), smooth: 0.95f);
        var mRubber = Mat("Mat_Rubber", new Color(0.08f, 0.08f, 0.08f), smooth: 0.2f);
        var mSpark = ParticleMat("Mat_Sparks", new Color(1f, 0.85f, 0.45f, 1f), additive: true);
        var mSmoke = ParticleMat("Mat_Smoke", new Color(0.3f, 0.3f, 0.3f, 0.6f), additive: false);

        // ---------- 1. hazard texts + rewire G to "spot" ----------
        Configure(puddle, HazardLevel.Caution, "WET FLOOR",
            "Slip and fall right in front of Workstation 2. Water near 230 V parts can carry current.",
            "Mop it up with the SPILL KIT by the door.",
            "Spill mopped up. Wet-floor sign left out while the floor dries.", false, false);
        Configure(cable, HazardLevel.Danger, "DAMAGED LEAD (LIVE)",
            "Bare conductors at 230 V: electric shock, sparks and fire. It is on a wall socket, so isolating W2 will NOT make it dead.",
            "Pull its plug out of the wall socket.",
            "Unplugged and tagged DANGER - DO NOT USE. Taken out of service.", false, false);
        Configure(strip, HazardLevel.Danger, "OVERLOADED POWER STRIP",
            "Too many plugs on one strip: it overheats, melts and can start a fire. It is warm and buzzing.",
            "Unplug the strip at the wall socket behind the bench.",
            "Unplugged and tagged DANGER - DO NOT USE. One plug per socket.", false, false);
        Configure(tool, HazardLevel.Caution, "LOOSE METAL TOOL",
            "A spanner on Workstation 2 can fall into the open fuse holder and short live parts, or be left there when power comes back.",
            "Pick it up and put it on the TOOL RETURN trolley.",
            "Tool removed and stored. Work area clear.", true, true);

        // ---------- 2. callouts ----------
        puddle.callout = MakeCallout("Callout_WetFloor", CalloutPos(puddle.transform.position, 1.45f, 0.3f), group.transform, mCalloutBg, mCalloutBar);
        cable.callout = MakeCallout("Callout_DamagedLead", CalloutPos(cable.transform.position, 1.4f, 0.3f), group.transform, mCalloutBg, mCalloutBar);
        strip.callout = MakeCallout("Callout_PowerStrip", CalloutPos(strip.transform.position, Mathf.Max(1.5f, strip.transform.position.y + 0.6f), 0.3f), group.transform, mCalloutBg, mCalloutBar);
        tool.callout = MakeCallout("Callout_LooseTool", CalloutPos(tool.transform.position, tool.transform.position.y + 0.55f, 0.45f), group.transform, mCalloutBg, mCalloutBar);
        foreach (var h in new[] { puddle, cable, strip, tool }) Record(h);

        // ---------- 3. WET FLOOR: spill kit + mop ----------
        var kit = Node("SpillKit", group.transform, SpillKitPos, Vector3.zero);
        Cyl("Bucket", kit.transform, V(0, 0.15f, 0), V(0.34f, 0.15f, 0.34f), mYellow);
        Cyl("BucketWater", kit.transform, V(0, 0.27f, 0), V(0.31f, 0.005f, 0.31f), mWater);
        Box("Wringer", kit.transform, V(0.1f, 0.33f, 0), V(0.12f, 0.07f, 0.2f), mDarkSteel);
        Box("PadsBox", kit.transform, V(-0.32f, 0.12f, 0.02f), V(0.22f, 0.24f, 0.16f), mYellow);
        Label("PadsLabel", kit.transform, V(-0.32f, 0.14f, 0.102f), V(0, 180, 0), "ABSORBENT\nPADS", 0.2f, V2(0.2f, 0.1f), Color.black);
        Box("SpillKitSign", kit.transform, V(0, 1.25f, -0.25f), V(0.36f, 0.17f, 0.01f), mYellow);
        Label("SpillKitText", kit.transform, V(0, 1.25f, -0.243f), V(0, 180, 0),
            "<b>SPILL KIT</b>\n<size=60%>Clean spills at once</size>", 0.5f, V2(0.34f, 0.15f), Color.black);

        var mop = Node("Mop", group.transform, SpillKitPos + V(0, 0.02f, 0), V(0, 0, 6f));
        Cyl("Handle", mop.transform, V(0, 0.8f, 0), V(0.028f, 0.66f, 0.028f), mMopHandle);
        Cyl("HandleCap", mop.transform, V(0, 1.47f, 0), V(0.034f, 0.02f, 0.034f), mBlack);
        Box("HeadBlock", mop.transform, V(0, 0.12f, 0), V(0.28f, 0.03f, 0.06f), mDarkSteel);
        Box("Fringe", mop.transform, V(0, 0.06f, 0), V(0.3f, 0.09f, 0.12f), mMopHead);
        var mopHead = Node("MopHead", mop.transform, Vector3.zero, Vector3.zero, local: true);
        mopHead.transform.localPosition = V(0, 0.02f, 0);
        var mopGrip = Node("Grip", mop.transform, Vector3.zero, Vector3.zero, local: true);
        mopGrip.transform.localPosition = V(0, 1.2f, 0);
        var mopCol = mop.AddComponent<CapsuleCollider>();
        mopCol.center = V(0, 1.0f, 0); mopCol.height = 0.9f; mopCol.radius = 0.05f; mopCol.direction = 1;
        var mopGrab = Grabbable(mop, mopGrip.transform, 1f);
        var mopCtl = mop.AddComponent<MopControl>();
        mopCtl.hazard = puddle; mopCtl.head = mopHead.transform; mopCtl.puddle = puddle.transform;

        var conPuddle = Node("Consequence_WetFloor", group.transform, puddle.transform.position, Vector3.zero);
        var cp = conPuddle.AddComponent<HazardConsequence>();
        cp.hazard = puddle; cp.type = ConsequenceType.SlipOnSpill;

        // ---------- 4. DAMAGED LEAD: wall socket + plug ----------
        Vector3 cablePos = cable.transform.position;
        var cablePlug = SocketAndPlug("DamagedLead", group.transform,
            new Vector3(CableSocketPos.x, CableSocketPos.y, cablePos.z), cablePos + V(0, 0.02f, 0),
            mWhite, mHole, mNeon, mBlack, mDarkSteel, mLeadGrey, cable);

        var conCable = Node("Consequence_DamagedLead", group.transform, cablePos, Vector3.zero);
        var cc = conCable.AddComponent<HazardConsequence>();
        cc.hazard = cable; cc.type = ConsequenceType.LiveDamagedLead; cc.nearRadius = 1.0f;
        cc.sparks = Sparks(conCable.transform, cablePos + V(0, 0.04f, 0), mSpark);
        cc.glow = PointLight("ArcFlash", conCable.transform, cablePos + V(0, 0.08f, 0), new Color(0.75f, 0.85f, 1f), 0.9f);

        // ---------- 5. OVERLOADED STRIP: wall socket + plug + overheating ----------
        Vector3 stripPos = strip.transform.position;
        SocketAndPlug("PowerStrip", group.transform, StripSocketPos, stripPos + V(0, 0.02f, 0),
            mWhite, mHole, mNeon, mBlack, mDarkSteel, mLeadBlack, strip);

        var conStrip = Node("Consequence_PowerStrip", group.transform, stripPos, Vector3.zero);
        var cs = conStrip.AddComponent<HazardConsequence>();
        cs.hazard = strip; cs.type = ConsequenceType.OverheatingStrip; cs.overheatAfter = 25f;
        cs.smoke = Smoke(conStrip.transform, stripPos + V(0, 0.05f, 0), mSmoke);
        cs.glow = PointLight("HotGlow", conStrip.transform, stripPos + V(0, 0.06f, 0), new Color(1f, 0.45f, 0.1f), 0.7f);
        var loop = conStrip.AddComponent<AudioSource>();
        loop.playOnAwake = false; loop.spatialBlend = 1f; loop.minDistance = 0.5f; loop.maxDistance = 8f;
        loop.rolloffMode = AudioRolloffMode.Logarithmic; loop.dopplerLevel = 0f;
        cs.loop = loop;

        // ---------- 6. LOOSE TOOL: real spanner + tool return trolley ----------
        foreach (var r in tool.GetComponentsInChildren<Renderer>(true))
        {
            if (tool.marker != null && r.transform.IsChildOf(tool.marker.transform)) continue;
            Undo.RecordObject(r, "hide tool");
            r.enabled = false;
            PrefabUtility.RecordPrefabInstancePropertyModifications(r);
        }
        Vector3 tp = tool.transform.position;
        var spanner = Node("Spanner", group.transform, tp + V(0, 0.006f, 0), V(0, 25f, 0));
        Box("Shaft", spanner.transform, V(0, 0.003f, 0), V(0.17f, 0.006f, 0.022f), mSteel);
        Cyl("RingEnd", spanner.transform, V(-0.095f, 0.003f, 0), V(0.042f, 0.004f, 0.042f), mSteel);
        Box("JawBase", spanner.transform, V(0.095f, 0.003f, 0), V(0.03f, 0.006f, 0.044f), mSteel);
        Box("JawTop", spanner.transform, V(0.118f, 0.003f, 0.016f), V(0.026f, 0.006f, 0.012f), mSteel);
        Box("JawBottom", spanner.transform, V(0.118f, 0.003f, -0.016f), V(0.026f, 0.006f, 0.012f), mSteel);
        var spGrip = Node("Grip", spanner.transform, Vector3.zero, Vector3.zero, local: true);
        spGrip.transform.localPosition = V(-0.03f, 0.003f, 0);
        var spCol = spanner.AddComponent<BoxCollider>();
        spCol.center = V(0, 0.012f, 0); spCol.size = V(0.28f, 0.045f, 0.08f);
        Grabbable(spanner, spGrip.transform, 0.3f);

        var trolley = Node("ToolTrolley", group.transform, TrolleyPos, Vector3.zero);
        Box("TopTray", trolley.transform, V(0, 0.85f, 0), V(0.6f, 0.03f, 0.4f), mRed);
        Box("LipFront", trolley.transform, V(0, 0.875f, 0.195f), V(0.6f, 0.025f, 0.01f), mRed);
        Box("LipBack", trolley.transform, V(0, 0.875f, -0.195f), V(0.6f, 0.025f, 0.01f), mRed);
        Box("LipLeft", trolley.transform, V(-0.295f, 0.875f, 0), V(0.01f, 0.025f, 0.4f), mRed);
        Box("LipRight", trolley.transform, V(0.295f, 0.875f, 0), V(0.01f, 0.025f, 0.4f), mRed);
        Box("LowerShelf", trolley.transform, V(0, 0.3f, 0), V(0.56f, 0.02f, 0.36f), mRed);
        foreach (var x in new[] { -0.27f, 0.27f })
            foreach (var z in new[] { -0.17f, 0.17f })
            {
                Cyl("Leg", trolley.transform, V(x, 0.46f, z), V(0.024f, 0.4f, 0.024f), mSteel);
                var w = Cyl("Wheel", trolley.transform, V(x, 0.04f, z), V(0.08f, 0.012f, 0.08f), mRubber);
                w.transform.localEulerAngles = V(0, 0, 90);
            }
        Box("LabelPlate", trolley.transform, V(0, 0.79f, 0.202f), V(0.42f, 0.07f, 0.004f), mWhite);
        Label("LabelText", trolley.transform, V(0, 0.79f, 0.206f), V(0, 180, 0), "<b>TOOL RETURN</b>", 0.4f, V2(0.4f, 0.06f), Color.black);
        var seat = Node("Seat", trolley.transform, Vector3.zero, Vector3.zero, local: true);
        seat.transform.localPosition = V(0, 0.866f, 0);
        var tCol = trolley.AddComponent<BoxCollider>();
        tCol.center = V(0, 0.43f, 0); tCol.size = V(0.62f, 0.86f, 0.42f);

        var toolCtl = spanner.AddComponent<LooseToolControl>();
        toolCtl.hazard = tool; toolCtl.trolleySeat = seat.transform;

        var conTool = Node("Consequence_LooseTool", group.transform, tp, Vector3.zero);
        var ct = conTool.AddComponent<HazardConsequence>();
        ct.hazard = tool; ct.type = ConsequenceType.ToolLeftOnMachine; ct.tool = toolCtl;

        // ---------- 7. hazard report board ----------
        var rep = Node("HazardReportBoard", group.transform, ReportPos, V(0, 180, 0));
        Box("Frame", rep.transform, V(0, 0, 0), V(0.74f, 0.96f, 0.025f), mCalloutBg);
        Box("HeaderBar", rep.transform, V(0, 0.43f, -0.004f), V(0.74f, 0.1f, 0.022f), mYellow);
        Label("Header", rep.transform, V(0, 0.43f, -0.017f), Vector3.zero, "<b>HAZARD REPORT</b>", 0.6f, V2(0.7f, 0.08f), Color.black);
        var listT = Label("List", rep.transform, V(0, -0.06f, -0.014f), Vector3.zero, "", 0.32f, V2(0.66f, 0.8f), Color.white);
        listT.alignment = TextAlignmentOptions.TopLeft;
        listT.fontSizeMin = 0.12f; listT.fontSizeMax = 0.32f;
        var board = rep.AddComponent<HazardReportBoard>();
        board.list = listT;

        // ---------- 8. more time for a real check ----------
        var sm = Object.FindFirstObjectByType<SessionManager>();
        if (sm != null)
        {
            var so = new SerializedObject(sm);
            var p = so.FindProperty("preCheckSeconds");
            if (p != null && p.propertyType == SerializedPropertyType.Float && p.floatValue < 180f) { p.floatValue = 180f; so.ApplyModifiedProperties(); }
            else if (p != null && p.propertyType == SerializedPropertyType.Integer && p.intValue < 180) { p.intValue = 180; so.ApplyModifiedProperties(); }
        }

        EditorSceneManager.MarkSceneDirty(group.scene);
        Selection.activeGameObject = group;
        Debug.Log("[HazardSetup] Built realistic hazards: spill kit + mop, 2 wall sockets + plugs, spanner + tool trolley, callouts, consequences and the hazard report board. Pre-check time set to 180 s.");
    }

    // ================= helpers =================

    static void Configure(Hazard h, HazardLevel lvl, string title, string risk, string control, string done,
                          bool hideMarker, bool disableColliders)
    {
        Undo.RecordObject(h, "Configure hazard");
        h.level = lvl; h.title = title; h.risk = risk; h.control = control; h.controlledText = done;
        h.hideMarkerWhenControlled = hideMarker;
        h.disableCollidersWhenSpotted = disableColliders;

        // G used to call Identify() straight away. Now G = Spot (done in code), so remove the old wiring.
        foreach (var it in h.GetComponentsInChildren<XRBaseInteractable>(true))
        {
            bool changed = false;
            for (int i = it.selectEntered.GetPersistentEventCount() - 1; i >= 0; i--)
                if (it.selectEntered.GetPersistentMethodName(i) == "Identify") { UnityEventTools.RemovePersistentListener(it.selectEntered, i); changed = true; }
            for (int i = it.activated.GetPersistentEventCount() - 1; i >= 0; i--)
                if (it.activated.GetPersistentMethodName(i) == "Identify") { UnityEventTools.RemovePersistentListener(it.activated, i); changed = true; }
            if (changed) Record(it);
        }
    }

    static void Record(Object o)
    {
        EditorUtility.SetDirty(o);
        PrefabUtility.RecordPrefabInstancePropertyModifications(o);
    }

    static Vector3 CalloutPos(Vector3 p, float y, float pushToCentre)
    {
        var flat = new Vector3(-p.x, 0, -p.z);
        if (flat.sqrMagnitude > 0.001f) flat.Normalize();
        return new Vector3(p.x, y, p.z) + flat * pushToCentre;
    }

    static HazardCallout MakeCallout(string name, Vector3 pos, Transform parent, Material bg, Material barMat)
    {
        var root = Node(name, parent, pos, Vector3.zero);
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
        return c;
    }

    static PlugControl SocketAndPlug(string name, Transform parent, Vector3 socketPos, Vector3 anchorPos,
        Material white, Material hole, Material neon, Material black, Material grey, Material leadMat, Hazard hazard)
    {
        var rot = V(0, EastWallYaw, 0);
        var socket = Node("Socket_" + name, parent, socketPos, rot);
        Box("BackBox", socket.transform, V(0, 0, 0.004f), V(0.09f, 0.09f, 0.008f), white);
        Box("Plate", socket.transform, V(0, 0, 0.01f), V(0.086f, 0.086f, 0.006f), white);
        Box("HoleEarth", socket.transform, V(-0.012f, 0.02f, 0.0132f), V(0.004f, 0.009f, 0.001f), hole);
        Box("HoleLive", socket.transform, V(-0.024f, -0.006f, 0.0132f), V(0.008f, 0.004f, 0.001f), hole);
        Box("HoleNeutral", socket.transform, V(0f, -0.006f, 0.0132f), V(0.008f, 0.004f, 0.001f), hole);
        Box("Switch", socket.transform, V(0.027f, -0.004f, 0.016f), V(0.016f, 0.026f, 0.008f), white);
        var ind = Box("OnIndicator", socket.transform, V(0.027f, 0.024f, 0.0135f), V(0.008f, 0.005f, 0.002f), neon);
        var seat = Node("PlugSeat", socket.transform, Vector3.zero, Vector3.zero, local: true);
        seat.transform.localPosition = V(-0.012f, 0.002f, 0.028f);

        var plug = Node("Plug_" + name, parent, seat.transform.position, rot);
        Box("Body", plug.transform, V(0, 0, 0), V(0.045f, 0.052f, 0.03f), black);
        Box("GripTop", plug.transform, V(0, 0.012f, 0), V(0.047f, 0.004f, 0.026f), grey);
        Box("GripBottom", plug.transform, V(0, -0.012f, 0), V(0.047f, 0.004f, 0.026f), grey);
        Cyl("Boot", plug.transform, V(0, -0.034f, 0), V(0.016f, 0.01f, 0.016f), black);
        var lp = Node("LeadPoint", plug.transform, Vector3.zero, Vector3.zero, local: true);
        lp.transform.localPosition = V(0, -0.042f, 0);
        var col = plug.AddComponent<BoxCollider>();
        col.size = V(0.075f, 0.085f, 0.05f);
        Grabbable(plug, null, 0.12f);

        var anchor = Node("LeadAnchor_" + name, parent, anchorPos, Vector3.zero);

        var leadGo = Node("Lead_" + name, parent, Vector3.zero, Vector3.zero);
        var lr = leadGo.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.widthMultiplier = 0.011f;
        lr.numCapVertices = 2;
        lr.numCornerVertices = 2;
        lr.shadowCastingMode = ShadowCastingMode.Off;
        lr.sharedMaterial = leadMat;
        float length = Vector3.Distance(lp.transform.position, anchorPos) + 0.6f;
        PlugControl.DrawLead(lr, lp.transform.position, anchorPos, length);

        var ctl = plug.AddComponent<PlugControl>();
        ctl.hazard = hazard; ctl.seat = seat.transform; ctl.leadAnchor = anchor.transform;
        ctl.leadPoint = lp.transform; ctl.lead = lr; ctl.leadLength = length;
        ctl.socketIndicator = ind.GetComponent<Renderer>();
        return ctl;
    }

    static XRGrabInteractable Grabbable(GameObject go, Transform attach, float mass)
    {
        var rb = go.AddComponent<Rigidbody>();
        rb.mass = mass;
        rb.isKinematic = true;
        rb.useGravity = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        var g = go.AddComponent<XRGrabInteractable>();
        g.movementType = XRBaseInteractable.MovementType.Kinematic;
        g.throwOnDetach = false;
        if (attach != null) g.attachTransform = attach;
        return g;
    }

    static ParticleSystem Sparks(Transform parent, Vector3 pos, Material mat)
    {
        var go = Node("Sparks", parent, pos, Vector3.zero);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true; main.playOnAwake = true; main.duration = 1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 2.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.006f, 0.016f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.97f, 0.8f), new Color(1f, 0.65f, 0.2f));
        main.gravityModifier = 1.2f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 200;
        var em = ps.emission; em.rateOverTime = 0f;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Hemisphere; sh.radius = 0.02f; sh.rotation = V(-90, 0, 0);
        var col = ps.colorOverLifetime; col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.45f, 0.1f), 1f) },
                  new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
        var c = ps.collision; c.enabled = true; c.type = ParticleSystemCollisionType.World; c.mode = ParticleSystemCollisionMode.Collision3D;
        c.dampen = 0.5f; c.bounce = 0.35f; c.lifetimeLoss = 0.2f; c.quality = ParticleSystemCollisionQuality.Low;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat; r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = 0.04f; r.lengthScale = 1.5f;
        r.shadowCastingMode = ShadowCastingMode.Off;
        return ps;
    }

    static ParticleSystem Smoke(Transform parent, Vector3 pos, Material mat)
    {
        var go = Node("Smoke", parent, pos, Vector3.zero);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true; main.playOnAwake = true; main.duration = 5f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 4f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.14f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new Color(0.35f, 0.35f, 0.35f, 1f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 300;
        var em = ps.emission; em.rateOverTime = 0f;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 12f; sh.radius = 0.04f; sh.rotation = V(-90, 0, 0);
        var sol = ps.sizeOverLifetime; sol.enabled = true; sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 2.8f));
        var col = ps.colorOverLifetime; col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.6f, 0.2f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
        var n = ps.noise; n.enabled = true; n.strength = 0.08f; n.frequency = 0.6f;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat; r.renderMode = ParticleSystemRenderMode.Billboard; r.shadowCastingMode = ShadowCastingMode.Off;
        return ps;
    }

    static Light PointLight(string name, Transform parent, Vector3 pos, Color c, float range)
    {
        var go = Node(name, parent, pos, Vector3.zero);
        var l = go.AddComponent<Light>();
        l.type = LightType.Point; l.color = c; l.range = range; l.intensity = 0f; l.shadows = LightShadows.None;
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

    // TextMeshPro reads correctly when seen from its local -z side.
    static TextMeshPro Label(string name, Transform parent, Vector3 lp, Vector3 euler, string text, float size, Vector2 rect, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = lp;
        go.transform.localEulerAngles = euler;
        var t = go.AddComponent<TextMeshPro>();
        t.text = text;
        t.fontSize = size;
        t.enableAutoSizing = true;
        t.fontSizeMin = size * 0.3f;
        t.fontSizeMax = size;
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.Normal;
        t.color = color;
        t.rectTransform.sizeDelta = rect;
        return t;
    }

    static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);
    static Vector2 V2(float x, float y) => new Vector2(x, y);

    static Material Mat(string name, Color c, bool unlit = false, float smooth = 0.3f, float metal = 0f)
    {
        var existing = FindMat(name);
        if (existing != null) return existing;
        EnsureFolder(MatFolder);
        var m = new Material(Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit"));
        m.SetColor("_BaseColor", c);
        if (!unlit) { m.SetFloat("_Smoothness", smooth); m.SetFloat("_Metallic", metal); }
        AssetDatabase.CreateAsset(m, MatFolder + "/" + name + ".mat");
        return m;
    }

    static Material ParticleMat(string name, Color c, bool additive)
    {
        var existing = FindMat(name);
        if (existing != null) return existing;
        EnsureFolder(MatFolder);
        var baseMat = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/ParticlesUnlit.mat");
        var m = baseMat != null ? new Material(baseMat) : new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
        m.SetColor("_BaseColor", c);
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", additive ? 2f : 0f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)RenderQueue.Transparent;
        if (m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") == null)
            m.SetTexture("_BaseMap", AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Particle.psd"));
        AssetDatabase.CreateAsset(m, MatFolder + "/" + name + ".mat");
        return m;
    }

    static Material FindMat(string name)
    {
        foreach (var g in AssetDatabase.FindAssets(name + " t:Material"))
        {
            var p = AssetDatabase.GUIDToAssetPath(g);
            if (Path.GetFileNameWithoutExtension(p) == name) return AssetDatabase.LoadAssetAtPath<Material>(p);
        }
        return null;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parts = path.Split('/');
        string cur = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = cur + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(cur, parts[i]);
            cur = next;
        }
    }
}
