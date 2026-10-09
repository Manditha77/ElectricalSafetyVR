using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// Tools > Workshop > Final > Set Up Final Flow (welcome board + results board)
// - pre-check time 120 s, review 15 s
// - welcome board: main text linked to TrainingFlow, text area made taller (same top edge)
// - builds the new Results board (old ResultsBoard + HazardReportBoard switched off, not deleted)
public static class FinalFlowSetup
{
    const string MatFolder = "Assets/_Project/B_Environment/Materials";

    [MenuItem("Tools/Workshop/Final/Set Up Final Flow (welcome + results boards)")]
    static void Run()
    {
        var log = new System.Text.StringBuilder();

        // ---- session timings ----
        var sm = Object.FindFirstObjectByType<SessionManager>();
        if (sm != null)
        {
            Undo.RecordObject(sm, "session");
            sm.preCheckSeconds = 120f;
            sm.reviewSeconds = 15f;
            sm.reviewMakeSafeAt = 5f;
            EditorUtility.SetDirty(sm);
            log.AppendLine("Pre-check 120 s, review 15 s");
        }

        // ---- welcome board ----
        var flow = Object.FindFirstObjectByType<TrainingFlow>();
        var main = GameObject.Find("WelcomeCanvas/Text (TMP)");
        if (flow != null && main != null)
        {
            var t = main.GetComponent<TMP_Text>();
            Undo.RecordObject(flow, "flow");
            flow.boardText = t;
            EditorUtility.SetDirty(flow);

            var rt = main.GetComponent<RectTransform>();
            if (flow.summaryText != null)
            {
                var top = Corners(rt, true);
                var sumTop = Corners(flow.summaryText.rectTransform, true);
                float bottomWanted = sumTop + 0.03f;
                float height = top - bottomWanted;
                float scale = rt.lossyScale.y;
                if (height > 0.1f && scale > 0f && height / scale > rt.sizeDelta.y)
                {
                    Undo.RecordObject(rt, "welcome text");
                    rt.sizeDelta = new Vector2(rt.sizeDelta.x, height / scale);
                    var p = rt.position;
                    rt.position = new Vector3(p.x, bottomWanted + rt.pivot.y * height, p.z);
                    log.AppendLine("Welcome text area: " + Mathf.RoundToInt(height * 100f) + " cm tall");
                }
            }
        }
        else log.AppendLine("WARN: TrainingFlow or WelcomeCanvas/Text (TMP) not found");

        // ---- results board ----
        var oldBoard = GameObject.Find("ResultsBoard");
        Vector3 pos = oldBoard != null ? oldBoard.transform.position : new Vector3(1.4f, 1.6f, -2.965f);
        Quaternion rot = oldBoard != null ? oldBoard.transform.rotation : Quaternion.Euler(0, 180, 0);
        if (oldBoard != null) { Undo.RecordObject(oldBoard, "old"); oldBoard.SetActive(false); log.AppendLine("Old ResultsBoard switched off"); }
        var report = GameObject.Find("HazardControls/HazardReportBoard");
        if (report != null) { Undo.RecordObject(report, "old"); report.SetActive(false); log.AppendLine("HazardReportBoard switched off (now part of the results)"); }

        var existing = GameObject.Find("TrainingResultsBoard");
        if (existing != null) Undo.DestroyObjectImmediate(existing);

        var bg = Mat("Mat_CalloutBg", new Color(0.06f, 0.07f, 0.09f), true);
        var bar = Mat("Mat_CalloutBar", Color.white, true);
        var yellow = Mat("Mat_SafetyYellow", new Color(1f, 0.78f, 0.05f), false);
        var green = Mat("Mat_ButtonGreen", new Color(0.15f, 0.55f, 0.25f), false);
        var grey = Mat("Mat_DarkSteel", new Color(0.25f, 0.27f, 0.3f), false);

        var root = new GameObject("TrainingResultsBoard");
        Undo.RegisterCreatedObjectUndo(root, "results");
        root.transform.SetPositionAndRotation(pos, rot);

        Box("Frame", root.transform, V(0, 0, 0.01f), V(1.34f, 1.08f, 0.02f), bg);
        Box("TopTrim", root.transform, V(0, 0.535f, 0.0f), V(1.34f, 0.012f, 0.024f), yellow);
        var header = Box("HeaderBar", root.transform, V(0, 0.47f, -0.002f), V(1.3f, 0.1f, 0.006f), bar);
        Label("Title", root.transform, V(0, 0.47f, -0.008f), "<b>TRAINING RESULTS</b>", 0.55f, V2(1.2f, 0.08f), Color.black, TextAlignmentOptions.Center);

        var waiting = new GameObject("Waiting");
        waiting.transform.SetParent(root.transform, false);
        Label("WaitingText", waiting.transform, V(0, 0.05f, -0.004f),
            "<color=#9AA4AE>Your results appear here when the job ends.\n\nThe job decides PASS or FAIL.\nYour pre-work check (PPE + hazards) is shown with it.</color>",
            0.42f, V2(1.15f, 0.6f), Color.white, TextAlignmentOptions.Center);

        var group = new GameObject("Results");
        group.transform.SetParent(root.transform, false);
        var grade = Label("Grade", group.transform, V(0, 0.36f, -0.004f), "PASS", 0.75f, V2(1.25f, 0.1f), Color.white, TextAlignmentOptions.Center);
        var summary = Label("Summary", group.transform, V(0, 0.285f, -0.004f), "", 0.36f, V2(1.25f, 0.06f), Color.white, TextAlignmentOptions.Center);
        Box("Divider", group.transform, V(0, 0.245f, -0.002f), V(1.22f, 0.004f, 0.004f), grey);
        var pre = Label("PreWork", group.transform, V(-0.315f, 0.06f, -0.004f), "", 0.34f, V2(0.6f, 0.36f), Color.white, TextAlignmentOptions.TopLeft);
        var job = Label("JobSteps", group.transform, V(0.33f, 0.06f, -0.004f), "", 0.34f, V2(0.6f, 0.36f), Color.white, TextAlignmentOptions.TopLeft);
        Box("Divider2", group.transform, V(0, -0.135f, -0.002f), V(1.22f, 0.004f, 0.004f), grey);
        var imp = Label("Improve", group.transform, V(0, -0.255f, -0.004f), "", 0.3f, V2(1.22f, 0.22f), Color.white, TextAlignmentOptions.TopLeft);

        var restart = Button3D("RestartTrainingButton", group.transform, V(-0.3f, -0.45f, -0.02f), green, "<b>RESTART TRAINING</b>", Color.white);
        var newbie = Button3D("NewTraineeButton", group.transform, V(0.3f, -0.45f, -0.02f), grey, "<b>NEW TRAINEE</b>", Color.white);

        var board = root.AddComponent<TrainingResultsBoard>();
        board.resultGroup = group; board.waitingGroup = waiting; board.headerBar = header.GetComponent<Renderer>();
        board.grade = grade; board.summary = summary; board.preWork = pre; board.jobSteps = job; board.improve = imp;
        board.restartTrainingButton = restart; board.newTraineeButton = newbie;
        group.SetActive(false);

        EditorSceneManager.MarkSceneDirty(root.scene);
        Selection.activeGameObject = root;
        log.AppendLine("Results board built at " + pos);
        Debug.Log("[FinalFlow]\n" + log);
    }

