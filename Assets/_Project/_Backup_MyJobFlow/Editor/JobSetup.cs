using MyJobFlowBackup;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// Builds the job equipment: Workstation 2, test equipment and tools, job card, results board.
public static class JobSetup
{
    const string MaterialFolder = "Assets/_Project/B_Environment/Materials";
    const string BreakerPrefab = "Assets/_Project/C_Interactions/Prefabs/BreakerPanel.prefab";

    static Material Machine  => Mat("Mat_Machine",      new Color32(70, 95, 120, 255),   0.45f);
    static Material Grey     => Mat("Mat_IsolatorGrey", new Color32(200, 200, 195, 255), 0.4f);
    static Material Black    => Mat("Mat_Black",        new Color32(20, 20, 25, 255),    0.8f);
    static Material White    => Mat("Mat_Poster",       new Color32(235, 235, 230, 255), 0.2f);
    static Material Yellow   => Mat("Mat_Warning",      new Color32(240, 200, 0, 255),   0.3f);
    static Material Red      => Mat("Mat_Danger",       new Color32(200, 30, 30, 255),   0.4f);
    static Material Metal    => Mat("Mat_Metal",        new Color32(130, 135, 140, 255), 0.6f);
    static Material Wood     => Mat("Mat_Bench",        new Color32(120, 85, 50, 255),   0.3f);
    static Material Charcoal => Mat("Mat_Steel",        new Color32(45, 52, 62, 255),    0.5f);
    static Material Burnt    => Mat("Mat_FuseBurnt",    new Color32(45, 35, 30, 255),    0.2f);
    static Material Ceramic  => Mat("Mat_Ceramic",      new Color32(240, 240, 232, 255), 0.5f);
    static Material Brass    => Mat("Mat_Brass",        new Color32(185, 145, 60, 255),  0.75f);
    static Material Orange   => Mat("Mat_Orange",       new Color32(240, 120, 20, 255),  0.4f);
    static Material Green    => Mat("Mat_ButtonGreen",  new Color32(40, 140, 65, 255),   0.4f);

    [MenuItem("Tools/Workshop/Build Job Equipment")]
    static void BuildAll()
    {
        if (Object.FindFirstObjectByType<SessionManager>() == null)
        {
            Debug.LogError("Open ElectricalWorkshop first.");
            return;
        }

        FuseHolder holder = BuildWorkstation();
        BuildBenchEquipment(holder);
        BuildJobCard();
        BuildResultsBoard();
        MoveMetalToolHazard();
        MarkLockoutTag();

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("Workshop: job equipment built. Save the scene (Ctrl+S).");
    }

    // ---------- Workstation 2 (west wall, at Anchor_Workstation) ----------

