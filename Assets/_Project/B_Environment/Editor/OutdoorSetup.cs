using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

// OUTDOOR YARD - optional, two menus, either can be used at any time:
//   Tools > Workshop > Outdoor > Add Outdoor Yard + Night Sky
//   Tools > Workshop > Outdoor > Remove Outdoor (room only)
// Add:    door opening in the south wall, fenced yard, lamps, bench, assembly point, trees, hills,
//         moon + stars, crickets, night skybox (indoor lighting kept the same).
// Remove: deletes all of that, puts the south wall + doorway back, restores the skybox, sun and
//         ambient light, and takes "OutdoorArea" out of the blackout list.
//         Nothing inside the room (job steps, hazards, fuse fix, boards, sounds) is touched.
public static class OutdoorSetup
{
    const string MatFolder = "Assets/_Project/B_Environment/Materials";
    const string SnapshotPath = "Assets/_Project/B_Environment/Editor/OutdoorSnapshot.json";
    const float YardX = 9f, YardFarZ = -14f;

    // lighting before the yard was added
    [System.Serializable]
    class Snapshot
    {
        public string skyboxPath;      // "" = none, "builtin" = Unity default skybox
        public string sunPath;
        public int ambientMode;
        public Color ambientSky, ambientEquator, ambientGround, ambientLight;
        public float ambientIntensity;
    }

