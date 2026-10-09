using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.UI;

// One-click setup for the isolation point and the pre-work check flow.
public static class FlowSetup
{
    const string BreakerPrefab  = "Assets/_Project/C_Interactions/Prefabs/BreakerPanel.prefab";
    const string WorkshopPrefab = "Assets/_Project/B_Environment/Workshop_Main.prefab";
    const string MaterialFolder = "Assets/_Project/B_Environment/Materials";
    const float IsolatorOffset = 0.62f;   // W1 / W2 distance from the power box

    static Material IsolatorGrey => Mat("Mat_IsolatorGrey", new Color32(200, 200, 195, 255), 0.4f);
    static Material Red          => Mat("Mat_Danger",       new Color32(200, 30, 30, 255),   0.4f);
    static Material Yellow       => Mat("Mat_Warning",      new Color32(240, 200, 0, 255),   0.3f);
    static Material White        => Mat("Mat_Poster",       new Color32(235, 235, 230, 255), 0.2f);
    static Material Charcoal     => Mat("Mat_Steel",        new Color32(45, 52, 62, 255),    0.5f);

    // ---------- 1. breaker panel: two real-looking isolators, W1 wrong and W2 correct ----------

    [MenuItem("Tools/Workshop/Fix Breaker Panel")]
    static void FixBreakerPanel()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(BreakerPrefab);
        try
        {
            Transform w2 = root.transform.Find("Isolator_W2");
            if (w2 == null) { Debug.LogError("Isolator_W2 not found in BreakerPanel."); return; }

            // The lever must not toggle itself and bypass the safety manager.
            Transform lever = w2.Find("LeverPivot/Lever");
            if (lever != null)
            {
                XRSimpleInteractable direct = lever.GetComponent<XRSimpleInteractable>();
                if (direct != null) Object.DestroyImmediate(direct);
            }

            // The hidden "wrong breaker" over the power box is replaced by W1.
            Transform row2 = root.transform.Find("Row2_Correct");
            if (row2 != null) row2.gameObject.SetActive(false);

            // Clean W2 of anything added earlier, then copy it to make W1.
            foreach (string n in new[] { "Label", "NamePlate", "HandlePlate" }) RemoveChild(w2, n);
            RemoveChild(root.transform, "Isolator_W1");

            Vector3 p = w2.localPosition;
            w2.localPosition = new Vector3(-IsolatorOffset, p.y, p.z);
            GameObject w1 = Object.Instantiate(w2.gameObject, root.transform);
            w1.name = "Isolator_W1";
            w1.transform.localPosition = new Vector3(IsolatorOffset, p.y, p.z);
            w1.transform.localRotation = w2.localRotation;
            w1.transform.localScale = w2.localScale;

            w1.GetComponent<BreakerSwitch>().isCorrectBreaker = false;
            w2.GetComponent<BreakerSwitch>().isCorrectBreaker = true;

            StyleIsolator(w1.transform, "W1", "WORKSTATION 1");
            StyleIsolator(w2, "W2", "WORKSTATION 2");

            Transform sign = root.transform.Find("PanelSign/Text (TMP)");
            if (sign != null) sign.GetComponent<TMP_Text>().text = "LOCAL ISOLATORS";

            // Label under the indicator lamp
            Transform lamp = root.transform.Find("PanelLamp");
            if (lamp != null)
            {
                RemoveChild(lamp, "LampLabel");
                TextMeshPro t = Label(lamp, "LampLabel", "ISOLATED", new Vector3(0f, -0.12f, 0.01f), 180f,
                                      new Vector2(0.3f, 0.08f), 0.6f, Color.white);
                t.fontStyle = FontStyles.Bold;
            }
            // Indicator lamp should light its own area, not the whole wall.
            Transform lampGlow = root.transform.Find("PanelLamp/Glow");
            if (lampGlow != null)
            {
                Light l = lampGlow.GetComponent<Light>();
                l.intensity = 0.8f;
                l.range = 1.2f;
            }

            PrefabUtility.SaveAsPrefabAsset(root, BreakerPrefab);
            Debug.Log("Workshop: breaker panel fixed and styled (W1 wrong, W2 correct).");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // Grey enclosure, red handle on a yellow plate, white nameplate above.
    static void StyleIsolator(Transform iso, string code, string name)
    {
        SetMaterial(iso.Find("SwitchBox"), IsolatorGrey);
        SetMaterial(iso.Find("LeverPivot/Lever"), Red);

        Box(iso, "HandlePlate", new Vector3(0f, 0f, -0.078f), new Vector3(0.24f, 0.3f, 0.004f), Yellow);

        GameObject plate = new GameObject("NamePlate");
        plate.transform.SetParent(iso, false);
        Box(plate.transform, "Plate", new Vector3(0f, 0.34f, -0.075f), new Vector3(0.34f, 0.11f, 0.006f), White);
        Label(plate.transform, "Text", "<size=140%><b>" + code + "</b></size>\n" + name,
              new Vector3(0f, 0.34f, -0.08f), 0f, new Vector2(0.32f, 0.1f), 0.35f, Color.black);
    }

    // ---------- 2. isolation point on the wall: mounting board, safety border, sign above ----------

    [MenuItem("Tools/Workshop/Style Isolation Point")]
    static void StyleIsolationPoint()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(WorkshopPrefab);
        try
        {
            Transform old = FindDeep(root.transform, "Breaker_BackingBoard");
            if (old != null) old.gameObject.SetActive(false);

            RemoveChild(root.transform, "IsolationBoard");
            Transform g = new GameObject("IsolationBoard").transform;
            g.SetParent(root.transform, false);

            Box(g, "Board",       new Vector3(3.915f, 1.65f, 0.5f),  new Vector3(0.02f, 1.6f, 2.0f),  Charcoal);
            Box(g, "Trim_Top",    new Vector3(3.9f, 2.43f, 0.5f),    new Vector3(0.02f, 0.04f, 2.0f), Yellow);
            Box(g, "Trim_Bottom", new Vector3(3.9f, 0.87f, 0.5f),    new Vector3(0.02f, 0.04f, 2.0f), Yellow);
            Box(g, "Trim_Left",   new Vector3(3.9f, 1.65f, 1.48f),   new Vector3(0.02f, 1.6f, 0.04f), Yellow);
            Box(g, "Trim_Right",  new Vector3(3.9f, 1.65f, -0.48f),  new Vector3(0.02f, 1.6f, 0.04f), Yellow);

            // Lift the wall sign above the new board.
            foreach (TMP_Text t in root.GetComponentsInChildren<TMP_Text>(true))
            {
                if (t.text.Trim().ToUpper() != "ISOLATION POINT") continue;
                Vector3 lp = t.transform.localPosition;
                t.transform.localPosition = new Vector3(lp.x, 2.65f, lp.z);
            }

            PrefabUtility.SaveAsPrefabAsset(root, WorkshopPrefab);
            Debug.Log("Workshop: isolation point styled.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ---------- 3. scene: welcome board layout, flow, status panel, managers ----------

    [MenuItem("Tools/Workshop/Set Up Pre-Work Check Scene")]
    static void SetUpScene()
    {
        SessionManager session = Object.FindFirstObjectByType<SessionManager>();
        ElectricalSafetyManager safety = Object.FindFirstObjectByType<ElectricalSafetyManager>();
        GameObject canvas = GameObject.Find("WelcomeCanvas");
        if (session == null || safety == null || canvas == null)
        {
            Debug.LogError("Open ElectricalWorkshop first (Managers or WelcomeCanvas not found).");
            return;
        }

        // Managers
        Undo.RecordObject(session, "Flow setup");
        session.autoStartForTesting = false;
        PrefabUtility.RecordPrefabInstancePropertyModifications(session);
        Undo.RecordObject(safety, "Flow setup");
        safety.requiredPpeItems = 5;
        safety.requiredHazards = 3;
        PrefabUtility.RecordPrefabInstancePropertyModifications(safety);

        // Breaker panel replaces the old single switch
        GameObject oldSwitch = GameObject.Find("MainSwitch");
        if (oldSwitch != null)
        {
            Undo.RecordObject(oldSwitch, "Flow setup");
            oldSwitch.SetActive(false);
        }
        if (GameObject.Find("BreakerPanel") == null)
        {
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(BreakerPrefab);
            GameObject panel = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            Undo.RegisterCreatedObjectUndo(panel, "Flow setup");
        }

        // Welcome canvas pieces
        if (canvas.GetComponent<TrackedDeviceGraphicRaycaster>() == null)
            Undo.AddComponent<TrackedDeviceGraphicRaycaster>(canvas);

        Transform startT = canvas.transform.Find("StartButton");
        Transform mainT = canvas.transform.Find("Text (TMP)");
        if (startT == null || mainT == null) { Debug.LogError("StartButton or Text (TMP) not found under WelcomeCanvas."); return; }

        Transform preT = canvas.transform.Find("PreCheckButton");
        if (preT == null) preT = Copy(startT, "PreCheckButton");
        Transform sumT = canvas.transform.Find("SummaryText");
        if (sumT == null) sumT = Copy(mainT, "SummaryText");

        Button start = startT.GetComponent<Button>();
        Button pre = preT.GetComponent<Button>();
        ClearClicks(start);
        ClearClicks(pre);

        // Layout inside the welcome board (board spans x -1.3..1.3, y 1.1..2.5)
        float z = canvas.transform.position.z;
        Place(mainT, new Vector3(0f, 2.1f, z),     new Vector2(1150f, 340f));
        Place(sumT,  new Vector3(0f, 1.55f, z),    new Vector2(1150f, 120f));
        Place(preT,  new Vector3(-0.45f, 1.25f, z), new Vector2(380f, 70f));
        Place(startT, new Vector3(0.45f, 1.25f, z), new Vector2(380f, 70f));

        TMP_Text main = mainT.GetComponent<TMP_Text>();
        StyleText(main, 30f, new Color(0.1f, 0.1f, 0.12f));
        main.text =
            "<size=140%><b>WELCOME, TRAINEE</b></size>\n\n" +
            "<b>Your job:</b> Workstation 2 has a blown fuse. Make the area safe and replace it.\n\n" +
            "<b>1.</b> Pre-work check: put on your PPE and report every hazard.\n" +
            "<b>2.</b> Training: isolate W2, lock it off, prove it dead, replace the fuse, restore power.";

        TMP_Text summary = sumT.GetComponent<TMP_Text>();
        StyleText(summary, 24f, new Color(0.1f, 0.25f, 0.5f));
        summary.text = "Step 1: Pre-work check";

        TMP_Text preLabel = preT.GetComponentInChildren<TMP_Text>();
        TMP_Text startLabel = startT.GetComponentInChildren<TMP_Text>();
        StyleButton(pre, preLabel, "Start Pre-Work Check", new Color(0.98f, 0.72f, 0.15f), new Color(0.1f, 0.1f, 0.1f));
        StyleButton(start, startLabel, "Start Training", new Color(0.15f, 0.55f, 0.25f), Color.white);

        // Flow controller (outside the canvas, because the canvas gets hidden)
        GameObject flowGo = FindOrCreate("TrainingFlow");
        TrainingFlow flow = flowGo.GetComponent<TrainingFlow>();
        if (flow == null) flow = Undo.AddComponent<TrainingFlow>(flowGo);
        Undo.RecordObject(flow, "Flow setup");
        flow.preCheckButton = pre;
        flow.preCheckButtonLabel = preLabel;
        flow.startTrainingButton = start;
        flow.summaryText = summary;
        flow.welcomeGroup = canvas;

        // Floating status panel
        GameObject hudGo = FindOrCreate("StatusHud");
        if (hudGo.GetComponent<StatusHud>() == null) Undo.AddComponent<StatusHud>(hudGo);

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("Workshop: pre-work check flow set up. Save the scene (Ctrl+S).");
    }

    // ---------- 4. fault effects: flickering room lights, status beacon, 3D sounds ----------

    [MenuItem("Tools/Workshop/Set Up Fault Effects")]
    static void SetUpFaultEffects()
    {
        GameObject workshop = GameObject.Find("Workshop_Main");
        if (workshop == null) { Debug.LogError("Open ElectricalWorkshop first (Workshop_Main not found)."); return; }

        GameObject old = GameObject.Find("FaultEffects");
        if (old != null) Undo.DestroyObjectImmediate(old);

        GameObject root = new GameObject("FaultEffects");
        Undo.RegisterCreatedObjectUndo(root, "Fault effects");
        FaultAtmosphere fx = root.AddComponent<FaultAtmosphere>();
        fx.lightingRoot = workshop.transform;

        // Beacon on a wall bracket above the left corner of the isolation board
        Transform beacon = new GameObject("StatusBeacon").transform;
        beacon.SetParent(root.transform, false);
        beacon.position = new Vector3(3.8f, 2.55f, 1.4f);
        Box(beacon, "Bracket", new Vector3(0.06f, 0f, 0f), new Vector3(0.12f, 0.02f, 0.1f), Charcoal);
        Prim(PrimitiveType.Cylinder, beacon, "Base", new Vector3(0f, 0.03f, 0f), new Vector3(0.1f, 0.02f, 0.1f), Charcoal);
        Renderer lens = Prim(PrimitiveType.Cylinder, beacon, "Lens", new Vector3(0f, 0.1f, 0f),
                             new Vector3(0.09f, 0.05f, 0.09f), GlowMat("Mat_BeaconLens"));
        Prim(PrimitiveType.Cylinder, beacon, "Cap", new Vector3(0f, 0.16f, 0f), new Vector3(0.09f, 0.01f, 0.09f), Charcoal);

        Light glow = new GameObject("BeaconLight").AddComponent<Light>();
        glow.transform.SetParent(beacon, false);
        glow.transform.localPosition = new Vector3(-0.15f, 0.1f, 0f);   // just in front of the lens
        glow.type = LightType.Point;
        glow.range = 2.5f;
        glow.intensity = 1f;
        glow.enabled = false;

        fx.beaconLight = glow;
        fx.beaconLens = lens;
        fx.faultHum = Audio(beacon, "FaultHumSource", true, 0.4f);
        fx.isolateSound = Audio(beacon, "IsolateSoundSource", false, 0.8f);

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("Workshop: fault effects set up. Save the scene (Ctrl+S).");
    }

    static Renderer Prim(PrimitiveType type, Transform parent, string name, Vector3 position, Vector3 scale, Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localScale = scale;
        Renderer r = go.GetComponent<Renderer>();
        r.sharedMaterial = mat;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        return r;
    }

    static AudioSource Audio(Transform parent, string name, bool loop, float volume)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        AudioSource a = go.AddComponent<AudioSource>();
        a.spatialBlend = 1f;               // fully 3D
        a.loop = loop;
        a.playOnAwake = false;
        a.volume = volume;
        a.minDistance = 1f;
        a.maxDistance = 12f;
        a.rolloffMode = AudioRolloffMode.Linear;
        return a;
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

    // ---------- helpers ----------

    static Transform Copy(Transform original, string name)
    {
        GameObject copy = Object.Instantiate(original.gameObject, original.parent);
        copy.name = name;
        Undo.RegisterCreatedObjectUndo(copy, "Flow setup");
        return copy.transform;
    }

    static void Place(Transform t, Vector3 worldPosition, Vector2 size)
    {
        RectTransform r = (RectTransform)t;
        Undo.RecordObject(r, "Flow setup");
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
        r.pivot = new Vector2(0.5f, 0.5f);
        r.sizeDelta = size;
        r.position = worldPosition;
    }

    static void StyleText(TMP_Text t, float maxSize, Color color)
    {
        Undo.RecordObject(t, "Flow setup");
        t.enableAutoSizing = true;
        t.fontSizeMin = 16f;
        t.fontSizeMax = maxSize;
        t.alignment = TextAlignmentOptions.Center;
        t.color = color;
    }

    static void StyleButton(Button b, TMP_Text label, string text, Color fill, Color textColor)
    {
        Image img = b.GetComponent<Image>();
        if (img != null) { Undo.RecordObject(img, "Flow setup"); img.color = fill; }

        Undo.RecordObject(b, "Flow setup");
        ColorBlock colors = b.colors;
        colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.6f);
        b.colors = colors;

        Undo.RecordObject(label, "Flow setup");
        label.text = text;
        label.color = textColor;
        label.fontStyle = FontStyles.Bold;
        label.enableAutoSizing = true;
        label.fontSizeMin = 16f;
        label.fontSizeMax = 28f;
    }

    static GameObject FindOrCreate(string name)
    {
        GameObject go = GameObject.Find(name);
        if (go != null) return go;
        go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Flow setup");
        return go;
    }

    static void ClearClicks(Button button)
    {
        Undo.RecordObject(button, "Flow setup");
        while (button.onClick.GetPersistentEventCount() > 0)
            UnityEventTools.RemovePersistentListener(button.onClick, 0);
    }

    static Transform FindDeep(Transform parent, string name)
    {
        foreach (Transform child in parent.GetComponentsInChildren<Transform>(true))
            if (child.name == name) return child;
        return null;
    }

    static void RemoveChild(Transform parent, string name)
    {
        Transform child = parent.Find(name);
        if (child != null) Object.DestroyImmediate(child.gameObject);
    }

    static void SetMaterial(Transform t, Material mat)
    {
        if (t == null) return;
        Renderer r = t.GetComponent<Renderer>();
        if (r != null) r.sharedMaterial = mat;
    }

    static void Box(Transform parent, string name, Vector3 position, Vector3 scale, Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        Object.DestroyImmediate(go.GetComponent<Collider>());
    }

    static TextMeshPro Label(Transform parent, string name, string text, Vector3 position, float yRotation,
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
        go.transform.localRotation = Quaternion.Euler(0f, yRotation, 0f);
        return tmp;
    }

    // Uses the material if it exists (never changes it); otherwise creates it.
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
}