    // highest (top=true) world Y of a rect
    static float Corners(RectTransform rt, bool top)
    {
        var c = new Vector3[4];
        rt.GetWorldCorners(c);
        float v = top ? float.MinValue : float.MaxValue;
        foreach (var p in c) v = top ? Mathf.Max(v, p.y) : Mathf.Min(v, p.y);
        return v;
    }

    static XRSimpleInteractable Button3D(string name, Transform parent, Vector3 lp, Material m, string text, Color textColor)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = lp;
        go.transform.localScale = V(0.5f, 0.085f, 0.03f);
        go.GetComponent<Renderer>().sharedMaterial = m;
        var it = go.AddComponent<XRSimpleInteractable>();
        var lbl = Label(name + "Label", parent, lp + V(0, 0, -0.017f), text, 0.38f, V2(0.46f, 0.07f), textColor, TextAlignmentOptions.Center);
        lbl.name = name + "Label";
        return it;
    }

    static GameObject Box(string n, Transform p, Vector3 lp, Vector3 ls, Material m)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = n;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(p, false);
        go.transform.localPosition = lp;
        go.transform.localScale = ls;
        go.GetComponent<Renderer>().sharedMaterial = m;
        return go;
    }

    // TextMeshPro reads correctly from its local -z side (the room side of this board).
    static TextMeshPro Label(string name, Transform parent, Vector3 lp, string text, float size, Vector2 rect, Color color, TextAlignmentOptions align)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = lp;
        var t = go.AddComponent<TextMeshPro>();
        t.text = text;
        t.fontSize = size;
        t.enableAutoSizing = true;
        t.fontSizeMin = size * 0.35f;
        t.fontSizeMax = size;
        t.alignment = align;
        t.textWrappingMode = TextWrappingModes.Normal;
        t.color = color;
        t.rectTransform.sizeDelta = rect;
        return t;
    }

    static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);
    static Vector2 V2(float x, float y) => new Vector2(x, y);

    static Material Mat(string name, Color c, bool unlit)
    {
        foreach (var g in AssetDatabase.FindAssets(name + " t:Material"))
        {
            var p = AssetDatabase.GUIDToAssetPath(g);
            if (Path.GetFileNameWithoutExtension(p) == name) return AssetDatabase.LoadAssetAtPath<Material>(p);
        }
        if (!AssetDatabase.IsValidFolder(MatFolder))
        {
            var parts = MatFolder.Split('/'); string cur = parts[0];
            for (int i = 1; i < parts.Length; i++) { string nx = cur + "/" + parts[i]; if (!AssetDatabase.IsValidFolder(nx)) AssetDatabase.CreateFolder(cur, parts[i]); cur = nx; }
        }
        var m = new Material(Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit"));
        m.SetColor("_BaseColor", c);
        AssetDatabase.CreateAsset(m, MatFolder + "/" + name + ".mat");
        return m;
    }
}