    [MenuItem("Tools/Workshop/Outdoor/Add Outdoor Yard + Night Sky")]
    static void Build()
    {
        if (GameObject.Find("OutdoorArea") == null) SaveSnapshot();   // room-only lighting, before any change
        Remove(false);
        var log = new System.Text.StringBuilder();

        // ---------- the south wall + door opening ----------
        var wall = FindAny("Wall_South");
        if (wall == null) { EditorUtility.DisplayDialog("Outdoor", "Wall_South not found.", "OK"); return; }
        var wr = wall.GetComponent<Renderer>();
        Bounds wb = wr.bounds;

        Bounds door = new Bounds(new Vector3(-2.5f, 1.05f, wb.center.z), new Vector3(1.0f, 2.1f, 0.1f));
        var swing = Object.FindFirstObjectByType<SwingDoor>();
        if (swing != null && swing.parts != null && swing.parts.Length > 0 && swing.parts[0] != null)
        {
            var lr = swing.parts[0].GetComponent<Renderer>();
            if (lr != null) door = lr.bounds;
        }
        float hx0 = door.min.x - 0.01f, hx1 = door.max.x + 0.01f, hy1 = door.max.y + 0.01f;

        var wallGroup = new GameObject("SouthWallWithDoor");
        Undo.RegisterCreatedObjectUndo(wallGroup, "wall");
        var wallMats = wr.sharedMaterials;
        WallPiece("Left", wallGroup.transform, wb.min.x, hx0, wb.min.y, wb.max.y, wb, wallMats);
        WallPiece("Right", wallGroup.transform, hx1, wb.max.x, wb.min.y, wb.max.y, wb, wallMats);
        WallPiece("Top", wallGroup.transform, hx0, hx1, hy1, wb.max.y, wb, wallMats);
        Undo.RecordObject(wr, "hide wall"); wr.enabled = false; PrefabUtility.RecordPrefabInstancePropertyModifications(wr);
        foreach (var c in wall.GetComponents<Collider>()) { Undo.RecordObject(c, "wall col"); c.enabled = false; PrefabUtility.RecordPrefabInstancePropertyModifications(c); }
        var gap = FindAny("Doorway");
        if (gap != null) { Undo.RecordObject(gap.gameObject, "doorway"); gap.gameObject.SetActive(false); }
        log.AppendLine("Door opening " + (hx1 - hx0).ToString("0.00") + " x " + hy1.ToString("0.00") + " m in the south wall");

        float outZ = wb.min.z;        // outside face of the wall
        float doorX = door.center.x;

        // ---------- materials ----------
        var mGrass = Mat("Mat_Grass", new Color(0.13f, 0.22f, 0.1f), 0.15f);
        var mFar = Mat("Mat_GrassFar", new Color(0.07f, 0.12f, 0.06f), 0.1f);
        var mConcrete = Mat("Mat_YardConcrete", new Color(0.42f, 0.42f, 0.41f), 0.25f);
        var mGravel = Mat("Mat_Gravel", new Color(0.33f, 0.31f, 0.28f), 0.1f);
        var mWood = Mat("Mat_FenceWood", new Color(0.32f, 0.22f, 0.14f), 0.2f);
        var mBark = Mat("Mat_Bark", new Color(0.22f, 0.15f, 0.1f), 0.1f);
        var mLeaf = Mat("Mat_Leaves", new Color(0.08f, 0.2f, 0.08f), 0.15f);
        var mHill = Mat("Mat_Hills", new Color(0.04f, 0.07f, 0.06f), 0.05f);
        var mMetal = Mat("Mat_LampMetal", new Color(0.18f, 0.19f, 0.2f), 0.5f);
        var mGreenSign = Mat("Mat_AssemblyGreen", new Color(0.05f, 0.5f, 0.22f), 0.3f);
        var mLampGlow = Unlit("Mat_LampGlow", new Color(1f, 0.85f, 0.55f));
        var mMoon = Unlit("Mat_Moon", new Color(0.95f, 0.95f, 0.88f));
        var mStars = StarMat();

        var root = new GameObject("OutdoorArea");
        Undo.RegisterCreatedObjectUndo(root, "outdoor");
        var R = root.transform;

        // ---------- ground ----------
        float yardDepth = outZ - YardFarZ;
        var far = Box("FarGround", R, new Vector3(0, -0.06f, outZ - 30f), new Vector3(140f, 0.1f, 120f), mFar, false);
        var yard = Box("YardGround", R, new Vector3(0, -0.05f, outZ - yardDepth / 2f), new Vector3(YardX * 2f, 0.1f, yardDepth), mGrass, true);
        var apron = Box("ConcreteApron", R, new Vector3(doorX, -0.04f, outZ - 1.6f), new Vector3(4f, 0.1f, 3.2f), mConcrete, false);
        var path = Box("GravelPath", R, new Vector3(doorX, -0.045f, (outZ - 3.2f + YardFarZ) / 2f), new Vector3(1.4f, 0.1f, (outZ - 3.2f) - YardFarZ), mGravel, false);
        Box("Step", R, new Vector3(doorX, 0.02f, outZ - 0.25f), new Vector3(1.4f, 0.04f, 0.5f), mConcrete, false);

        // teleport onto the yard like the indoor floor (same interaction layers)
        var indoorTp = Object.FindFirstObjectByType<TeleportationArea>();
        if (indoorTp != null)
        {
            var tp = yard.AddComponent<TeleportationArea>();
            tp.interactionLayers = indoorTp.interactionLayers;
            log.AppendLine("Yard is teleportable (same layers as the indoor floor)");
        }

        // ---------- fence + invisible walls ----------
        var fence = new GameObject("Fence"); fence.transform.SetParent(R, false);
        float bx0 = wb.min.x, bx1 = wb.max.x;
        FenceLine(fence.transform, new Vector3(-YardX, 0, outZ), new Vector3(-YardX, 0, YardFarZ), mWood);
        FenceLine(fence.transform, new Vector3(YardX, 0, outZ), new Vector3(YardX, 0, YardFarZ), mWood);
        FenceLine(fence.transform, new Vector3(-YardX, 0, YardFarZ), new Vector3(doorX - 1.2f, 0, YardFarZ), mWood);
        FenceLine(fence.transform, new Vector3(doorX + 1.2f, 0, YardFarZ), new Vector3(YardX, 0, YardFarZ), mWood);
        FenceLine(fence.transform, new Vector3(-YardX, 0, outZ - 0.05f), new Vector3(bx0, 0, outZ - 0.05f), mWood);
        FenceLine(fence.transform, new Vector3(bx1, 0, outZ - 0.05f), new Vector3(YardX, 0, outZ - 0.05f), mWood);
        // closed gate at the end of the path
        var gate = new GameObject("Gate"); gate.transform.SetParent(fence.transform, false);
        for (int i = 0; i < 9; i++)
            Box("GateBar", gate.transform, new Vector3(doorX - 1.1f + i * 0.275f, 0.6f, YardFarZ), new Vector3(0.05f, 1.15f, 0.05f), mMetal, false);
        Box("GateRailTop", gate.transform, new Vector3(doorX, 1.15f, YardFarZ), new Vector3(2.4f, 0.06f, 0.06f), mMetal, false);
        Box("GateRailLow", gate.transform, new Vector3(doorX, 0.15f, YardFarZ), new Vector3(2.4f, 0.06f, 0.06f), mMetal, false);
        // invisible boundary
        var walls = new GameObject("InvisibleBoundary"); walls.transform.SetParent(R, false);
        Wall(walls.transform, new Vector3(-YardX - 0.1f, 1.5f, (outZ + YardFarZ) / 2f), new Vector3(0.2f, 3f, yardDepth + 0.4f));
        Wall(walls.transform, new Vector3(YardX + 0.1f, 1.5f, (outZ + YardFarZ) / 2f), new Vector3(0.2f, 3f, yardDepth + 0.4f));
        Wall(walls.transform, new Vector3(0, 1.5f, YardFarZ - 0.1f), new Vector3(YardX * 2f + 0.4f, 3f, 0.2f));
        Wall(walls.transform, new Vector3((-YardX + bx0) / 2f, 1.5f, outZ - 0.05f), new Vector3(bx0 + YardX, 3f, 0.2f));
        Wall(walls.transform, new Vector3((bx1 + YardX) / 2f, 1.5f, outZ - 0.05f), new Vector3(YardX - bx1, 3f, 0.2f));

        // ---------- building front: sign + bulkhead light over the door ----------
        var front = new GameObject("BuildingFront"); front.transform.SetParent(R, false);
        var sign = Box("SignBoard", front.transform, new Vector3(doorX + 0.15f, wb.max.y - 0.35f, outZ - 0.03f), new Vector3(2.6f, 0.42f, 0.04f), mMetal, false);
        Label("SignText", front.transform, new Vector3(doorX + 0.15f, wb.max.y - 0.35f, outZ - 0.055f), Quaternion.identity,
              "<b>ELECTRICAL WORKSHOP</b>", 1.6f, new Vector2(2.5f, 0.36f), new Color(1f, 0.82f, 0.2f));
        Box("Bulkhead", front.transform, new Vector3(doorX, hy1 + 0.22f, outZ - 0.07f), new Vector3(0.3f, 0.14f, 0.1f), mMetal, false);
        Box("BulkheadGlass", front.transform, new Vector3(doorX, hy1 + 0.2f, outZ - 0.125f), new Vector3(0.24f, 0.08f, 0.01f), mLampGlow, false);
        SpotLight("DoorLight", front.transform, new Vector3(doorX, hy1 + 0.15f, outZ - 0.2f), Quaternion.Euler(50f, 180f, 0f), 7f, 70f, 2.2f);
        // exterior door trim
        Box("TrimL", front.transform, new Vector3(hx0 - 0.05f, hy1 / 2f, outZ - 0.02f), new Vector3(0.1f, hy1, 0.04f), mMetal, false);
        Box("TrimR", front.transform, new Vector3(hx1 + 0.05f, hy1 / 2f, outZ - 0.02f), new Vector3(0.1f, hy1, 0.04f), mMetal, false);
        Box("TrimTop", front.transform, new Vector3(doorX, hy1 + 0.05f, outZ - 0.02f), new Vector3(hx1 - hx0 + 0.2f, 0.1f, 0.04f), mMetal, false);

        // ---------- lamp posts along the path ----------
        foreach (float z in new[] { outZ - 4.5f, outZ - 9.5f })
            LampPost(R, new Vector3(doorX - 1.3f, 0, z), mMetal, mLampGlow);

        // ---------- bench + assembly point ----------
        var bench = new GameObject("Bench"); bench.transform.SetParent(R, false);
        bench.transform.position = new Vector3(doorX + 2.6f, 0, outZ - 6.5f);
        bench.transform.rotation = Quaternion.Euler(0, -90, 0);
        Box("Seat", bench.transform, new Vector3(0, 0.45f, 0), new Vector3(1.6f, 0.05f, 0.45f), mWood, true, true);
        Box("Back", bench.transform, new Vector3(0, 0.75f, 0.2f), new Vector3(1.6f, 0.35f, 0.05f), mWood, false, true);
        foreach (float x in new[] { -0.7f, 0.7f })
            Box("Leg", bench.transform, new Vector3(x, 0.22f, 0), new Vector3(0.06f, 0.44f, 0.4f), mMetal, false, true);

        var ap = new GameObject("AssemblyPoint"); ap.transform.SetParent(R, false);
        ap.transform.position = new Vector3(doorX + 4.5f, 0, outZ - 10.5f);
        ap.transform.rotation = Quaternion.Euler(0, 180, 0);   // sign faces the building / the path
        Box("Post", ap.transform, new Vector3(0, 1.0f, 0), new Vector3(0.08f, 2.0f, 0.08f), mMetal, true, true);
        Box("Board", ap.transform, new Vector3(0, 1.85f, -0.05f), new Vector3(0.8f, 0.6f, 0.03f), mGreenSign, false, true);
        Label("APText", ap.transform, new Vector3(0, 1.85f, -0.07f), Quaternion.identity,
              "<b>FIRE\nASSEMBLY\nPOINT</b>", 0.9f, new Vector2(0.7f, 0.5f), Color.white, true);

        // ---------- trees, hills ----------
        var trees = new GameObject("Trees"); trees.transform.SetParent(R, false);
        var rnd = new System.Random(11);
        var spots = new List<Vector3>
        {
            new Vector3(-7.3f, 0, outZ - 3.5f), new Vector3(7.4f, 0, outZ - 3.2f),
            new Vector3(-7.2f, 0, YardFarZ + 2.2f), new Vector3(7.0f, 0, YardFarZ + 2.5f)
        };
        for (int i = 0; i < 26; i++)
        {
            float a = i / 26f * Mathf.PI * 2f + (float)rnd.NextDouble() * 0.2f;
            float r = 15f + (float)rnd.NextDouble() * 9f;
            var p = new Vector3(Mathf.Cos(a) * r, 0, -8f + Mathf.Sin(a) * r);
            if (p.z > outZ - 1f && Mathf.Abs(p.x) < 6f) continue;   // not behind/into the building
            spots.Add(p);
        }
        foreach (var p in spots) Tree(trees.transform, p, 0.8f + (float)rnd.NextDouble() * 0.6f, mBark, mLeaf);

        var hills = new GameObject("Hills"); hills.transform.SetParent(R, false);
        Vector3[] hp = { new Vector3(-40, -4, -70), new Vector3(10, -6, -85), new Vector3(55, -5, -60), new Vector3(-70, -5, -20), new Vector3(75, -6, 10), new Vector3(-30, -6, 60), new Vector3(35, -6, 70) };
        foreach (var h in hp)
        {
            var s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.DestroyImmediate(s.GetComponent<Collider>());
            s.name = "Hill"; s.transform.SetParent(hills.transform, false);
            s.transform.position = h; s.transform.localScale = new Vector3(55f, 18f, 40f);
            s.GetComponent<Renderer>().sharedMaterial = mHill;
            s.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        // ---------- moon + stars ----------
        var sky = new GameObject("NightSky"); sky.transform.SetParent(R, false);
        var moon = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.DestroyImmediate(moon.GetComponent<Collider>());
        moon.name = "Moon"; moon.transform.SetParent(sky.transform, false);
        moon.transform.position = new Vector3(60f, 140f, -230f);
        moon.transform.localScale = Vector3.one * 14f;
        moon.GetComponent<Renderer>().sharedMaterial = mMoon;
        moon.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;

        var starsGo = new GameObject("Stars"); starsGo.transform.SetParent(sky.transform, false);
        var ps = starsGo.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = false; main.playOnAwake = false; main.maxParticles = 2000;
        main.startLifetime = 1e6f; main.startSpeed = 0f; main.simulationSpace = ParticleSystemSimulationSpace.World;
        var em = ps.emission; em.enabled = false;
        var shape = ps.shape; shape.enabled = false;
        var psr = starsGo.GetComponent<ParticleSystemRenderer>();
        psr.sharedMaterial = mStars; psr.renderMode = ParticleSystemRenderMode.Billboard;
        psr.shadowCastingMode = ShadowCastingMode.Off; psr.maxParticleSize = 0.02f;
        starsGo.AddComponent<StarField>();

        // ---------- crickets ----------
        var amb = new GameObject("NightAmbience"); amb.transform.SetParent(R, false);
        amb.transform.position = new Vector3(doorX, 1f, outZ - 7f);
        amb.AddComponent<AudioSource>().playOnAwake = false;
        amb.AddComponent<NightAmbience>();

        // ---------- night skybox, indoor ambient kept ----------
        KeepIndoorAmbient(log);
        var skyMat = NightSkyMat();
        if (skyMat != null) { RenderSettings.skybox = skyMat; RenderSettings.sun = null; log.AppendLine("Night sky set"); }

        // outdoor lights stay on in the indoor blackout
        var bc = Object.FindFirstObjectByType<BlackoutController>();
        if (bc != null)
        {
            var so = new SerializedObject(bc);
            var keep = so.FindProperty("keepUnder");
            if (keep != null)
            {
                bool has = false;
                for (int i = 0; i < keep.arraySize; i++) if (keep.GetArrayElementAtIndex(i).stringValue == "OutdoorArea") has = true;
                if (!has) { keep.InsertArrayElementAtIndex(keep.arraySize); keep.GetArrayElementAtIndex(keep.arraySize - 1).stringValue = "OutdoorArea"; so.ApplyModifiedProperties(); }
            }
        }

        EditorSceneManager.MarkSceneDirty(root.scene);
        Selection.activeGameObject = root;
        Debug.Log("[Outdoor] Built.\n" + log);
        EditorUtility.DisplayDialog("Outdoor", "Outdoor yard + night sky added.\n\n" + log + "\nSave the scene (Ctrl+S).", "OK");
    }

    [MenuItem("Tools/Workshop/Outdoor/Remove Outdoor (room only)")]
    static void RemoveMenu()
    {
        string log = Remove(true);
        Debug.Log("[Outdoor] Removed.\n" + log);
        EditorUtility.DisplayDialog("Outdoor", "Outdoor removed. The room is as it was.\n\n" + log + "\nSave the scene (Ctrl+S).", "OK");
    }

    static string Remove(bool fullRestore)
    {
        var log = new System.Text.StringBuilder();
        foreach (var n in new[] { "OutdoorArea", "SouthWallWithDoor" })
        {
            var g = GameObject.Find(n);
            if (g != null) { Undo.DestroyObjectImmediate(g); log.AppendLine("Deleted " + n); }
        }
        if (!fullRestore) return log.ToString();

        // south wall + doorway back
        var wall = FindAny("Wall_South");
        if (wall != null)
        {
            var r = wall.GetComponent<Renderer>();
            if (r != null) RestoreEnabled(r);
            foreach (var c in wall.GetComponents<Collider>()) RestoreEnabled(c);
            log.AppendLine("South wall restored");
        }
        var gap = FindAny("Doorway");
        if (gap != null && !gap.gameObject.activeSelf) { Undo.RecordObject(gap.gameObject, "doorway"); gap.gameObject.SetActive(true); log.AppendLine("Doorway restored"); }

        // blackout list
        var bc = Object.FindFirstObjectByType<BlackoutController>(FindObjectsInactive.Include);
        if (bc != null)
        {
            var so = new SerializedObject(bc);
            var keep = so.FindProperty("keepUnder");
            if (keep != null)
                for (int i = keep.arraySize - 1; i >= 0; i--)
                    if (keep.GetArrayElementAtIndex(i).stringValue == "OutdoorArea") { keep.DeleteArrayElementAtIndex(i); log.AppendLine("Blackout list cleaned"); }
            so.ApplyModifiedProperties();
        }

        // sky + ambient light
        var rso = GetRenderSettings();
        if (rso != null) Undo.RecordObject(rso, "lighting");
        var snap = LoadSnapshot();
        if (snap != null)
        {
            RenderSettings.skybox = snap.skyboxPath == "builtin" ? DefaultSkybox()
                                  : string.IsNullOrEmpty(snap.skyboxPath) ? null
                                  : AssetDatabase.LoadAssetAtPath<Material>(snap.skyboxPath);
            RenderSettings.ambientMode = (AmbientMode)snap.ambientMode;
            RenderSettings.ambientSkyColor = snap.ambientSky;
            RenderSettings.ambientEquatorColor = snap.ambientEquator;
            RenderSettings.ambientGroundColor = snap.ambientGround;
            RenderSettings.ambientLight = snap.ambientLight;
            RenderSettings.ambientIntensity = snap.ambientIntensity;
            var sun = string.IsNullOrEmpty(snap.sunPath) ? null : GameObject.Find(snap.sunPath);
            RenderSettings.sun = sun != null ? sun.GetComponent<Light>() : FindSun();
            log.AppendLine("Sky + ambient light restored to how they were before the yard");
        }
        else if (RenderSettings.skybox != null && RenderSettings.skybox.name == "Mat_NightSky")
        {
            // yard was added before snapshots existed: go back to Unity's defaults (the room keeps its own lights)
            RenderSettings.skybox = DefaultSkybox();
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.sun = FindSun();
            log.AppendLine("Night sky removed: default sky + skybox ambient light back");
        }
        DynamicGI.UpdateEnvironment();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        return log.ToString();
    }

    static void RestoreEnabled(Component c)
    {
        // clear the override if it is a prefab instance, otherwise just switch it back on
        if (PrefabUtility.IsPartOfPrefabInstance(c))
        {
            try { PrefabUtility.RevertObjectOverride(c, InteractionMode.UserAction); } catch { }
        }
        if (c is Renderer r && !r.enabled) { Undo.RecordObject(r, "wall"); r.enabled = true; }
        if (c is Collider col && !col.enabled) { Undo.RecordObject(col, "wall"); col.enabled = true; }
    }

    static Object GetRenderSettings()
    {
        var m = typeof(RenderSettings).GetMethod("GetRenderSettings", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        return m != null ? (Object)m.Invoke(null, null) : null;
    }

    static Material DefaultSkybox() => AssetDatabase.GetBuiltinExtraResource<Material>("Default-Skybox.mat");

    static Light FindSun()
    {
        Light best = null;
        foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (l.type == LightType.Directional && (best == null || l.intensity > best.intensity)) best = l;
        return best;
    }

    static string PathOf(Transform t)
    {
        string p = t.name;
        for (var q = t.parent; q != null; q = q.parent) p = q.name + "/" + p;
        return "/" + p;
    }

    static void SaveSnapshot()
    {
        var sb = RenderSettings.skybox;
        string skyPath = sb == null ? "" : AssetDatabase.GetAssetPath(sb);
        if (sb != null && !skyPath.StartsWith("Assets")) skyPath = "builtin";
        if (sb != null && sb.name == "Mat_NightSky") return;   // already night: keep the older snapshot
        var s = new Snapshot
        {
            skyboxPath = skyPath,
            sunPath = RenderSettings.sun != null ? PathOf(RenderSettings.sun.transform) : "",
            ambientMode = (int)RenderSettings.ambientMode,
            ambientSky = RenderSettings.ambientSkyColor,
            ambientEquator = RenderSettings.ambientEquatorColor,
            ambientGround = RenderSettings.ambientGroundColor,
            ambientLight = RenderSettings.ambientLight,
            ambientIntensity = RenderSettings.ambientIntensity
        };
        File.WriteAllText(SnapshotPath, JsonUtility.ToJson(s, true));
        AssetDatabase.ImportAsset(SnapshotPath);
    }

    static Snapshot LoadSnapshot()
    {
        if (!File.Exists(SnapshotPath)) return null;
        try { return JsonUtility.FromJson<Snapshot>(File.ReadAllText(SnapshotPath)); } catch { return null; }
    }

    // ================================================================== pieces

    static void WallPiece(string name, Transform parent, float x0, float x1, float y0, float y1, Bounds wb, Material[] mats)
    {
        if (x1 - x0 < 0.01f || y1 - y0 < 0.01f) return;
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "Wall_South_" + name;
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3((x0 + x1) / 2f, (y0 + y1) / 2f, wb.center.z);
        go.transform.localScale = new Vector3(x1 - x0, y1 - y0, wb.size.z);
        go.GetComponent<Renderer>().sharedMaterials = mats;
    }

    static void FenceLine(Transform parent, Vector3 a, Vector3 b, Material m)
    {
        float len = Vector3.Distance(a, b);
        if (len < 0.3f) return;
        int posts = Mathf.Max(2, Mathf.CeilToInt(len / 2f) + 1);
        Vector3 dir = (b - a).normalized;
        var line = new GameObject("FenceLine"); line.transform.SetParent(parent, false);
        for (int i = 0; i < posts; i++)
            Box("Post", line.transform, Vector3.Lerp(a, b, i / (posts - 1f)) + Vector3.up * 0.6f, new Vector3(0.1f, 1.2f, 0.1f), m, false);
        Quaternion rot = Quaternion.LookRotation(dir, Vector3.up);
        foreach (float y in new[] { 0.35f, 0.75f, 1.1f })
        {
            var rail = Box("Rail", line.transform, (a + b) / 2f + Vector3.up * y, new Vector3(0.04f, 0.09f, len), m, false);
            rail.transform.rotation = rot;
        }
    }

    static void Wall(Transform parent, Vector3 c, Vector3 size)
    {
        var go = new GameObject("Boundary");
        go.transform.SetParent(parent, false);
        go.transform.position = c;
        go.AddComponent<BoxCollider>().size = size;
    }

    static void LampPost(Transform parent, Vector3 p, Material metal, Material glow)
    {
        var lp = new GameObject("LampPost"); lp.transform.SetParent(parent, false); lp.transform.position = p;
        var pole = Cyl("Pole", lp.transform, new Vector3(0, 1.8f, 0), new Vector3(0.1f, 1.8f, 0.1f), metal);
        pole.AddComponent<CapsuleCollider>();
        Box("Arm", lp.transform, new Vector3(0.3f, 3.55f, 0), new Vector3(0.65f, 0.06f, 0.06f), metal, false, true);
        Box("Head", lp.transform, new Vector3(0.6f, 3.48f, 0), new Vector3(0.35f, 0.12f, 0.22f), metal, false, true);
        Box("Lens", lp.transform, new Vector3(0.6f, 3.415f, 0), new Vector3(0.28f, 0.01f, 0.16f), glow, false, true);
        SpotLight("Light", lp.transform, p + new Vector3(0.6f, 3.38f, 0), Quaternion.Euler(90, 0, 0), 9f, 85f, 3f);
    }

    static void Tree(Transform parent, Vector3 p, float s, Material bark, Material leaf)
    {
        var t = new GameObject("Tree"); t.transform.SetParent(parent, false); t.transform.position = p;
        t.transform.localScale = Vector3.one * s;
        var trunk = Cyl("Trunk", t.transform, new Vector3(0, 1.1f, 0), new Vector3(0.28f, 1.1f, 0.28f), bark);
        trunk.AddComponent<CapsuleCollider>();
        foreach (var (y, w, h) in new[] { (2.6f, 2.4f, 1.9f), (3.5f, 1.8f, 1.5f), (4.2f, 1.1f, 1.0f) })
        {
            var c = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.DestroyImmediate(c.GetComponent<Collider>());
            c.name = "Canopy"; c.transform.SetParent(t.transform, false);
            c.transform.localPosition = new Vector3(0, y, 0); c.transform.localScale = new Vector3(w, h, w);
            c.GetComponent<Renderer>().sharedMaterial = leaf;
        }
    }

    static void SpotLight(string name, Transform parent, Vector3 pos, Quaternion rot, float range, float angle, float intensity)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(pos, rot);
        var l = go.AddComponent<Light>();
        l.type = LightType.Spot; l.range = range; l.spotAngle = angle; l.innerSpotAngle = angle * 0.5f;
        l.intensity = intensity; l.color = new Color(1f, 0.82f, 0.58f); l.shadows = LightShadows.Hard;
    }

    static void KeepIndoorAmbient(System.Text.StringBuilder log)
    {
        if (RenderSettings.ambientMode != AmbientMode.Skybox) { log.AppendLine("Indoor ambient already fixed colours: unchanged"); return; }
        var dirs = new[] { Vector3.up, Vector3.forward, Vector3.down };
        var res = new Color[3];
        RenderSettings.ambientProbe.Evaluate(dirs, res);
        float br = res[0].maxColorComponent + res[1].maxColorComponent;
        if (br < 0.05f) { res[0] = new Color(0.52f, 0.54f, 0.58f); res[1] = new Color(0.42f, 0.42f, 0.42f); res[2] = new Color(0.26f, 0.24f, 0.22f); }
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = res[0];
        RenderSettings.ambientEquatorColor = res[1];
        RenderSettings.ambientGroundColor = res[2];
        log.AppendLine("Indoor ambient kept (switched from skybox to the same fixed colours)");
    }

    // ================================================================== helpers

    static Transform FindAny(string name)
    {
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t.name == name) return t;
        return null;
    }

