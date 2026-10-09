using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Tools > Workshop > Fix > Restore Text Colours
// Every TextMeshPro text multiplies its own colour by the font material's "Face Color".
// If someone sets that Face Color to black, ALL text turns black and <color> tags stop working.
// This puts Face Color back to white on every TextMeshPro font material (text that is meant to be
// black stays black, because that is set on the text itself).
public static class FixTextColours
{
    [MenuItem("Tools/Workshop/Fix/Restore Text Colours")]
    static void Run()
    {
        var mats = new HashSet<Material>();

        foreach (var g in AssetDatabase.FindAssets("t:TMP_FontAsset"))
        {
            var fa = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(g));
            if (fa != null && fa.material != null) mats.Add(fa.material);
        }
        foreach (var g in AssetDatabase.FindAssets("t:Material"))
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(g));
            if (m != null && m.shader != null && m.shader.name.StartsWith("TextMeshPro")) mats.Add(m);
        }
        foreach (var t in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t.fontSharedMaterial != null && EditorUtility.IsPersistent(t.fontSharedMaterial)) mats.Add(t.fontSharedMaterial);

        var log = new StringBuilder();
        int fixedCount = 0;
        foreach (var m in mats)
        {
            if (!m.HasProperty("_FaceColor")) continue;
            Color c = m.GetColor("_FaceColor");
            if (c.r > 0.97f && c.g > 0.97f && c.b > 0.97f && c.a > 0.97f) continue;
            Undo.RecordObject(m, "Restore text colour");
            m.SetColor("_FaceColor", Color.white);
            EditorUtility.SetDirty(m);
            fixedCount++;
            log.AppendLine("FIXED " + AssetDatabase.GetAssetPath(m) + " : " + m.name + "  (Face Color was " + ColorUtility.ToHtmlStringRGBA(c) + ")");
        }
        AssetDatabase.SaveAssets();

        // scene texts: refresh so the change shows at once
        foreach (var t in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            t.SetAllDirty();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        string msg = fixedCount == 0
            ? "All TextMeshPro font materials already have a white Face Color.\nSend me a screenshot of one black text's Inspector (the material section at the bottom)."
            : "Restored " + fixedCount + " font material(s) to a white Face Color.\n\n" + log;
        Debug.Log("[FixTextColours]\n" + msg);
        EditorUtility.DisplayDialog("Restore Text Colours", msg.Length > 1500 ? msg.Substring(0, 1500) + "\n..." : msg, "OK");
    }
}
