using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// Tools > Workshop > Job Flow > 0. Check Steps 4-8 (and fix)
// Finds out why the tester / proving unit / cover / fuse / puller can't be used, and fixes it.
// Works in Edit mode and in Play mode (Play mode also shows what is enabled right now).
public static class JobFlowCheck
{
    const string Backup = "Assets/_Project/_Backup_MyJobFlow";
    static readonly string[] JobRoots = { "Workstation2", "TestEquipment" };
    static readonly string[] Needed =
        { "CoverHinge", "BlownFuse", "FuseHolder", "FusePullerTool", "ToolRack", "TesterProbe", "TestTerminal", "ProvingUnit" };

    [MenuItem("Tools/Workshop/Job Flow/0. Check Steps 4-8 (and fix)")]
    static void Check()
    {
        var scene = SceneManager.GetActiveScene();
        var sb = new StringBuilder();
        var have = new HashSet<string>();
        int interactables = 0;

        foreach (var rootName in JobRoots)
        {
            var root = FindActive(scene, rootName);
            if (root == null) { sb.AppendLine("MISSING in scene (active): " + rootName); continue; }
            foreach (var c in root.GetComponentsInChildren<MonoBehaviour>(true))
                if (c != null) have.Add(c.GetType().Name);
            foreach (var it in root.GetComponentsInChildren<XRBaseInteractable>(true))
            {
                interactables++;
                bool tool = it.GetComponentInParent<TrainingTool>(true) != null;
                int cols = 0;
                foreach (var col in it.GetComponentsInChildren<Collider>(true)) if (col.enabled && !col.isTrigger) cols++;
                sb.AppendLine("  " + it.name + " (" + it.GetType().Name + "): " +
                              (it.enabled ? "enabled" : "disabled") + ", colliders " + cols +
                              (tool ? "" : ", NO TrainingTool") +
                              (it.gameObject.activeInHierarchy ? "" : ", OBJECT INACTIVE"));
            }
        }

        var missing = new List<string>();
        foreach (var n in Needed) if (!have.Contains(n)) missing.Add(n);

        // duplicate script files (a copy in the backup AND the original folder breaks things)
        foreach (var n in Needed)
        {
            var paths = new List<string>();
            foreach (var g in AssetDatabase.FindAssets(n + " t:MonoScript"))
            {
                var p = AssetDatabase.GUIDToAssetPath(g);
                if (System.IO.Path.GetFileNameWithoutExtension(p) == n) paths.Add(p);
            }
            if (paths.Count > 1) sb.AppendLine("DUPLICATE script " + n + ": " + string.Join(" | ", paths));
        }

        bool stripped = interactables == 0 || missing.Count >= 4;
        bool hasBackup = AssetDatabase.LoadAssetAtPath<GameObject>(Backup + "/Prefabs/Workstation2.prefab") != null &&
                         AssetDatabase.LoadAssetAtPath<GameObject>(Backup + "/Prefabs/TestEquipment.prefab") != null;

        string head = "Interactables on Workstation2 + TestEquipment: " + interactables +
                      "\nJob scripts missing: " + (missing.Count == 0 ? "none" : string.Join(", ", missing)) +
                      (Application.isPlaying ? "\nPhase now: " + HazardBridge.Phase + ", state: " + HazardBridge.State : "");
        Debug.Log("[JobFlowCheck]\n" + head + "\n" + sb);

        if (stripped)
        {
            if (!hasBackup)
            {
                EditorUtility.DisplayDialog("Steps 4-8", head + "\n\nThe job tools have no interactions and no backup was found.\nSend me the Console text from [JobFlowCheck].", "OK");
                return;
            }
            if (Application.isPlaying) { EditorUtility.DisplayDialog("Steps 4-8", head + "\n\nThe job tools were shelved (environment only). Stop Play mode and run this again to restore them.", "OK"); return; }
            if (EditorUtility.DisplayDialog("Steps 4-8 are switched off",
                head + "\n\nWorkstation2 + TestEquipment were 'shelved' earlier (environment only), so steps 4-8 can't be used.\n\nRestore MY interactive versions from the backup now?",
                "Restore", "Cancel"))
            {
                Restore(scene, "Workstation2");
                Restore(scene, "TestEquipment");
                if (EditorUtility.DisplayDialog("Job card + results board",
                    "Also bring back MY Job Card and Results Board?\n\nChoose 'No' if your teammates' job card / results board are already in the scene.",
                    "Yes, mine", "No"))
                {
                    Restore(scene, "JobCard");
                    Restore(scene, "ResultsBoard");
                }
                EditorSceneManager.MarkSceneDirty(scene);
                EditorUtility.DisplayDialog("Steps 4-8", "Restored. Save the scene (Ctrl+S) and test: isolate, lock, then grab the tester and touch it to the proving unit.", "OK");
            }
            return;
        }

        EditorUtility.DisplayDialog("Steps 4-8", head + "\n\nThe job tools are there. Details are in the Console ([JobFlowCheck]). " +
            "Run this again in Play mode during training and send me the Console text if a tool still can't be grabbed.", "OK");
    }

    static void Restore(Scene scene, string n)
    {
        var pf = AssetDatabase.LoadAssetAtPath<GameObject>(Backup + "/Prefabs/" + n + ".prefab");
        if (pf == null) { Debug.LogWarning("[JobFlowCheck] No backup for " + n); return; }
        var existing = FindActive(scene, n);
        if (existing != null)
        {
            Undo.RecordObject(existing, "Restore");
            existing.name = n + "_EnvOnly";
            existing.SetActive(false);
        }
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(pf, scene);
        PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        inst.name = n;
        Undo.RegisterCreatedObjectUndo(inst, "Restore");
        Debug.Log("[JobFlowCheck] Restored " + n + " (the environment-only copy is disabled as " + n + "_EnvOnly)");
    }

    static GameObject FindActive(Scene scene, string name)
    {
        foreach (var root in scene.GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Transform>(false))
                if (t.name == name) return t.gameObject;
        return null;
    }
}