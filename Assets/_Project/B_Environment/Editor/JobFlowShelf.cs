using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Tools > Workshop > Job Flow > 1. Shelve My Job Interactions (keep environment)
//   - Backs up the scene + Workstation2 / TestEquipment / JobCard / ResultsBoard as prefabs
//   - Workstation2 + TestEquipment stay as plain environment (no grab, no triggers, no job scripts)
//   - JobCard + ResultsBoard are removed (teammates deliver their own)
//   - My job scripts move to _Backup_MyJobFlow and get their own namespace,
//     so they can never clash with Member C's scripts of the same name.
// Tools > Workshop > Job Flow > 2. Restore My Job Interactions (from backup)  (emergency only)
public static class JobFlowShelf
{
    const string Backup = "Assets/_Project/_Backup_MyJobFlow";
    const string NS = "MyJobFlowBackup";

    static readonly string[] KeepAsEnvironment = { "Workstation2", "TestEquipment" };
    static readonly string[] RemoveFromScene = { "JobCard", "ResultsBoard" };

    // my step 4-8 scripts + results/job card (only the copies in Assets/_Project/Shared are moved)
    static readonly HashSet<string> JobScripts = new HashSet<string>
    {
        "CoverHinge", "BlownFuse", "FuseHolder", "FusePullerTool", "ToolRack",
        "TesterProbe", "TestTerminal", "ProvingUnit", "JobCardBoard", "ResultsBoard"
    };
    // generic helpers: stripped from the two objects, but the files stay (used elsewhere)
    static readonly HashSet<string> StripAlso = new HashSet<string> { "SnapItem", "TrainingTool" };

    [MenuItem("Tools/Workshop/Job Flow/1. Shelve My Job Interactions (keep environment)")]
    static void Shelve()
    {
        var scene = SceneManager.GetActiveScene();
        if (!EditorUtility.DisplayDialog("Shelve my job interactions",
            "This will:\n\n" +
            "- back up the scene and Workstation2, TestEquipment, JobCard, ResultsBoard to " + Backup + "\n" +
            "- keep Workstation2 + TestEquipment as environment only (no interactions)\n" +
            "- remove JobCard + ResultsBoard from the scene\n" +
            "- move my job scripts to the backup folder (own namespace)\n\n" +
            "Run it BEFORE importing Member C's work. Continue?", "Shelve", "Cancel"))
            return;

        var report = new List<string>();
        EnsureFolder(Backup + "/Prefabs");
        EditorSceneManager.SaveScene(scene);
        string sceneCopy = Backup + "/" + scene.name + "_BeforeShelve.unity";
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(sceneCopy) == null)
        {
            AssetDatabase.CopyAsset(scene.path, sceneCopy);
            report.Add("Scene copy: " + sceneCopy);
        }

        foreach (var n in KeepAsEnvironment.Concat(RemoveFromScene))
        {
            var go = FindInScene(scene, n);
            if (go == null) { report.Add("Not found (skipped): " + n); continue; }
            SaveBackupPrefab(go, report);
        }

        foreach (var n in KeepAsEnvironment)
        {
            var go = FindInScene(scene, n);
            if (go != null) Strip(go, report);
        }

        foreach (var n in RemoveFromScene)
        {
            var go = FindInScene(scene, n);
            if (go != null) { Undo.DestroyObjectImmediate(go); report.Add("Removed from scene: " + n); }
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        MoveScripts(report);
        AssetDatabase.Refresh();
        Debug.Log("[JobFlowShelf] Done.\n- " + string.Join("\n- ", report));
    }

    [MenuItem("Tools/Workshop/Job Flow/2. Restore My Job Interactions (from backup)")]
    static void Restore()
    {
        if (!EditorUtility.DisplayDialog("Restore my job interactions",
            "Emergency only: brings back my interactive Workstation2, TestEquipment, JobCard and ResultsBoard " +
            "from the backup. The environment-only versions are disabled (renamed _EnvOnly). " +
            "Remove/disable Member C's step 4-8 objects yourself if both are active. Continue?", "Restore", "Cancel"))
            return;

        var scene = SceneManager.GetActiveScene();
        foreach (var n in KeepAsEnvironment.Concat(RemoveFromScene))
        {
            var pf = AssetDatabase.LoadAssetAtPath<GameObject>(Backup + "/Prefabs/" + n + ".prefab");
            if (pf == null) { Debug.LogWarning("[JobFlowShelf] No backup for " + n); continue; }
            var existing = FindInScene(scene, n);
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
        }
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("[JobFlowShelf] Restored my job interactions from " + Backup + "/Prefabs");
    }

