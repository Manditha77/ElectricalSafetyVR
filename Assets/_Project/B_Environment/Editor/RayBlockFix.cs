using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// Tools > Workshop > Fix > Unblock Grabbing (PPE rack etc.)
// A solid box collider on furniture (e.g. the PPE rack) can swallow the items standing inside it,
// so the controller ray hits the rack instead of the gloves / goggles / ear defenders.
// This finds furniture colliders under the room objects that enclose an interactable and makes them
// triggers: rays pass through (XR rays ignore triggers), nothing else changes. Ctrl+Z undoes it.
public static class RayBlockFix
{
    [MenuItem("Tools/Workshop/Fix/Unblock Grabbing (PPE rack etc.)")]
    static void Run()
    {
        var items = new List<XRBaseInteractable>(Object.FindObjectsByType<XRBaseInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None));
        var log = new StringBuilder();
        int n = 0;

        foreach (var rootName in new[] { "PPE_Station", "PPEStation", "Workshop_Main", "WorkBench", "WorkshopRoom" })
        {
            var root = GameObject.Find(rootName);
            if (root == null) continue;
            foreach (var col in root.GetComponentsInChildren<Collider>(true))
            {
                if (col.isTrigger || col is CharacterController) continue;
                if (col.GetComponentInParent<XRBaseInteractable>(true) != null || col.GetComponentInParent<Rigidbody>(true) != null) continue;
                if (col.GetComponentInParent<PpeItem>(true) != null) continue;
                Bounds b = col.bounds;
                if (b.size.x * b.size.y * b.size.z > 3f) continue;            // room-sized: walls, floor
                Bounds inner = b; inner.Expand(-0.02f);

                string hit = null;
                foreach (var it in items)
                {
                    if (it == null) continue;
                    Vector3 c = Centre(it);
                    if (inner.Contains(c)) { hit = it.name; break; }
                }
                if (hit == null) continue;

                Undo.RecordObject(col, "Unblock grabbing");
                col.isTrigger = true;
                if (PrefabUtility.IsPartOfPrefabInstance(col)) PrefabUtility.RecordPrefabInstancePropertyModifications(col);
                n++;
                log.AppendLine("- " + Path(col.transform) + "  (was covering " + hit + ")");
            }
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        string msg = n == 0
            ? "No blocking colliders found.\nSend me a screenshot of PPE_Station expanded in the Hierarchy with the rack selected (Inspector visible)."
            : "Fixed " + n + " collider(s); rays now reach the items inside:\n" + log + "\nSave the scene (Ctrl+S) and test.";
        Debug.Log("[RayBlockFix] " + msg);
        EditorUtility.DisplayDialog("Unblock Grabbing", msg.Length > 1500 ? msg.Substring(0, 1500) + "\n..." : msg, "OK");
    }

    static Vector3 Centre(Component it)
    {
        var rs = it.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return it.transform.position;
        Bounds b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return b.center;
    }

    static string Path(Transform t)
    {
        string p = t.name;
        for (var q = t.parent; q != null; q = q.parent) p = q.name + "/" + p;
        return p;
    }
}
