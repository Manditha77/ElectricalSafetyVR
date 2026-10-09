using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Tools > Workshop > Fix > PPE Sign As Mandatory Sign
// Turns the plain "PPE REQUIRED BEYOND THIS POINT" wall text into a proper mandatory-action sign:
// blue board (safety-sign blue), white border, white bold text, same place on the wall.
// Ctrl+Z undoes it. Run it again = rebuilt, not duplicated.
public static class PpeSignFix
{
    const string BoardName = "PPESign_Board";

    [MenuItem("Tools/Workshop/Fix/PPE Sign As Mandatory Sign")]
    static void Run()
    {
        TMP_Text sign = null;
        foreach (var t in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t.text != null && t.text.ToUpperInvariant().Contains("PPE REQUIRED")) { sign = t; break; }
        if (sign == null)
        {
            EditorUtility.DisplayDialog("PPE Sign", "No text containing \"PPE REQUIRED\" found in the open scene.", "OK");
            return;
        }

        var old = GameObject.Find(BoardName);
        if (old != null) Undo.DestroyObjectImmediate(old);

        Undo.RecordObject(sign, "PPE sign");
        sign.text = "<size=55%>MANDATORY</size>\n<b>PPE REQUIRED BEYOND THIS POINT</b>";
        sign.color = Color.white;
        sign.alignment = TextAlignmentOptions.Center;
        sign.textWrappingMode = TextWrappingModes.NoWrap;
        if (PrefabUtility.IsPartOfPrefabInstance(sign)) PrefabUtility.RecordPrefabInstancePropertyModifications(sign);
        EditorUtility.SetDirty(sign);
        sign.ForceMeshUpdate();

        // size of the text on the wall, in metres
        Bounds b = sign.textBounds;
        Vector3 ls = sign.transform.lossyScale;
        float w = Mathf.Abs(b.size.x * ls.x), h = Mathf.Abs(b.size.y * ls.y);
        if (w < 0.05f || h < 0.02f) { w = 2.2f; h = 0.45f; }
        float pad = Mathf.Max(0.06f, h * 0.18f);
        Vector3 centre = sign.transform.TransformPoint(b.center);
        Vector3 back = sign.transform.forward;          // TMP is read from its -Z side, so +Z is towards the wall

        var board = new GameObject(BoardName);
        Undo.RegisterCreatedObjectUndo(board, "PPE sign");
        board.transform.SetPositionAndRotation(centre + back * 0.006f, sign.transform.rotation);

        var blue = Mat("Mat_SignMandatoryBlue", new Color(0f, 0.325f, 0.53f));
        var white = Mat("Mat_SignWhite", new Color(0.95f, 0.95f, 0.95f));
        Plate("Blue", board.transform, Vector3.zero, new Vector3(w + pad * 2f, h + pad * 2f, 0.004f), blue);
        Plate("Border", board.transform, back * 0.004f, new Vector3(w + pad * 2f + 0.05f, h + pad * 2f + 0.05f, 0.004f), white);

        EditorSceneManager.MarkSceneDirty(sign.gameObject.scene);
        Selection.activeGameObject = board;
        EditorUtility.DisplayDialog("PPE Sign", "PPE sign is now a blue mandatory sign with white text.\nCtrl+Z to undo. Save the scene (Ctrl+S).", "OK");
    }

    static void Plate(string n, Transform parent, Vector3 worldOffset, Vector3 size, Material m)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.name = n;
        go.transform.SetParent(parent, false);
        go.transform.position = parent.position + worldOffset;
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = m;
        go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    static Material Mat(string name, Color c)
    {
        const string folder = "Assets/_Project/B_Environment/Materials";
        string path = folder + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m != null) return m;
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/_Project/B_Environment", "Materials");
        m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.SetColor("_BaseColor", c);
        m.SetFloat("_Smoothness", 0.2f);
        AssetDatabase.CreateAsset(m, path);
        return m;
    }
}
