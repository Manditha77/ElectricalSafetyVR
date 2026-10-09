using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Tools > Workshop > Fix > Add Big Tester Readout
public static class TesterReadoutSetup
{
    [MenuItem("Tools/Workshop/Fix/Add Big Tester Readout")]
    static void Run()
    {
        GameObject tester = null;
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            if (t.name.Contains("VoltageTester") && t.GetComponentInChildren<TMP_Text>(true) != null) { tester = t.gameObject; break; }
        if (tester == null)
        {
            EditorUtility.DisplayDialog("Tester readout", "Could not find the VoltageTester (with its screen text) in the open scene.\nSelect the tester and send me a screenshot of its Hierarchy.", "OK");
            return;
        }

        var r = tester.GetComponent<TesterReadout>();
        if (r == null) r = Undo.AddComponent<TesterReadout>(tester);

        TMP_Text screen = null;
        foreach (var t in tester.GetComponentsInChildren<TMP_Text>(true))
            if (t.name.ToLowerInvariant().Contains("display") || t.name.ToLowerInvariant().Contains("screen")) { screen = t; break; }
        if (screen == null) screen = tester.GetComponentInChildren<TMP_Text>(true);

        Undo.RecordObject(r, "readout");
        r.source = screen;
        EditorUtility.SetDirty(r);
        EditorSceneManager.MarkSceneDirty(tester.scene);
        Selection.activeGameObject = tester;
        Debug.Log("[TesterReadout] Added to '" + tester.name + "', copying '" + screen.name + "'.");
    }
}
