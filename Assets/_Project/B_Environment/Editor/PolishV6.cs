using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// Tools > Workshop > Polish v6 > ...   (each step can be run again safely)
public static class PolishV6
{
    const string MatFolder = "Assets/_Project/B_Environment/Materials";

    [MenuItem("Tools/Workshop/Polish v6/Run ALL (1-5)")]
    static void All()
    {
        Colliders();
        Hints();
        Poster();
        SpillKit();
        WelcomeInScene();
        Debug.Log("[PolishV6] All done. Save the scene.");
    }

    // ------------------------------------------------------------------ 1. colliders
    [MenuItem("Tools/Workshop/Polish v6/1. Solid Furniture, Floor + Ceiling (no walking through)")]
    static void Colliders()
    {
        int added = 0;
        var roots = new List<GameObject>();
        foreach (var n in new[] { "Workshop_Main", "WorkBench", "PPE_Station", "WorkshopRoom" })
        {
            var g = GameObject.Find(n);
            if (g != null) roots.Add(g);
        }
        string[] skip = { "LightPanel", "Text", "Trunk", "Skirting", "KeepClear", "Poster", "Anchor", "Sign", "Label", "Water", "Blob", "Glow", "Lamp", "Door", "Entrance", "Handle", "Window", "Kick", "Exit" };

        foreach (var root in roots)
            foreach (var mr in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                var go = mr.gameObject;
                if (go.GetComponent<Collider>() != null || go.GetComponent<TMP_Text>() != null) continue;
                if (go.GetComponentInParent<XRBaseInteractable>(true) != null || go.GetComponentInParent<Rigidbody>(true) != null) continue;
                if (go.GetComponentInParent<Hazard>(true) != null || go.GetComponentInParent<PpeItem>(true) != null) continue;
                if (go.GetComponentInParent<Canvas>(true) != null) continue;
                bool bad = false;
                foreach (var s in skip) if (go.name.Contains(s)) { bad = true; break; }
                if (bad) continue;
                var sz = mr.bounds.size;
                if (Mathf.Max(sz.x, Mathf.Max(sz.y, sz.z)) < 0.06f) continue;   // tiny details
                Undo.AddComponent<BoxCollider>(go);
                PrefabUtility.RecordPrefabInstancePropertyModifications(go);
                added++;
            }

        // the player needs a body that collides
        string cc = "";
        foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
        {
            if (mb == null || mb.GetType().Name != "XROrigin") continue;
            var c = mb.GetComponent<CharacterController>();
            if (c == null)
            {
                c = Undo.AddComponent<CharacterController>(mb.gameObject);
                c.height = 1.6f; c.radius = 0.2f; c.center = new Vector3(0f, 0.8f, 0f);
                c.stepOffset = 0.3f; c.skinWidth = 0.02f; c.slopeLimit = 45f;
                cc = "Added a Character Controller to '" + mb.name + "' (the player now collides).";
            }
            else cc = "Player already has a Character Controller.";
            break;
        }
        Dirty();
        Debug.Log("[PolishV6] 1: added " + added + " colliders (shelf, barrel, boxes, extinguisher, benches, floor, ceiling, walls...). " + cc);
    }

