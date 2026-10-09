using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Tools > Workshop > Hazards > Use Member C's Spanner Model (Assem 1_LR1)
// Puts a VISUAL COPY of her "Assem 1_LR1" inside HazardControls/Spanner.
// The hazard behaviour (spot -> pick up -> TOOL RETURN trolley) stays exactly the same.
// Her original object/model is never changed.
public static class SpannerModelSwap
{
    static readonly string[] ModelNames = { "Assem 1_LR1", "Assem1_LR1", "Assem 1_LR 1", "Assem_1_LR1" };
    const float TargetLength = 0.24f;   // a real 10-13 mm combination spanner is about 15-25 cm
    const string CopyName = "Model_Assem1_LR1";

    [MenuItem("Tools/Workshop/Hazards/Use Member C's Spanner Model (Assem 1_LR1)")]
    static void Swap()
    {
        var spanner = FindSceneObject("Spanner", requireParent: "HazardControls");
        if (spanner == null)
        {
            EditorUtility.DisplayDialog("Spanner swap", "HazardControls/Spanner not found.\nRun Tools > Workshop > Build Realistic Hazards first.", "OK");
            return;
        }

        GameObject source = null;
        bool fromAsset = false;
        foreach (var n in ModelNames) { source = FindSceneObject(n, null); if (source != null) break; }
        if (source == null)
        {
            foreach (var guid in AssetDatabase.FindAssets("Assem t:GameObject"))
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (go == null) continue;
                foreach (var n in ModelNames)
                    if (Normalize(go.name) == Normalize(n)) { source = go; fromAsset = true; break; }
                if (source != null) break;
            }
        }
        if (source == null)
        {
            EditorUtility.DisplayDialog("Spanner swap",
                "Could not find \"Assem 1_LR1\" in the open scene or in the Project.\n\n" +
                "Import Member C's files first. If the name is different, select her spanner in the Hierarchy and use\n" +
                "Tools > Workshop > Hazards > Use SELECTED Object as Spanner Model.", "OK");
            return;
        }
        DoSwap(spanner, source, fromAsset);
    }

    [MenuItem("Tools/Workshop/Hazards/Use SELECTED Object as Spanner Model")]
    static void SwapSelected()
    {
        var spanner = FindSceneObject("Spanner", requireParent: "HazardControls");
        var sel = Selection.activeGameObject;
        if (spanner == null || sel == null)
        {
            EditorUtility.DisplayDialog("Spanner swap", "Select her spanner model (scene object or model asset) first, and make sure HazardControls/Spanner exists.", "OK");
            return;
        }
        DoSwap(spanner, sel, EditorUtility.IsPersistent(sel));
    }

    [MenuItem("Tools/Workshop/Hazards/Undo Spanner Model (back to simple spanner)")]
    static void Revert()
    {
        var spanner = FindSceneObject("Spanner", requireParent: "HazardControls");
        if (spanner == null) return;
        var old = spanner.transform.Find(CopyName);
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
        foreach (Transform c in spanner.transform)
        {
            var r = c.GetComponent<Renderer>();
            if (r != null) { Undo.RecordObject(r, "Show"); r.enabled = true; }
        }
        var col = spanner.GetComponent<BoxCollider>();
        if (col != null) { Undo.RecordObject(col, "Collider"); col.center = new Vector3(0, 0.012f, 0); col.size = new Vector3(0.28f, 0.045f, 0.08f); }
        EditorSceneManager.MarkSceneDirty(spanner.scene);
    }

    // ------------------------------------------------------------------

    static void DoSwap(GameObject spanner, GameObject source, bool fromAsset)
    {
        var old = spanner.transform.Find(CopyName);
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

        // 1. visual copy
        GameObject copy;
        if (fromAsset)
        {
            copy = (GameObject)PrefabUtility.InstantiatePrefab(source, spanner.scene);
            if (copy == null) copy = Object.Instantiate(source);
            if (PrefabUtility.IsPartOfPrefabInstance(copy))
                PrefabUtility.UnpackPrefabInstance(copy, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        }
        else
        {
            copy = Object.Instantiate(source);
        }
        copy.name = CopyName;
        copy.SetActive(true);
        foreach (var t in copy.GetComponentsInChildren<Transform>(true)) t.gameObject.SetActive(true);
        StripToVisuals(copy);

        if (copy.GetComponentsInChildren<Renderer>(true).Length == 0)
        {
            Object.DestroyImmediate(copy);
            EditorUtility.DisplayDialog("Spanner swap", "\"" + source.name + "\" has no visible mesh.", "OK");
            return;
        }

        copy.transform.SetParent(spanner.transform, false);
        copy.transform.localPosition = Vector3.zero;
        copy.transform.localRotation = Quaternion.identity;
        copy.transform.localScale = Vector3.one;

        // 2. lie flat: longest side along spanner X, thinnest side up (Y)
        Vector3 s = LocalBounds(spanner.transform, copy).size;
        if (s.z > s.x && s.z >= s.y) copy.transform.localRotation = Quaternion.Euler(0, 90, 0) * copy.transform.localRotation;
        else if (s.y > s.x && s.y >= s.z) copy.transform.localRotation = Quaternion.Euler(0, 0, 90) * copy.transform.localRotation;
        s = LocalBounds(spanner.transform, copy).size;
        if (s.z < s.y) copy.transform.localRotation = Quaternion.Euler(90, 0, 0) * copy.transform.localRotation;

        // 3. real-world size
        Bounds b = LocalBounds(spanner.transform, copy);
        float len = Mathf.Max(b.size.x, 0.0001f);
        if (len < 0.12f || len > 0.4f) copy.transform.localScale *= TargetLength / len;

        // 4. sit on the workstation surface, centred
        b = LocalBounds(spanner.transform, copy);
        copy.transform.localPosition -= new Vector3(b.center.x, b.min.y, b.center.z);
        b = LocalBounds(spanner.transform, copy);

        // 5. hide the simple primitive spanner, fit grab collider + grip
        foreach (Transform c in spanner.transform)
        {
            if (c == copy.transform) continue;
            var r = c.GetComponent<Renderer>();
            if (r != null) { Undo.RecordObject(r, "Hide"); r.enabled = false; }
        }
        var col = spanner.GetComponent<BoxCollider>();
        if (col != null)
        {
            Undo.RecordObject(col, "Fit collider");
            col.center = b.center;
            col.size = new Vector3(Mathf.Max(b.size.x, 0.12f) + 0.02f, Mathf.Max(b.size.y, 0.03f) + 0.01f, Mathf.Max(b.size.z, 0.05f) + 0.02f);
        }
        var grip = spanner.transform.Find("Grip");
        if (grip != null)
        {
            Undo.RecordObject(grip, "Grip");
            grip.localPosition = new Vector3(b.center.x - b.size.x * 0.15f, b.center.y, b.center.z);
        }

        Undo.RegisterCreatedObjectUndo(copy, "Spanner model swap");
        EditorSceneManager.MarkSceneDirty(spanner.scene);
        Selection.activeGameObject = spanner;
        Debug.Log("[SpannerModelSwap] HazardControls/Spanner now uses a visual copy of \"" + source.name +
                  "\" (" + b.size.x.ToString("0.00") + " m long). Hazard behaviour unchanged; her original was not modified.");
    }

    // Keep only Transform + mesh rendering; remove colliders, rigidbodies, XR and her scripts from the COPY.
    static void StripToVisuals(GameObject root)
    {
        for (int pass = 0; pass < 8; pass++)
        {
            bool any = false;
            foreach (var c in root.GetComponentsInChildren<Component>(true))
            {
                if (c == null || c is Transform || c is MeshFilter || c is MeshRenderer || c is SkinnedMeshRenderer) continue;
                if (IsRequiredByOther(c)) continue;
                Object.DestroyImmediate(c);
                any = true;
            }
            if (!any) break;
        }
        foreach (var r in root.GetComponentsInChildren<Renderer>(true)) r.enabled = true;
    }

    static bool IsRequiredByOther(Component c)
    {
        foreach (var o in c.GetComponents<Component>())
        {
            if (o == null || o == c) continue;
            foreach (RequireComponent rc in o.GetType().GetCustomAttributes(typeof(RequireComponent), true))
                foreach (var rt in new[] { rc.m_Type0, rc.m_Type1, rc.m_Type2 })
                    if (rt != null && rt.IsAssignableFrom(c.GetType())) return true;
        }
        return false;
    }

    static Bounds LocalBounds(Transform space, GameObject go)
    {
        bool has = false;
        var b = new Bounds();
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            Bounds lb;
            Transform t = r.transform;
            if (r is SkinnedMeshRenderer sk && sk.sharedMesh != null) lb = sk.sharedMesh.bounds;
            else { var mf = r.GetComponent<MeshFilter>(); if (mf == null || mf.sharedMesh == null) continue; lb = mf.sharedMesh.bounds; }
            Vector3 e = lb.extents;
            for (int i = 0; i < 8; i++)
            {
                var corner = lb.center + new Vector3((i & 1) == 0 ? -e.x : e.x, (i & 2) == 0 ? -e.y : e.y, (i & 4) == 0 ? -e.z : e.z);
                var p = space.InverseTransformPoint(t.TransformPoint(corner));
                if (!has) { b = new Bounds(p, Vector3.zero); has = true; } else b.Encapsulate(p);
            }
        }
        return b;
    }

    static GameObject FindSceneObject(string name, string requireParent)
    {
        var scene = EditorSceneManager.GetActiveScene();
        string want = Normalize(name);
        foreach (var root in scene.GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (Normalize(t.name) != want) continue;
                if (requireParent != null && (t.parent == null || t.parent.name != requireParent)) continue;
                if (requireParent == null && IsUnder(t, "HazardControls")) continue;
                return t.gameObject;
            }
        return null;
    }

    static bool IsUnder(Transform t, string name)
    {
        for (var p = t.parent; p != null; p = p.parent) if (p.name == name) return true;
        return false;
    }

    static string Normalize(string s) => s.Replace(" ", "").Replace("_", "").ToLowerInvariant();
}