    // ---------------------------------------------------------------

    static GameObject FindInScene(Scene scene, string name)
    {
        foreach (var root in scene.GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t.gameObject;
        return null;
    }

    static void SaveBackupPrefab(GameObject go, List<string> report)
    {
        string path = Backup + "/Prefabs/" + go.name + ".prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) { report.Add("Backup already exists (kept): " + path); return; }
        var clone = Object.Instantiate(go);
        clone.name = go.name;
        clone.transform.SetPositionAndRotation(go.transform.position, go.transform.rotation);
        PrefabUtility.SaveAsPrefabAsset(clone, path);
        Object.DestroyImmediate(clone);
        report.Add("Backup prefab: " + path);
    }

    static void Strip(GameObject root, List<string> report)
    {
        int triggers = 0, removed = 0;
        foreach (var c in root.GetComponentsInChildren<Collider>(true))
        {
            if (!c.isTrigger || !c.enabled) continue;
            Undo.RecordObject(c, "Disable trigger");
            c.enabled = false;
            triggers++;
        }

        for (int pass = 0; pass < 8; pass++)
        {
            bool any = false;
            foreach (var c in root.GetComponentsInChildren<Component>(true))
            {
                if (c == null || c is Transform || !ShouldRemove(c) || IsRequiredByOther(c)) continue;
                Undo.DestroyObjectImmediate(c);
                removed++;
                any = true;
            }
            if (!any) break;
        }
        report.Add(root.name + ": removed " + removed + " interaction components, disabled " + triggers + " trigger zones (meshes and solid colliders kept)");
    }

    static bool ShouldRemove(Component c)
    {
        var t = c.GetType();
        string ns = t.Namespace ?? "";
        return JobScripts.Contains(t.Name) || StripAlso.Contains(t.Name) || c is Rigidbody
               || ns.StartsWith("UnityEngine.XR.Interaction.Toolkit");
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

    static void MoveScripts(List<string> report)
    {
        EnsureFolder(Backup + "/Scripts");
        EnsureFolder(Backup + "/Editor");
        var moved = new List<string>();

        foreach (var guid in AssetDatabase.FindAssets("t:MonoScript", new[] { "Assets/_Project/Shared" }))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            string n = Path.GetFileNameWithoutExtension(p);
            if (!JobScripts.Contains(n) || !p.EndsWith(".cs")) continue;
            string dest = Backup + "/Scripts/" + n + ".cs";
            string err = AssetDatabase.MoveAsset(p, dest);
            if (!string.IsNullOrEmpty(err)) { report.Add("Move failed " + p + ": " + err); continue; }
            AddNamespace(dest);
            moved.Add(n);
            report.Add("Script shelved: " + dest);
        }

        foreach (var guid in AssetDatabase.FindAssets("JobSetup t:MonoScript"))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileName(p) != "JobSetup.cs" || p.StartsWith(Backup)) continue;
            string err = AssetDatabase.MoveAsset(p, Backup + "/Editor/JobSetup.cs");
            report.Add(string.IsNullOrEmpty(err) ? "Editor menu shelved: JobSetup.cs" : "Move failed JobSetup.cs: " + err);
        }

        if (moved.Count == 0) return;

        // Any remaining script that uses a shelved class gets "using MyJobFlowBackup;"
        var rx = new Regex(@"\b(" + string.Join("|", moved) + @")\b");
        foreach (var file in Directory.GetFiles("Assets", "*.cs", SearchOption.AllDirectories))
        {
            string f = file.Replace('\\', '/');
            if (f.EndsWith("/JobFlowShelf.cs") || f.StartsWith(Backup + "/Scripts/")) continue;
            string text = File.ReadAllText(f);
            if (!rx.IsMatch(text) || text.Contains("using " + NS + ";")) continue;
            File.WriteAllText(f, "using " + NS + ";\n" + text);
            report.Add("Added 'using " + NS + ";' to " + f);
        }
    }

    static void AddNamespace(string path)
    {
        var lines = File.ReadAllLines(path).ToList();
        if (lines.Any(l => l.TrimStart().StartsWith("namespace "))) return;
        int insert = 0;
        for (int i = 0; i < lines.Count; i++)
        {
            string t = lines[i].Trim();
            if (t.StartsWith("using ") && t.EndsWith(";")) insert = i + 1;
            else if (t.Length > 0 && !t.StartsWith("//") && !t.StartsWith("#")) break;
        }
        lines.Insert(insert, "namespace " + NS + " {");
        lines.Add("}");
        File.WriteAllLines(path, lines);
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