    // ------------------------------------------------------------------ 2. placement hints
    [MenuItem("Tools/Workshop/Polish v6/2. Placement Hints (ghost / glow where things go)")]
    static void Hints()
    {
        var old = GameObject.Find("PlacementHints");
        if (old != null) Undo.DestroyObjectImmediate(old);
        var root = new GameObject("PlacementHints");
        Undo.RegisterCreatedObjectUndo(root, "hints");

        var green = HintMat("Mat_HintGreen", new Color(0.2f, 1f, 0.4f, 0.45f));
        var blue = HintMat("Mat_HintBlue", new Color(0.25f, 0.6f, 1f, 0.45f));
        var yellow = HintMat("Mat_HintYellow", new Color(1f, 0.85f, 0.1f, 0.45f));
        var red = HintMat("Mat_HintRed", new Color(1f, 0.3f, 0.25f, 0.45f));
        var log = new StringBuilder();

        // spanner -> tool trolley (green ghost)
        var spanner = Grab(Find("Spanner", "HazardControls"));
        var seat = Find("Seat", "ToolTrolley");
        Add(root, "Spanner_to_Trolley", spanner, seat, PlacementHint.Mode.Ghost, PlacementHint.Condition.Always, green,
            new Color(0.2f, 1f, 0.4f, 0.5f), 0.45f, "RELEASE ON THE TROLLEY", null, false, log);

        // fuse puller -> tool board (blue ghost at its home)
        var puller = Grab(Find("FusePuller", null));
        Add(root, "Puller_to_ToolBoard", puller, puller != null ? puller.transform : null, PlacementHint.Mode.Ghost, PlacementHint.Condition.Always, blue,
            new Color(0.25f, 0.6f, 1f, 0.5f), 0.3f, "RELEASE TO HANG IT UP", null, true, log);

        // fuse puller -> blown fuse (yellow ring, only with the cover open)
        var blown = Find("BlownFuse", null);
        Add(root, "Puller_to_BlownFuse", puller, blown, PlacementHint.Mode.Marker, PlacementHint.Condition.CoverOpen, yellow,
            new Color(1f, 0.85f, 0.1f, 0.5f), 0.15f, "GRIP THE OLD FUSE", null, false, log);

        // new fuse -> fuse slot (green ring, cover open)
        var newFuse = Grab(Find("NewFuse", null));
        var slot = Find("FuseSlot", null);
        Add(root, "NewFuse_to_Slot", newFuse, slot, PlacementHint.Mode.Marker, PlacementHint.Condition.CoverOpen, green,
            new Color(0.2f, 1f, 0.4f, 0.5f), 0.15f, "RELEASE TO FIT", null, false, log);

        // tester -> W2 test point (red ring + both terminal threads glow)
        var tester = Grab(Find("VoltageTester", null));
        var testPoint = Find("TestTerminal", null) ?? Find("TestZone", null);
        var ws = GameObject.Find("Workstation2");
        var terms = new List<Renderer>();
        if (ws != null)
            foreach (var r in ws.GetComponentsInChildren<Renderer>(true))
            {
                string n = r.name.ToLowerInvariant();
                if ((n.Contains("terminal") || n.Contains("term_") || n.StartsWith("term")) && Mathf.Max(r.bounds.size.x, Mathf.Max(r.bounds.size.y, r.bounds.size.z)) < 0.12f) terms.Add(r);
            }
        Add(root, "Tester_to_TestPoint", tester, testPoint, PlacementHint.Mode.Marker, PlacementHint.Condition.Always, red,
            new Color(1f, 0.3f, 0.25f, 0.5f), 0.25f, "TOUCH THE TEST POINT", terms.ToArray(), false, log);

        // tester -> proving unit contact (yellow ring + contact glows)
        var prove = Find("ProveZone", null) ?? Find("ProvingUnit", null);
        var pu = GameObject.Find("ProvingUnit") ?? (prove != null ? prove.gameObject : null);
        var contacts = new List<Renderer>();
        if (pu != null)
            foreach (var r in pu.GetComponentsInChildren<Renderer>(true))
            {
                string n = r.name.ToLowerInvariant();
                if (n.Contains("contact") || n.Contains("pad") || n.Contains("pin") || n.Contains("socket") || n.Contains("probe")) contacts.Add(r);
            }
        Add(root, "Tester_to_ProvingUnit", tester, prove, PlacementHint.Mode.Marker, PlacementHint.Condition.Always, yellow,
            new Color(1f, 0.85f, 0.1f, 0.5f), 0.25f, "TOUCH TO PROVE THE TESTER", contacts.ToArray(), false, log);

        Dirty();
        Debug.Log("[PolishV6] 2: placement hints\n" + log + "Test-point parts glowing: " + terms.Count + ", proving-unit parts glowing: " + contacts.Count);
    }

