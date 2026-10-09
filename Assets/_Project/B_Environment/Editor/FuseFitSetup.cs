using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// Tools > Workshop > Fix > New Fuse Fits Holder
// Adds FuseSnapAssist to NewFuse so releasing it at the green "RELEASE TO FIT" ring seats it in F2.
public static class FuseFitSetup
{
    [MenuItem("Tools/Workshop/Fix/New Fuse Fits Holder")]
    static void Run()
    {
        var log = new StringBuilder();
        XRGrabInteractable fuse = null;
        foreach (var g in Object.FindObjectsByType<XRGrabInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (g.name == "NewFuse") { fuse = g; break; }
        if (fuse == null)
        {
            var t = FindAny("NewFuse");
            if (t != null) fuse = t.GetComponentInParent<XRGrabInteractable>() ?? t.GetComponentInChildren<XRGrabInteractable>();
        }
        Transform slot = FindAny("FuseSlot");
        Transform blown = null;
        foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (mb != null && mb.GetType().Name == "BlownFuse") { blown = mb.transform; break; }
        if (blown == null) blown = FindAny("BlownFuse");

        if (fuse == null || slot == null)
        {
            EditorUtility.DisplayDialog("New Fuse Fits Holder",
                "Could not find " + (fuse == null ? "NewFuse (with XR Grab Interactable) " : "") + (slot == null ? "FuseSlot" : "") +
                ".\nSend me a screenshot of the Workstation2 hierarchy expanded.", "OK");
            return;
        }

        var a = fuse.GetComponent<FuseSnapAssist>();
        if (a == null) a = Undo.AddComponent<FuseSnapAssist>(fuse.gameObject);
        Undo.RecordObject(a, "Fuse fit");
        a.slot = slot;
        a.blownFuse = blown;
        a.fitRadius = 0.2f;
        a.needCoverOpen = true;
        if (PrefabUtility.IsPartOfPrefabInstance(a)) PrefabUtility.RecordPrefabInstancePropertyModifications(a);
        EditorUtility.SetDirty(a);
        log.AppendLine("NewFuse: FuseSnapAssist added (fit radius 0.2 m).");
        log.AppendLine("Seat pose from: " + (blown != null ? blown.name + " (where the blown fuse sits)" : "FuseSlot"));

        // make the hint say RELEASE only when it will really fit
        foreach (var h in Object.FindObjectsByType<PlacementHint>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (h.item != fuse) continue;
            Undo.RecordObject(h, "Fuse hint");
            h.placeRadius = 0.15f;
            EditorUtility.SetDirty(h);
            log.AppendLine("Hint " + h.name + ": RELEASE shown inside 0.15 m (fuse fits inside 0.2 m).");
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[FuseFitSetup]\n" + log);
        EditorUtility.DisplayDialog("New Fuse Fits Holder", log + "\nSave the scene (Ctrl+S) and press Play.", "OK");
    }

    static Transform FindAny(string name)
    {
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t.name == name) return t;
        return null;
    }
}