    static FuseHolder BuildWorkstation()
    {
        Transform t = Root("Workstation2", new Vector3(-3.425f, 0f, -1f), -90f);

        Part(PrimitiveType.Cube, t, "Body",   V(0f, 0.6f, 0.25f),  V(0.8f, 1.2f, 0.5f),   Machine, true);
        Part(PrimitiveType.Cube, t, "Plinth", V(0f, 0.04f, 0.24f), V(0.82f, 0.08f, 0.52f), Black);

        Part(PrimitiveType.Cube, t, "NamePlate", V(0f, 1.1f, -0.004f), V(0.42f, 0.08f, 0.006f), White);
        Text(t, "NameText", "WORKSTATION 2", V(0f, 1.1f, -0.009f), Vector3.zero, new Vector2(0.4f, 0.07f), 0.45f, Color.black);

        Part(PrimitiveType.Cube, t, "DangerPlate", V(0.27f, 0.93f, -0.004f), V(0.16f, 0.1f, 0.006f), Yellow);
        Text(t, "DangerText", "<b>DANGER\n230 V</b>", V(0.27f, 0.93f, -0.009f), Vector3.zero, new Vector2(0.15f, 0.09f), 0.28f, Color.black);

        GameObject lamp = Part(PrimitiveType.Sphere, t, "StatusLamp", V(-0.27f, 1.1f, -0.01f), V(0.05f, 0.05f, 0.05f), GlowMat("Mat_LampGlass"));
        Text(t, "LampText", "STATUS", V(-0.27f, 1.05f, -0.009f), Vector3.zero, new Vector2(0.15f, 0.04f), 0.18f, Color.white);
        t.gameObject.AddComponent<MachineLamp>().lamp = lamp.GetComponent<Renderer>();

        // Fuse compartment
        Part(PrimitiveType.Cube, t, "FuseRecess", V(0f, 0.75f, -0.004f), V(0.34f, 0.34f, 0.008f), Black);
        Part(PrimitiveType.Cube, t, "FuseBase",   V(0f, 0.75f, -0.015f), V(0.1f, 0.18f, 0.02f),   Ceramic);
        Part(PrimitiveType.Cube, t, "ClipTop",    V(0f, 0.81f, -0.03f),  V(0.04f, 0.02f, 0.012f), Brass);
        Part(PrimitiveType.Cube, t, "ClipBottom", V(0f, 0.69f, -0.03f),  V(0.04f, 0.02f, 0.012f), Brass);
        Part(PrimitiveType.Cube, t, "ScorchMark", V(0f, 0.835f, -0.026f), V(0.07f, 0.035f, 0.002f), Black);
        Text(t, "FuseLabel", "<b>F2</b>", V(0.11f, 0.88f, -0.009f), Vector3.zero, new Vector2(0.08f, 0.05f), 0.25f, Color.white);

        Transform slot = Empty(t, "FuseSlot", V(0f, 0.75f, -0.045f));

        GameObject blown = Part(PrimitiveType.Cylinder, t, "BlownFuse", slot.localPosition, V(0.03f, 0.05f, 0.03f), Burnt);
        BoxCollider blownZone = blown.AddComponent<BoxCollider>();
        blownZone.isTrigger = true;
        blownZone.size = new Vector3(2.5f, 1.6f, 2.5f);

        FuseHolder holder = t.gameObject.AddComponent<FuseHolder>();
        holder.slot = slot;
        blown.AddComponent<BlownFuse>().holder = holder;

        // Hinged cover over the fuse
        Transform hinge = Empty(t, "CoverHinge", V(-0.17f, 0.75f, -0.075f));
        GameObject cover = Part(PrimitiveType.Cube, hinge, "Cover", V(0.17f, 0f, 0f), V(0.34f, 0.34f, 0.012f), Grey, true);
        Part(PrimitiveType.Sphere, hinge, "Knob", V(0.31f, 0f, -0.01f), V(0.025f, 0.025f, 0.025f), Black);
        Part(PrimitiveType.Cube, hinge, "CoverStripe", V(0.17f, 0.13f, -0.007f), V(0.3f, 0.04f, 0.002f), Yellow);
        Text(hinge, "CoverText", "<b>FUSES</b>\nISOLATE AND PROVE DEAD\nBEFORE OPENING",
             V(0.17f, -0.03f, -0.008f), Vector3.zero, new Vector2(0.3f, 0.2f), 0.2f, new Color(0.55f, 0.05f, 0.05f));
        XRSimpleInteractable coverSelect = cover.AddComponent<XRSimpleInteractable>();
        cover.AddComponent<TrainingTool>();
        hinge.gameObject.AddComponent<CoverHinge>().handle = coverSelect;

        // Test terminals (outside the cover)
        Part(PrimitiveType.Cube, t, "TestLabel", V(0f, 0.5f, -0.004f), V(0.2f, 0.05f, 0.006f), Yellow);
        Text(t, "TestLabelText", "<b>TEST POINT</b>", V(0f, 0.5f, -0.009f), Vector3.zero, new Vector2(0.19f, 0.045f), 0.2f, Color.black);
        Part(PrimitiveType.Cube, t, "TestBlock", V(0f, 0.42f, -0.012f), V(0.16f, 0.08f, 0.024f), Black);
        Part(PrimitiveType.Cylinder, t, "TerminalL", V(-0.04f, 0.43f, -0.03f), V(0.02f, 0.012f, 0.02f), Brass, false, V(90f, 0f, 0f));
        Part(PrimitiveType.Cylinder, t, "TerminalN", V(0.04f, 0.43f, -0.03f),  V(0.02f, 0.012f, 0.02f), Brass, false, V(90f, 0f, 0f));
        Text(t, "TerminalText", "L          N", V(0f, 0.395f, -0.026f), Vector3.zero, new Vector2(0.15f, 0.03f), 0.15f, Color.white);
        Transform testZone = Empty(t, "TestZone", V(0f, 0.42f, -0.05f));
        BoxCollider tz = testZone.gameObject.AddComponent<BoxCollider>();
        tz.isTrigger = true;
        tz.size = new Vector3(0.18f, 0.12f, 0.1f);
        testZone.gameObject.AddComponent<TestTerminal>();

        return holder;
    }