    static void Add(GameObject root, string name, XRGrabInteractable item, Transform target, PlacementHint.Mode mode,
                    PlacementHint.Condition cond, Material mat, Color c, float placeRadius, string text, Renderer[] glow, bool startPose, StringBuilder log)
    {
        if (item == null || target == null) { log.AppendLine("SKIPPED " + name + " (item or target not found)"); return; }
        var go = new GameObject(name);
        go.transform.SetParent(root.transform, false);
        var h = go.AddComponent<PlacementHint>();
        h.item = item; h.target = target; h.mode = mode; h.condition = cond; h.hintMaterial = mat; h.color = c;
        h.placeRadius = placeRadius; h.closeText = text; h.glowParts = glow; h.ghostUsesItemStartPose = startPose;
        log.AppendLine("OK " + name + ": " + item.name + " -> " + target.name);
    }

    static XRGrabInteractable Grab(Transform t)
    {
        if (t == null) return null;
        var g = t.GetComponent<XRGrabInteractable>();
        if (g == null) g = t.GetComponentInParent<XRGrabInteractable>();
        if (g == null) g = t.GetComponentInChildren<XRGrabInteractable>();
        return g;
    }

    // ------------------------------------------------------------------ 3. safe isolation poster
    [MenuItem("Tools/Workshop/Polish v6/3. Proper Safe Isolation Poster")]
    static void Poster()
    {
        var oldBoard = FindAny("Poster_Board");
        var existing = GameObject.Find("SafeIsolationPoster");
        Vector3 pos = existing != null ? existing.transform.position : oldBoard != null ? oldBoard.transform.position : new Vector3(-3.94f, 1.45f, 0.6f);
        if (existing != null) Undo.DestroyObjectImmediate(existing);
        foreach (var n in new[] { "Poster_Board", "Poster_Header", "Poster_Title", "Poster_Steps" })
        {
            var t = FindAny(n);
            if (t != null && t.gameObject.activeSelf) { Undo.RecordObject(t.gameObject, "hide"); t.gameObject.SetActive(false); PrefabUtility.RecordPrefabInstancePropertyModifications(t.gameObject); }
        }

        // facing into the room, on the nearest wall
        Vector3 normal = Mathf.Abs(pos.x) > Mathf.Abs(pos.z) ? new Vector3(-Mathf.Sign(pos.x), 0, 0) : new Vector3(0, 0, -Mathf.Sign(pos.z));
        const float W = 0.62f, H = 0.88f;
        pos.y = 1.42f;
        // keep clear of the "PPE REQUIRED..." wall text above
        foreach (var t in Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None))
        {
            if (t.text == null || !t.text.ToUpperInvariant().Contains("PPE REQUIRED")) continue;
            var r = t.GetComponent<Renderer>();
            if (r == null) continue;
            var b = r.bounds;
            float top = pos.y + H / 2f;
            bool sideOverlap = Mathf.Abs(Vector3.Dot(b.center - pos, Vector3.Cross(Vector3.up, normal))) < (b.extents.magnitude + W / 2f);
            if (sideOverlap && top > b.min.y - 0.05f) pos.y = b.min.y - 0.06f - H / 2f;
        }
        pos.y = Mathf.Max(pos.y, 0.6f + H / 2f);

        var root = new GameObject("SafeIsolationPoster");
        Undo.RegisterCreatedObjectUndo(root, "poster");
        root.transform.SetPositionAndRotation(pos + normal * 0.012f, Quaternion.LookRotation(-normal, Vector3.up));

        var frame = Mat("Mat_PosterFrame", new Color(0.12f, 0.13f, 0.15f), false);
        var paper = Mat("Mat_PosterPaper", new Color(0.97f, 0.97f, 0.95f), true);
        var green = Mat("Mat_PosterGreen", new Color(0.1f, 0.48f, 0.22f), true);
        var yellow = Mat("Mat_PosterYellow", new Color(1f, 0.78f, 0.05f), true);
        var red = Mat("Mat_PosterRed", new Color(0.78f, 0.1f, 0.08f), true);

