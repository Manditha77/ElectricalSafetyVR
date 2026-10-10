using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// Tools > Workshop > Extras > 1. W2 Cover Sound
// Tools > Workshop > Extras > 2. Damaged Fuse Disposal Bin
// Tools > Workshop > Extras > 3. Fire at Damaged Lead + CO2 Extinguisher
// Tools > Workshop > Extras > Run All 3
// Each one can be run again (rebuilt, not duplicated) and undone with Ctrl+Z.
// Tools > Workshop > Extras > Remove > ...  takes each one (or all 3) out again, back to how it was before.
public static class ExtrasSetup
{
    const string MatFolder = "Assets/_Project/B_Environment/Materials";

    [MenuItem("Tools/Workshop/Extras/Run All 3")]
    static void All()
    {
        var log = new StringBuilder();
        CoverSoundSetup(log); BinSetup(log); FireSetup(log);
        Done(log);
    }

    [MenuItem("Tools/Workshop/Extras/1. W2 Cover Sound")]
    static void M1() { var l = new StringBuilder(); CoverSoundSetup(l); Done(l); }
    [MenuItem("Tools/Workshop/Extras/2. Damaged Fuse Disposal Bin")]
    static void M2() { var l = new StringBuilder(); BinSetup(l); Done(l); }
    [MenuItem("Tools/Workshop/Extras/3. Fire at Damaged Lead + CO2 Extinguisher")]
    static void M3() { var l = new StringBuilder(); FireSetup(l); Done(l); }

    static void Done(StringBuilder log)
    {
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[Extras]\n" + log);
        EditorUtility.DisplayDialog("Extras", log + "\nSave the scene (Ctrl+S) and press Play.", "OK");
    }