    // ---------- bench: tool board with fuse puller, spare fuse, tester, proving unit ----------

    static void BuildBenchEquipment(FuseHolder holder)
    {
        Transform r = Root("TestEquipment", Vector3.zero, 0f);

        // Shadow board for tools
        Part(PrimitiveType.Cube, r, "ToolBoard", V(2.0f, 1.2f, 2.17f), V(0.7f, 0.5f, 0.02f), Wood, true);
        Part(PrimitiveType.Cube, r, "BoardLabel", V(2.0f, 1.39f, 2.158f), V(0.46f, 0.07f, 0.004f), White);
        Text(r, "BoardText", "<b>TOOL BOARD - RETURN TOOLS HERE</b>", V(2.0f, 1.39f, 2.155f), Vector3.zero, new Vector2(0.44f, 0.06f), 0.2f, Color.black);
        Part(PrimitiveType.Cube, r, "PullerShadow", V(2.15f, 1.17f, 2.159f), V(0.06f, 0.22f, 0.002f), White);
        Part(PrimitiveType.Cylinder, r, "Peg", V(2.15f, 1.29f, 2.14f), V(0.012f, 0.02f, 0.012f), Metal, false, V(90f, 0f, 0f));
        Transform pullerSlot = Empty(r, "PullerSlot", V(2.15f, 1.17f, 2.13f));

        // Fuse puller
        GameObject puller = Grabbable(r, "FusePuller", pullerSlot.position, 0.15f, new Vector3(0.04f, 0.22f, 0.03f));
        Part(PrimitiveType.Cube, puller.transform, "Handle", V(0f, 0.04f, 0f),    V(0.035f, 0.12f, 0.025f), Orange);
        Part(PrimitiveType.Cube, puller.transform, "JawL",   V(-0.01f, -0.06f, 0f), V(0.008f, 0.08f, 0.012f), Black);
        Part(PrimitiveType.Cube, puller.transform, "JawR",   V(0.01f, -0.06f, 0f),  V(0.008f, 0.08f, 0.012f), Black);
        FusePullerTool pullerTool = puller.AddComponent<FusePullerTool>();
        pullerTool.jaw = Empty(puller.transform, "Jaw", V(0f, -0.1f, 0f));
        ToolRack rack = r.gameObject.AddComponent<ToolRack>();
        rack.tool = puller.GetComponent<SnapItem>();
        rack.slot = pullerSlot;

        // Spare fuse tray and new fuse
        Part(PrimitiveType.Cube, r, "FuseTray", V(2.05f, 0.97f, 1.7f), V(0.12f, 0.04f, 0.08f), White, true);
        Text(r, "TrayText", "<b>SPARE FUSES</b>", V(2.05f, 0.97f, 1.658f), Vector3.zero, new Vector2(0.11f, 0.03f), 0.12f, Color.black);
        GameObject fuse = Grabbable(r, "NewFuse", V(2.05f, 1.045f, 1.7f), 0.05f, Vector3.zero);
        CapsuleCollider fc = fuse.AddComponent<CapsuleCollider>();
        fc.radius = 0.016f;
        fc.height = 0.1f;
        Part(PrimitiveType.Cylinder, fuse.transform, "Body",      V(0f, 0f, 0f),      V(0.03f, 0.04f, 0.03f),    Ceramic);
        Part(PrimitiveType.Cylinder, fuse.transform, "CapTop",    V(0f, 0.04f, 0f),   V(0.032f, 0.008f, 0.032f), Brass);
        Part(PrimitiveType.Cylinder, fuse.transform, "CapBottom", V(0f, -0.04f, 0f),  V(0.032f, 0.008f, 0.032f), Brass);
        holder.newFuse = fuse.GetComponent<SnapItem>();

        // Voltage tester
        GameObject tester = Grabbable(r, "VoltageTester", V(1.7f, 0.975f, 1.65f), 0.25f, new Vector3(0.05f, 0.03f, 0.18f));
        Part(PrimitiveType.Cube, tester.transform, "Body",   V(0f, 0f, 0f),       V(0.05f, 0.03f, 0.14f),   Mat("Mat_TesterYellow", new Color32(230, 190, 30, 255), 0.4f));
        Part(PrimitiveType.Cube, tester.transform, "Grip",   V(0f, 0f, -0.05f),   V(0.052f, 0.032f, 0.04f), Black);
        Part(PrimitiveType.Cylinder, tester.transform, "Probe", V(0f, 0f, 0.085f), V(0.008f, 0.015f, 0.008f), Metal, false, V(90f, 0f, 0f));
        Part(PrimitiveType.Cube, tester.transform, "Screen", V(0f, 0.0155f, 0.015f), V(0.04f, 0.002f, 0.05f), Black);
        TextMeshPro display = Text(tester.transform, "Display", "- - -", V(0f, 0.018f, 0.015f), V(90f, 0f, 0f), new Vector2(0.045f, 0.03f), 0.15f, Color.gray);
        GameObject testerLamp = Part(PrimitiveType.Sphere, tester.transform, "Lamp", V(0f, 0.017f, 0.055f), V(0.014f, 0.014f, 0.014f), GlowMat("Mat_LampGlass"));
        TesterProbe probe = tester.AddComponent<TesterProbe>();
        probe.display = display;
        probe.lamp = testerLamp.GetComponent<Renderer>();

        // Proving unit
        Transform pu = Empty(r, "ProvingUnit", V(1.72f, 0.95f, 1.98f));
        Part(PrimitiveType.Cube, pu, "Body", V(0f, 0.03f, 0f), V(0.1f, 0.06f, 0.07f), Red, true);
        Part(PrimitiveType.Cylinder, pu, "Contact", V(0f, 0.065f, 0f), V(0.02f, 0.005f, 0.02f), Brass);
        Text(pu, "Label", "<b>PROVING UNIT</b>", V(0f, 0.03f, -0.037f), Vector3.zero, new Vector2(0.095f, 0.03f), 0.11f, Color.white);
        Transform proveZone = Empty(pu, "ProveZone", V(0f, 0.09f, 0f));
        BoxCollider pz = proveZone.gameObject.AddComponent<BoxCollider>();
        pz.isTrigger = true;
        pz.size = new Vector3(0.1f, 0.06f, 0.08f);
        proveZone.gameObject.AddComponent<ProvingUnit>();
    }