        Box("Frame", root.transform, V(0, 0, 0.004f), V(W + 0.03f, H + 0.03f, 0.008f), frame);
        Box("Paper", root.transform, V(0, 0, -0.001f), V(W, H, 0.002f), paper);
        Box("Header", root.transform, V(0, H / 2f - 0.075f, -0.003f), V(W, 0.15f, 0.002f), green);
        Label("Title", root.transform, V(0, H / 2f - 0.06f, -0.005f), "<b>SAFE ISOLATION</b>", 0.62f, V2(W - 0.06f, 0.07f), Color.white, TextAlignmentOptions.Center);
        Label("Sub", root.transform, V(0, H / 2f - 0.118f, -0.005f), "BEFORE YOU WORK ON ANY ELECTRICAL EQUIPMENT", 0.22f, V2(W - 0.06f, 0.03f), new Color(0.85f, 1f, 0.88f), TextAlignmentOptions.Center);

        string[,] steps =
        {
            { "WEAR YOUR PPE", "All 6 items, before you touch anything." },
            { "SWITCH OFF", "The correct isolator. Check the job card." },
            { "LOCK & TAG", "Your own lock and DANGER tag on it." },
            { "PROVE  TEST  PROVE", "Prove the tester, test for dead, prove again." },
            { "REPAIR", "Only when proven dead. Use the right tool." },
            { "RESTORE", "Tools clear, cover shut, lock off, switch on." },
        };
        float top0 = H / 2f - 0.2f, step = 0.098f;
        for (int i = 0; i < 6; i++)
        {
            float y = top0 - i * step;
            var disc = Cyl("Num" + (i + 1), root.transform, V(-W / 2f + 0.065f, y, -0.004f), V(0.06f, 0.001f, 0.06f), yellow);
            disc.transform.localEulerAngles = V(90, 0, 0);
            Label("NumText" + (i + 1), root.transform, V(-W / 2f + 0.065f, y, -0.006f), "<b>" + (i + 1) + "</b>", 0.42f, V2(0.06f, 0.06f), Color.black, TextAlignmentOptions.Center);
            Label("Step" + (i + 1), root.transform, V(0.045f, y, -0.005f),
                "<b>" + steps[i, 0] + "</b>\n<size=72%><color=#444444>" + steps[i, 1] + "</color></size>", 0.32f, V2(W - 0.17f, 0.085f), new Color(0.1f, 0.1f, 0.1f), TextAlignmentOptions.Left);
        }
        Box("Footer", root.transform, V(0, -H / 2f + 0.045f, -0.003f), V(W, 0.09f, 0.002f), red);
        Label("FooterText", root.transform, V(0, -H / 2f + 0.045f, -0.005f), "<b>IF IN DOUBT - STOP AND ASK</b>", 0.36f, V2(W - 0.06f, 0.06f), Color.white, TextAlignmentOptions.Center);