    static GameObject Box(string n, Transform p, Vector3 worldOrLocal, Vector3 s, Material m, bool collider, bool local = false)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = n;
        if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(p, false);
        if (local) go.transform.localPosition = worldOrLocal; else go.transform.position = worldOrLocal;
        go.transform.localScale = s;
        go.GetComponent<Renderer>().sharedMaterial = m;
        return go;
    }

    static GameObject Cyl(string n, Transform p, Vector3 lp, Vector3 s, Material m)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = n;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(p, false);
        go.transform.localPosition = lp; go.transform.localScale = s;
        go.GetComponent<Renderer>().sharedMaterial = m;
        return go;
    }

    static void Label(string n, Transform p, Vector3 pos, Quaternion rot, string text, float size, Vector2 rect, Color c, bool local = false)
    {
        var go = new GameObject(n);
        go.transform.SetParent(p, false);
        if (local) go.transform.localPosition = pos; else go.transform.position = pos;
        go.transform.rotation = rot;
        var t = go.AddComponent<TextMeshPro>();
        t.text = text; t.fontSize = size; t.enableAutoSizing = true; t.fontSizeMin = size * 0.3f; t.fontSizeMax = size;
        t.alignment = TextAlignmentOptions.Center; t.color = c; t.rectTransform.sizeDelta = rect;
        t.textWrappingMode = TextWrappingModes.Normal;
    }

    static Material Find(string name)
    {
        foreach (var g in AssetDatabase.FindAssets(name + " t:Material"))
        {
            var p = AssetDatabase.GUIDToAssetPath(g);
            if (Path.GetFileNameWithoutExtension(p) == name) return AssetDatabase.LoadAssetAtPath<Material>(p);
        }
        return null;
    }

    static void Folder()
    {
        if (AssetDatabase.IsValidFolder(MatFolder)) return;
        var parts = MatFolder.Split('/'); string cur = parts[0];
        for (int i = 1; i < parts.Length; i++) { string nx = cur + "/" + parts[i]; if (!AssetDatabase.IsValidFolder(nx)) AssetDatabase.CreateFolder(cur, parts[i]); cur = nx; }
    }

    static Material Mat(string name, Color c, float smooth)
    {
        var e = Find(name); if (e != null) return e;
        Folder();
        var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", smooth);
        AssetDatabase.CreateAsset(m, MatFolder + "/" + name + ".mat");
        return m;
    }

    static Material Unlit(string name, Color c)
    {
        var e = Find(name); if (e != null) return e;
        Folder();
        var m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        m.SetColor("_BaseColor", c);
        AssetDatabase.CreateAsset(m, MatFolder + "/" + name + ".mat");
        return m;
    }

    static Material StarMat()
    {
        var e = Find("Mat_Stars"); if (e != null) return e;
        Folder();
        var baseMat = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/ParticlesUnlit.mat");
        var m = baseMat != null ? new Material(baseMat) : new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
        m.SetColor("_BaseColor", Color.white);
        m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 2f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)BlendMode.One); m.SetFloat("_ZWrite", 0f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)RenderQueue.Transparent;
        if (m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") == null)
            m.SetTexture("_BaseMap", AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Particle.psd"));
        AssetDatabase.CreateAsset(m, MatFolder + "/Mat_Stars.mat");
        return m;
    }

    static Material NightSkyMat()
    {
        var e = Find("Mat_NightSky"); if (e != null) return e;
        var sh = Shader.Find("Skybox/Procedural");
        if (sh == null) return null;
        Folder();
        var m = new Material(sh);
        m.SetFloat("_SunDisk", 0f);
        m.SetFloat("_SunSize", 0f);
        m.SetFloat("_AtmosphereThickness", 0.4f);
        m.SetColor("_SkyTint", new Color(0.12f, 0.16f, 0.32f));
        m.SetColor("_GroundColor", new Color(0.02f, 0.025f, 0.03f));
        m.SetFloat("_Exposure", 0.18f);
        AssetDatabase.CreateAsset(m, MatFolder + "/Mat_NightSky.mat");
        return m;
    }
}