    // ---------- job card (west wall, at Anchor_JobCard) ----------

    static void BuildJobCard()
    {
        Transform t = Root("JobCard", new Vector3(-3.9f, 1.5f, -2.2f), -90f);
        Part(PrimitiveType.Cube, t, "Board", V(0f, 0f, 0.01f), V(0.62f, 0.86f, 0.01f), Wood);
        Part(PrimitiveType.Cube, t, "Paper", V(0f, -0.02f, 0.003f), V(0.56f, 0.76f, 0.004f), White);
        Part(PrimitiveType.Cube, t, "Clip",  V(0f, 0.4f, 0f),       V(0.16f, 0.05f, 0.02f),  Metal);
        TextMeshPro card = Text(t, "Text", "", V(0f, -0.02f, -0.003f), Vector3.zero, new Vector2(0.52f, 0.72f), 0.3f, Color.black);
        card.alignment = TextAlignmentOptions.TopLeft;
        card.enableAutoSizing = true;
        card.fontSizeMin = 0.1f;
        card.fontSizeMax = 0.3f;
        t.gameObject.AddComponent<JobCardBoard>().text = card;
    }

    // ---------- results board (south wall, at Anchor_Results) ----------

    static void BuildResultsBoard()
    {
        Transform t = Root("ResultsBoard", new Vector3(1.4f, 1.6f, -2.97f), 180f);
        Part(PrimitiveType.Cube, t, "Frame", V(0f, 0f, 0.01f), V(1.6f, 1.1f, 0.02f), Charcoal);
        Text(t, "Header", "TRAINING RESULTS", V(0f, 0.48f, -0.005f), Vector3.zero, new Vector2(1.5f, 0.1f), 0.4f, new Color(0.75f, 0.75f, 0.75f));

        GameObject content = new GameObject("Content");
        content.transform.SetParent(t, false);

        TextMeshPro title = Text(content.transform, "Title", "", V(0f, 0.31f, -0.005f), Vector3.zero, new Vector2(1.5f, 0.24f), 0.9f, Color.white);
        title.fontStyle = FontStyles.Bold;

        TextMeshPro details = Text(content.transform, "Details", "", V(-0.02f, -0.12f, -0.005f), Vector3.zero, new Vector2(1.46f, 0.6f), 0.35f, Color.white);
        details.alignment = TextAlignmentOptions.TopLeft;
        details.enableAutoSizing = true;
        details.fontSizeMin = 0.12f;
        details.fontSizeMax = 0.35f;

        GameObject button = Part(PrimitiveType.Cube, content.transform, "RestartButton", V(0.55f, -0.47f, -0.02f), V(0.4f, 0.09f, 0.04f), Green, true);
        Text(content.transform, "RestartText", "<b>RESTART TRAINING</b>", V(0.55f, -0.47f, -0.041f), Vector3.zero, new Vector2(0.38f, 0.08f), 0.3f, Color.white);
        XRSimpleInteractable restart = button.AddComponent<XRSimpleInteractable>();

        ResultsBoard board = t.gameObject.AddComponent<ResultsBoard>();
        board.content = content;
        board.title = title;
        board.details = details;
        board.restartButton = restart;
    }