        Dirty();
        Debug.Log("[PolishV6] 3: Safe Isolation poster at " + root.transform.position + " (old poster parts switched off).");
    }

    // ------------------------------------------------------------------ 4. spill kit + mop
    [MenuItem("Tools/Workshop/Polish v6/4. Proper Spill Kit + Mop Holder")]
    static void SpillKit()
    {
        var kit = GameObject.Find("HazardControls/SpillKit");
        var mop = GameObject.Find("HazardControls/Mop");
        if (kit == null || mop == null) { Debug.LogWarning("[PolishV6] 4: HazardControls/SpillKit or Mop not found."); return; }

        Vector3 basePos = kit.transform.position;
        // keep the wall sign + pads, rebuild the bucket
        foreach (var n in new[] { "Bucket", "BucketWater", "Wringer", "PadsBox", "PadsLabel", "MopBucket", "MopHolder" })
        {
            var t = kit.transform.Find(n);
            if (t != null) Undo.DestroyObjectImmediate(t.gameObject);
        }

        var body = Mat("Mat_MopBucket", new Color(1f, 0.74f, 0.05f), false);
        var dark = Mat("Mat_MopBucketDark", new Color(0.15f, 0.16f, 0.17f), false);
        var water = Mat("Mat_BucketWater", new Color(0.22f, 0.3f, 0.33f), false);
        var steel = Mat("Mat_Steel", new Color(0.72f, 0.74f, 0.76f), false);

        // janitor mop bucket: low wide tub on castors, wringer press, handle
        var b = new GameObject("MopBucket");
        Undo.RegisterCreatedObjectUndo(b, "bucket");
        b.transform.SetParent(kit.transform, false);
        b.transform.localPosition = V(0.08f, 0, 0.04f);
        Box("Tub", b.transform, V(0, 0.2f, 0), V(0.42f, 0.28f, 0.3f), body);
        Box("TubLip", b.transform, V(0, 0.345f, 0), V(0.44f, 0.02f, 0.32f), body);
        Box("Water", b.transform, V(0.05f, 0.33f, 0), V(0.28f, 0.005f, 0.26f), water);
        Box("WringerBody", b.transform, V(-0.13f, 0.42f, 0), V(0.14f, 0.14f, 0.26f), dark);
        Box("WringerPlate", b.transform, V(-0.13f, 0.5f, 0), V(0.16f, 0.02f, 0.28f), steel);
        var lever = Box("WringerLever", b.transform, V(-0.2f, 0.66f, 0), V(0.025f, 0.32f, 0.025f), dark);
        lever.transform.localEulerAngles = V(0, 0, -12f);
        Box("BaseTray", b.transform, V(0, 0.06f, 0), V(0.44f, 0.03f, 0.32f), dark);
        foreach (var x in new[] { -0.18f, 0.18f })
            foreach (var z in new[] { -0.12f, 0.12f })
            {
                var w = Cyl("Castor", b.transform, V(x, 0.025f, z), V(0.05f, 0.012f, 0.05f), dark);
                w.transform.localEulerAngles = V(0, 0, 90);
            }

        // wall clip (between the bucket and the door) that holds the mop upright, head along the wall
        var holder = new GameObject("MopHolder");
        Undo.RegisterCreatedObjectUndo(holder, "holder");
        holder.transform.SetParent(kit.transform, false);
        holder.transform.localPosition = V(-0.27f, 0, -0.05f);
        Box("WallPlate", holder.transform, V(0, 1.12f, -0.2f), V(0.08f, 0.12f, 0.01f), dark);
        Box("Arm", holder.transform, V(0, 1.12f, -0.1f), V(0.03f, 0.03f, 0.2f), dark);
        Box("Clip", holder.transform, V(0, 1.12f, 0f), V(0.07f, 0.035f, 0.05f), dark);

        Undo.RecordObject(mop.transform, "mop");
        mop.transform.SetPositionAndRotation(holder.transform.position + V(0, 0.015f, 0), Quaternion.Euler(0, 90, 0));

        Dirty();
        Debug.Log("[PolishV6] 4: spill kit rebuilt (wheeled mop bucket + wringer, mop stands in a wall clip). Let go of the mop and it returns to the clip.");
    }

    // ------------------------------------------------------------------ 5. welcome board text in the Scene view
    [MenuItem("Tools/Workshop/Polish v6/5. Show New Welcome Text in the Scene View")]
    static void WelcomeInScene()
    {
        var flow = Object.FindFirstObjectByType<TrainingFlow>();
        var m = Object.FindFirstObjectByType<ElectricalSafetyManager>();
        var s = Object.FindFirstObjectByType<SessionManager>();
        if (flow == null) { Debug.LogWarning("[PolishV6] 5: TrainingFlow not found."); return; }
        var main = flow.boardText;
        if (main == null) { var g = GameObject.Find("WelcomeCanvas/Text (TMP)"); if (g != null) main = g.GetComponent<TMP_Text>(); }
        int ppe = m != null ? m.requiredPpeItems : 6;
        int secs = s != null ? Mathf.RoundToInt(s.preCheckSeconds) : 120;
        if (main != null)
        {
            Undo.RecordObject(main, "welcome");
            TrainingFlow.StyleText(main, 22f, 46f);
            main.text = TrainingFlow.BuildWelcomeText(ppe, secs);
            EditorUtility.SetDirty(main);
        }
        if (flow.summaryText != null)
        {
            Undo.RecordObject(flow.summaryText, "summary");
            TrainingFlow.StyleText(flow.summaryText, 18f, 34f);
            flow.summaryText.text = TrainingFlow.BuildFirstSummary(ppe);
            EditorUtility.SetDirty(flow.summaryText);
        }
        Dirty();
        Debug.Log("[PolishV6] 5: welcome board text updated in the Scene view.");
    }

    // ================================================================== helpers
    static void Dirty() => EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

    static Transform Find(string name, string under)
    {
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
        {
            if (t.name != name) continue;
            if (under == null) return t;
            for (var p = t.parent; p != null; p = p.parent) if (p.name == under) return t;
        }
        return null;
    }

    static Transform FindAny(string name)
    {
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t.name == name) return t;
        return null;
    }

    static GameObject Prim(PrimitiveType type, string n, Transform p, Vector3 lp, Vector3 ls, Material m)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = n;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(p, false);
        go.transform.localPosition = lp;
        go.transform.localScale = ls;
        go.GetComponent<Renderer>().sharedMaterial = m;
        return go;
    }
    static GameObject Box(string n, Transform p, Vector3 lp, Vector3 ls, Material m) => Prim(PrimitiveType.Cube, n, p, lp, ls, m);
    static GameObject Cyl(string n, Transform p, Vector3 lp, Vector3 ls, Material m) => Prim(PrimitiveType.Cylinder, n, p, lp, ls, m);

    // TextMeshPro reads correctly from its local -z side (the room side).
    static TextMeshPro Label(string name, Transform parent, Vector3 lp, string text, float size, Vector2 rect, Color color, TextAlignmentOptions align)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = lp;
        var t = go.AddComponent<TextMeshPro>();
        t.text = text; t.fontSize = size; t.enableAutoSizing = true; t.fontSizeMin = size * 0.35f; t.fontSizeMax = size;
        t.alignment = align; t.textWrappingMode = TextWrappingModes.Normal; t.color = color;
        t.rectTransform.sizeDelta = rect;
        return t;
    }

    static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);
    static Vector2 V2(float x, float y) => new Vector2(x, y);

    static Material FindMat(string name)
    {
        foreach (var g in AssetDatabase.FindAssets(name + " t:Material"))
        {
            var p = AssetDatabase.GUIDToAssetPath(g);
            if (Path.GetFileNameWithoutExtension(p) == name) return AssetDatabase.LoadAssetAtPath<Material>(p);
        }
        return null;
    }

    static void EnsureFolder()
    {
        if (AssetDatabase.IsValidFolder(MatFolder)) return;
        var parts = MatFolder.Split('/'); string cur = parts[0];
        for (int i = 1; i < parts.Length; i++) { string nx = cur + "/" + parts[i]; if (!AssetDatabase.IsValidFolder(nx)) AssetDatabase.CreateFolder(cur, parts[i]); cur = nx; }
    }

    static Material Mat(string name, Color c, bool unlit)
    {
        var e = FindMat(name);
        if (e != null) return e;
        EnsureFolder();
        var m = new Material(Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit"));
        m.SetColor("_BaseColor", c);
        AssetDatabase.CreateAsset(m, MatFolder + "/" + name + ".mat");
        return m;
    }

    // transparent unlit (for ghosts / rings)
    static Material HintMat(string name, Color c)
    {
        var e = FindMat(name);
        if (e != null) return e;
        EnsureFolder();
        var m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        m.SetColor("_BaseColor", c);
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 0f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)RenderQueue.Transparent;
        AssetDatabase.CreateAsset(m, MatFolder + "/" + name + ".mat");
        return m;
    }
}