    // ------------------------------------------------------------------ 1. cover sound
    static void CoverSoundSetup(StringBuilder log)
    {
        Transform cover = ByType("CoverHinge");
        if (cover == null)
        {
            var w2 = FindAny("Workstation2");
            if (w2 != null) cover = w2.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.ToLowerInvariant().Contains("cover"));
        }
        Kill("W2_CoverSound");
        var go = new GameObject("W2_CoverSound");
        Undo.RegisterCreatedObjectUndo(go, "cover sound");
        if (cover != null) go.transform.position = cover.position;
        go.AddComponent<CoverSound>().cover = cover;
        log.AppendLine("Cover sound: " + (cover != null ? "listening to " + cover.name : "no cover transform found, using the safety manager's CoverOpen"));
    }

    // ------------------------------------------------------------------ 2. disposal bin
    static void BinSetup(StringBuilder log)
    {
        Transform blown = ByType("BlownFuse");
        if (blown == null) blown = FindAny("BlownFuse");
        if (blown == null) blown = FindAny("DamagedFuse");
        Transform slot = FindAny("FuseSlot");
        if (blown == null) { log.AppendLine("Disposal bin: blown fuse not found (no BlownFuse component or object). Skipped."); return; }
        var w2 = FindAny("Workstation2");
        Transform basis = w2 != null ? w2 : (slot != null ? slot : blown);
        Vector3 right = Vector3.ProjectOnPlane(basis.right, Vector3.up).normalized;
        Vector3 p = (slot != null ? slot.position : blown.position) + right * 0.8f; p.y = 0f;

        Kill("DamagedPartsBin");
        var root = new GameObject("DamagedPartsBin");
        Undo.RegisterCreatedObjectUndo(root, "bin");
        root.transform.SetPositionAndRotation(p, Quaternion.LookRotation(Vector3.ProjectOnPlane(basis.forward, Vector3.up).normalized, Vector3.up));

        var red = Lit("Mat_DisposalRed", new Color(0.7f, 0.08f, 0.06f), 0.4f);
        var dark = Lit("Mat_DarkSteel", new Color(0.2f, 0.21f, 0.22f), 0.5f);
        Box("Base", root.transform, new Vector3(0, 0.01f, 0), new Vector3(0.3f, 0.02f, 0.3f), dark);
        Box("Stand", root.transform, new Vector3(0, 0.41f, 0), new Vector3(0.05f, 0.8f, 0.05f), dark);
        Box("Bottom", root.transform, new Vector3(0, 0.815f, 0), new Vector3(0.3f, 0.01f, 0.3f), red);
        Box("WallF", root.transform, new Vector3(0, 0.9f, -0.145f), new Vector3(0.3f, 0.18f, 0.01f), red);
        Box("WallB", root.transform, new Vector3(0, 0.9f, 0.145f), new Vector3(0.3f, 0.18f, 0.01f), red);
        Box("WallL", root.transform, new Vector3(-0.145f, 0.9f, 0), new Vector3(0.01f, 0.18f, 0.3f), red);
        Box("WallR", root.transform, new Vector3(0.145f, 0.9f, 0), new Vector3(0.01f, 0.18f, 0.3f), red);
        var bc = root.AddComponent<BoxCollider>();
        bc.center = new Vector3(0, 0.5f, 0); bc.size = new Vector3(0.3f, 1f, 0.3f);

        FuseDisposalBin.Label(root.transform, new Vector3(0, 0.9f, -0.152f), 0f, "<b>DAMAGED\nPARTS</b>");
        FuseDisposalBin.Label(root.transform, new Vector3(0, 0.9f, 0.152f), 180f, "<b>DAMAGED\nPARTS</b>");
        FuseDisposalBin.Label(root.transform, new Vector3(-0.152f, 0.9f, 0), 90f, "<b>DAMAGED\nPARTS</b>");
        FuseDisposalBin.Label(root.transform, new Vector3(0.152f, 0.9f, 0), -90f, "<b>DAMAGED\nPARTS</b>");

        var drop = new GameObject("DropPoint");
        drop.transform.SetParent(root.transform, false);
        drop.transform.localPosition = new Vector3(0, 0.99f, 0);
        var lg = new GameObject("HintLight");
        lg.transform.SetParent(root.transform, false);
        lg.transform.localPosition = new Vector3(0, 1.15f, 0);
        var l = lg.AddComponent<Light>();
        l.type = LightType.Point; l.color = new Color(1f, 0.75f, 0.2f); l.range = 0.9f; l.shadows = LightShadows.None;

        var bin = root.AddComponent<FuseDisposalBin>();
        bin.blownFuse = blown; bin.dropPoint = drop.transform; bin.hintLight = l;
        Selection.activeGameObject = root;
        log.AppendLine("Disposal bin: built 0.8 m to the side of the fuse slot (move it if it overlaps anything). Blown fuse = " + blown.name);
    }

    // ------------------------------------------------------------------ 3. fire + extinguisher
    static void FireSetup(StringBuilder log)
    {
        Hazard cable = Object.FindObjectsByType<Hazard>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(h => h.name.StartsWith("Hazard_DamagedCable"));
        if (cable == null) { log.AppendLine("Fire: Hazard_DamagedCable not found. Skipped."); return; }
        PlugControl plug = null;
        var pg = GameObject.Find("HazardControls/Plug_DamagedLead");
        if (pg != null) plug = pg.GetComponent<PlugControl>();

        // where it burns: the middle of the damaged lead
        Vector3 at = cable.transform.position;
        var rs = cable.GetComponentsInChildren<Renderer>().Where(r => !(r is ParticleSystemRenderer)).ToArray();
        if (rs.Length > 0) { Bounds b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); at = b.center; at.y = b.min.y + 0.02f; }

        Kill("ElectricalFire_DamagedLead");
        var go = new GameObject("ElectricalFire_DamagedLead");
        Undo.RegisterCreatedObjectUndo(go, "fire");
        go.transform.position = at;
        var fire = go.AddComponent<ElectricalFire>();
        fire.hazard = cable;
        fire.plug = plug;
        fire.smokeMat = ParticleMat("Mat_Smoke", new Color(0.3f, 0.3f, 0.3f, 0.6f), false);
        fire.sparkMat = ParticleMat("Mat_Sparks", new Color(1f, 0.85f, 0.45f, 1f), true);
        fire.flameMat = fire.sparkMat;

        Undo.RecordObject(cable, "fire texts");
        cable.title = "DAMAGED LEAD - ON FIRE (LIVE)";
        cable.risk = "Bare conductors at 230 V are arcing: sparks, dark smoke and a fire starting. Touching the plug now risks shock and burns.";
        cable.control = "1. Put the fire out with the CO2 extinguisher (black band): grab it, aim, hold the trigger.\n2. Then pull its plug out of the wall socket.";
        cable.controlledText = "Fire out, unplugged and tagged DANGER - DO NOT USE. Taken out of service.";
        if (PrefabUtility.IsPartOfPrefabInstance(cable)) PrefabUtility.RecordPrefabInstancePropertyModifications(cable);
        log.AppendLine("Fire: at " + cable.name + (plug != null ? ", plug locked until the fire is out" : " (Plug_DamagedLead not found: plug not locked)"));

        // the extinguisher
        Transform ext = null;
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (!t.name.ToLowerInvariant().Contains("extinguisher") || Under(t, "HazardControls")) continue;
            var top = t;
            while (top.parent != null && top.parent.name.ToLowerInvariant().Contains("extinguisher")) top = top.parent;
            if (top.GetComponentsInChildren<Renderer>().Length > 0) { ext = top; break; }
        }
        if (ext == null) { log.AppendLine("Extinguisher: none found (name containing 'extinguisher'). Add one and run this again."); return; }

        foreach (var t in ext.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
        foreach (var mc in ext.GetComponentsInChildren<MeshCollider>(true)) { Undo.RecordObject(mc, "convex"); mc.convex = true; }
        if (ext.GetComponentsInChildren<Collider>(true).Length == 0)
        {
            var b = new Bounds(ext.position, Vector3.zero); bool first = true;
            foreach (var r in ext.GetComponentsInChildren<Renderer>()) { if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds); }
            var box = Undo.AddComponent<BoxCollider>(ext.gameObject);
            box.center = ext.InverseTransformPoint(b.center);
            Vector3 s = ext.lossyScale;
            box.size = new Vector3(b.size.x / Mathf.Abs(s.x), b.size.y / Mathf.Abs(s.y), b.size.z / Mathf.Abs(s.z));
        }
        var rb = ext.GetComponent<Rigidbody>();
        if (rb == null) rb = Undo.AddComponent<Rigidbody>(ext.gameObject);
        rb.mass = 6f; rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        var grab = ext.GetComponent<XRGrabInteractable>();
        if (grab == null) grab = Undo.AddComponent<XRGrabInteractable>(ext.gameObject);
        grab.movementType = XRBaseInteractable.MovementType.Kinematic;
        grab.useDynamicAttach = true;
        grab.throwOnDetach = false;
        var spray = ext.GetComponent<ExtinguisherSpray>();
        if (spray == null) spray = Undo.AddComponent<ExtinguisherSpray>(ext.gameObject);
        spray.sprayMat = ParticleMat("Mat_CO2Cloud", new Color(0.95f, 0.97f, 1f, 0.55f), false);
        foreach (var c in new Component[] { rb, grab, spray })
            if (PrefabUtility.IsPartOfPrefabInstance(c)) PrefabUtility.RecordPrefabInstancePropertyModifications(c);
        log.AppendLine("Extinguisher: " + ext.name + " is now grabbable; trigger sprays CO2");
    }

    // ================================================================== REMOVE (back to before the extras)
    [MenuItem("Tools/Workshop/Extras/Remove/Remove All 3 (back to before)")]
    static void RemoveAll()
    {
        var log = new StringBuilder();
        RemoveCover(log); RemoveBin(log); RemoveFire(log);
        Removed(log);
    }

    [MenuItem("Tools/Workshop/Extras/Remove/1. Remove Cover Sound")]
    static void R1() { var l = new StringBuilder(); RemoveCover(l); Removed(l); }
    [MenuItem("Tools/Workshop/Extras/Remove/2. Remove Disposal Bin")]
    static void R2() { var l = new StringBuilder(); RemoveBin(l); Removed(l); }
    [MenuItem("Tools/Workshop/Extras/Remove/3. Remove Fire + Extinguisher Grab")]
    static void R3() { var l = new StringBuilder(); RemoveFire(l); Removed(l); }

    static void Removed(StringBuilder log)
    {
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        string msg = log.Length == 0 ? "Nothing to remove: the extras are not in this scene." : log.ToString();
        Debug.Log("[Extras] Removed\n" + msg);
        EditorUtility.DisplayDialog("Extras removed", msg + "\nSave the scene (Ctrl+S). Everything else is unchanged.", "OK");
    }

    static void RemoveCover(StringBuilder log)
    {
        if (GameObject.Find("W2_CoverSound") != null) { Kill("W2_CoverSound"); log.AppendLine("Cover sound removed"); }
    }

    static void RemoveBin(StringBuilder log)
    {
        if (GameObject.Find("DamagedPartsBin") != null) { Kill("DamagedPartsBin"); log.AppendLine("Disposal bin removed (the fuse puller works as before)"); }
    }

    static void RemoveFire(StringBuilder log)
    {
        if (GameObject.Find("ElectricalFire_DamagedLead") != null) { Kill("ElectricalFire_DamagedLead"); log.AppendLine("Fire removed (plug can be pulled straight away again)"); }

        // the damaged-lead texts back to the original ones
        var cable = Object.FindObjectsByType<Hazard>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(h => h.name.StartsWith("Hazard_DamagedCable"));
        if (cable != null && cable.title == "DAMAGED LEAD - ON FIRE (LIVE)")
        {
            Undo.RecordObject(cable, "texts back");
            cable.title = "DAMAGED LEAD (LIVE)";
            cable.risk = "Bare conductors at 230 V: electric shock, sparks and fire. It is on a wall socket, so isolating W2 will NOT make it dead.";
            cable.control = "Pull its plug out of the wall socket.";
            cable.controlledText = "Unplugged and tagged DANGER - DO NOT USE. Taken out of service.";
            if (PrefabUtility.IsPartOfPrefabInstance(cable)) PrefabUtility.RecordPrefabInstancePropertyModifications(cable);
            log.AppendLine("Damaged lead texts restored");
        }

        // the extinguisher back to a fixed prop (spray, grab and rigidbody that menu 3 added)
        foreach (var spray in Object.FindObjectsByType<ExtinguisherSpray>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var go = spray.gameObject;
            var grab = go.GetComponent<XRGrabInteractable>();
            var rb = go.GetComponent<Rigidbody>();
            TryRemove(spray);
            if (grab != null) TryRemove(grab);
            if (rb != null) TryRemove(rb);
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic);
            log.AppendLine("Extinguisher " + go.name + " is a fixed prop again");
        }
    }

    static void TryRemove(Component c)
    {
        try { Undo.DestroyObjectImmediate(c); }
        catch (System.Exception e) { Debug.LogWarning("[Extras] Could not remove " + c.GetType().Name + " from " + c.name + ": " + e.Message); }
    }

    // ------------------------------------------------------------------ helpers
    static Transform ByType(string typeName)
    {
        foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (mb != null && mb.GetType().Name == typeName) return mb.transform;
        return null;
    }

    static Transform FindAny(string name)
    {
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t.name == name) return t;
        return null;
    }

    static bool Under(Transform t, string n)
    {
        for (var p = t.parent; p != null; p = p.parent) if (p.name == n) return true;
        return false;
    }

    static void Kill(string name)
    {
        var g = GameObject.Find(name);
        if (g != null) Undo.DestroyObjectImmediate(g);
    }

    static void Box(string n, Transform parent, Vector3 lp, Vector3 s, Material m)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.name = n;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = lp;
        go.transform.localScale = s;
        go.GetComponent<Renderer>().sharedMaterial = m;
    }

    static void Folder()
    {
        if (AssetDatabase.IsValidFolder(MatFolder)) return;
        var parts = MatFolder.Split('/'); string cur = parts[0];
        for (int i = 1; i < parts.Length; i++) { string nx = cur + "/" + parts[i]; if (!AssetDatabase.IsValidFolder(nx)) AssetDatabase.CreateFolder(cur, parts[i]); cur = nx; }
    }

    static Material Existing(string name)
    {
        foreach (var g in AssetDatabase.FindAssets(name + " t:Material"))
        {
            var p = AssetDatabase.GUIDToAssetPath(g);
            if (System.IO.Path.GetFileNameWithoutExtension(p) == name) return AssetDatabase.LoadAssetAtPath<Material>(p);
        }
        return null;
    }

    static Material Lit(string name, Color c, float smooth)
    {
        var e = Existing(name); if (e != null) return e;
        Folder();
        var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", smooth);
        AssetDatabase.CreateAsset(m, MatFolder + "/" + name + ".mat");
        return m;
    }

    static Material ParticleMat(string name, Color c, bool additive)
    {
        var e = Existing(name); if (e != null) return e;
        Folder();
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
}