    // ---------- small scene adjustments ----------

    static void MoveMetalToolHazard()
    {
        GameObject tool = GameObject.Find("Hazard_MetalTool");
        if (tool == null) return;
        Undo.RecordObject(tool.transform, "Job setup");
        tool.transform.position = new Vector3(-3.65f, 1.2f, -0.8f);   // on top of Workstation 2
        PrefabUtility.RecordPrefabInstancePropertyModifications(tool.transform);
    }

    static void MarkLockoutTag()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(BreakerPrefab);
        try
        {
            Transform tag = root.transform.Find("LockoutTag");
            if (tag == null) return;
            if (tag.GetComponent<TrainingTool>() == null) tag.gameObject.AddComponent<TrainingTool>();
            PrefabUtility.SaveAsPrefabAsset(root, BreakerPrefab);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ---------- helpers ----------

    static Vector3 V(float x, float y, float z) { return new Vector3(x, y, z); }

    static Transform Root(string name, Vector3 position, float yRotation)
    {
        GameObject old = GameObject.Find(name);
        if (old != null) Undo.DestroyObjectImmediate(old);
        GameObject go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Job setup");
        go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yRotation, 0f));
        return go.transform;
    }

    static Transform Empty(Transform parent, string name, Vector3 localPosition)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        return go.transform;
    }

    static GameObject Part(PrimitiveType type, Transform parent, string name, Vector3 position, Vector3 scale,
                           Material mat, bool keepCollider = false, Vector3? euler = null)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localRotation = Quaternion.Euler(euler ?? Vector3.zero);
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        if (!keepCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    // A grabbable job item: rigidbody, velocity-tracked grab, snapping support, training-only.
    static GameObject Grabbable(Transform parent, string name, Vector3 worldPosition, float mass, Vector3 boxSize)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = worldPosition;

        Rigidbody body = go.AddComponent<Rigidbody>();
        body.mass = mass;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.interpolation = RigidbodyInterpolation.Interpolate;

        if (boxSize != Vector3.zero) go.AddComponent<BoxCollider>().size = boxSize;

        XRGrabInteractable grab = go.AddComponent<XRGrabInteractable>();
        grab.movementType = XRBaseInteractable.MovementType.VelocityTracking;
        grab.throwOnDetach = false;

        go.AddComponent<SnapItem>();
        go.AddComponent<TrainingTool>();
        return go;
    }

    static TextMeshPro Text(Transform parent, string name, string text, Vector3 position, Vector3 euler,
                            Vector2 size, float fontSize, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        TextMeshPro tmp = go.AddComponent<TextMeshPro>();
        tmp.rectTransform.sizeDelta = size;
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = color;
        go.transform.localPosition = position;
        go.transform.localRotation = Quaternion.Euler(euler);
        return tmp;
    }

    static Material Mat(string name, Color color, float smoothness)
    {
        string path = MaterialFolder + "/" + name + ".mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null) return mat;
        mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.SetColor("_BaseColor", color);
        mat.SetFloat("_Smoothness", smoothness);
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    static Material GlowMat(string name)
    {
        string path = MaterialFolder + "/" + name + ".mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null) return mat;
        mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.SetColor("_BaseColor", new Color(0.3f, 0.3f, 0.3f));
        mat.SetFloat("_Smoothness", 0.8f);
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", Color.black);
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }
}