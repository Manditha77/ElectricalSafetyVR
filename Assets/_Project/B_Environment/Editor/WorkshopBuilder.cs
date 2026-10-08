using TMPro;
using UnityEditor;
using UnityEngine;

// Builds parts of the workshop inside the Workshop_Main prefab from the Tools menu.
public static class WorkshopBuilder
{
    const string PrefabPath = "Assets/_Project/B_Environment/Workshop_Main.prefab";
    const string MaterialFolder = "Assets/_Project/B_Environment/Materials";

    // ---------- menu items ----------

    [MenuItem("Tools/Workshop/Build Entrance Door")]
    static void MenuEntrance() { Build("entrance door", Entrance); }

    [MenuItem("Tools/Workshop/Build Workshop Details")]
    static void MenuDetails() { Build("workshop details", Details); }

    [MenuItem("Tools/Workshop/Build Everything")]
    static void MenuAll() { Build("everything", root => { Entrance(root); Details(root); }); }

    // ---------- materials (existing ones are used as they are) ----------

    static Material Wood      => Mat("Mat_Bench",     new Color32(120, 85, 50, 255),   0.3f, false);
    static Material Metal     => Mat("Mat_Metal",     new Color32(130, 135, 140, 255), 0.6f, false);
    static Material Black     => Mat("Mat_Black",     new Color32(20, 20, 25, 255),    0.8f, false);
    static Material ExitGreen => Mat("Mat_Exit",      new Color32(0, 140, 60, 255),    0.3f, true);
    static Material Steel     => Mat("Mat_Steel",     new Color32(45, 52, 62, 255),    0.5f, false);
    static Material Trunking  => Mat("Mat_Trunking",  new Color32(205, 205, 200, 255), 0.3f, false);
    static Material Red       => Mat("Mat_Danger",    new Color32(200, 30, 30, 255),   0.4f, false);
    static Material Yellow    => Mat("Mat_Warning",   new Color32(240, 200, 0, 255),   0.3f, false);
    static Material Cardboard => Mat("Mat_Cardboard", new Color32(170, 130, 85, 255),  0.1f, false);
    static Material Poster    => Mat("Mat_Poster",    new Color32(235, 235, 230, 255), 0.2f, false);

    // ---------- 1. entrance door (south wall) ----------

    static void Entrance(GameObject root)
    {
        Transform g = Group(root, "Entrance");

        Box(g, "Door",            V(-2.5f, 1.05f, -2.98f),  V(0.9f, 2.1f, 0.05f),   Wood);
        Box(g, "DoorFrame_Top",   V(-2.5f, 2.15f, -2.98f),  V(1.1f, 0.1f, 0.08f),   Metal);
        Box(g, "DoorFrame_Left",  V(-3.0f, 1.05f, -2.98f),  V(0.1f, 2.1f, 0.08f),   Metal);
        Box(g, "DoorFrame_Right", V(-2.0f, 1.05f, -2.98f),  V(0.1f, 2.1f, 0.08f),   Metal);
        Box(g, "Door_Handle",     V(-2.15f, 1.05f, -2.94f), V(0.04f, 0.15f, 0.04f), Metal);
        Box(g, "Door_KickPlate",  V(-2.5f, 0.12f, -2.953f), V(0.86f, 0.22f, 0.01f), Metal);
        Box(g, "Door_Window",     V(-2.5f, 1.6f, -2.953f),  V(0.3f, 0.45f, 0.01f),  Black);
        Box(g, "Exit_Board",      V(-2.5f, 2.4f, -2.99f),   V(0.6f, 0.2f, 0.02f),   ExitGreen);
        Label(g, "Exit_Text", "EXIT", V(-2.5f, 2.4f, -2.975f), 180f, new Vector2(0.6f, 0.2f), 1.5f, Color.white);
    }

    // ---------- 2. workshop details ----------

    static void Details(GameObject root)
    {
        Transform g = Group(root, "Details");

        // Skirting along the bottom of each wall (south one is split around the door)
        Box(g, "Skirting_North",  V(0f, 0.05f, 2.915f),       V(7.85f, 0.1f, 0.02f),  Steel);
        Box(g, "Skirting_East",   V(3.915f, 0.05f, -0.0375f), V(0.02f, 0.1f, 5.925f), Steel);
        Box(g, "Skirting_West",   V(-3.915f, 0.05f, -0.0375f),V(0.02f, 0.1f, 5.925f), Steel);
        Box(g, "Skirting_SouthA", V(-3.4875f, 0.05f, -2.99f), V(0.875f, 0.1f, 0.02f), Steel);
        Box(g, "Skirting_SouthB", V(0.9875f, 0.05f, -2.99f),  V(5.875f, 0.1f, 0.02f), Steel);

        // Steel columns in the four corners
        Box(g, "Column_NE", V(3.85f, 1.5f, 2.85f),    V(0.15f, 3f, 0.15f), Steel);
        Box(g, "Column_NW", V(-3.85f, 1.5f, 2.85f),   V(0.15f, 3f, 0.15f), Steel);
        Box(g, "Column_SE", V(3.85f, 1.5f, -2.925f),  V(0.15f, 3f, 0.15f), Steel);
        Box(g, "Column_SW", V(-3.85f, 1.5f, -2.925f), V(0.15f, 3f, 0.15f), Steel);

        // Cable trunking: along the ceiling through the lights, then down the east wall into the breaker board
        Box(g, "Trunk_Ceiling1",    V(-1.25f, 2.975f, 0f),   V(1.3f, 0.05f, 0.05f),   Trunking);
        Box(g, "Trunk_Ceiling2",    V(1.25f, 2.975f, 0f),    V(1.3f, 0.05f, 0.05f),   Trunking);
        Box(g, "Trunk_Ceiling3",    V(3.5125f, 2.975f, 0f),  V(0.825f, 0.05f, 0.05f), Trunking);
        Box(g, "Trunk_CeilingTurn", V(3.9f, 2.975f, -0.3f), V(0.05f, 0.05f, 0.65f), Trunking);
        Box(g, "Trunk_EastDrop",    V(3.9f, 2.2f, -0.6f),   V(0.05f, 1.6f, 0.05f),  Trunking);
        Box(g, "Trunk_ToBoard",     V(3.9f, 1.4f, -0.35f),  V(0.05f, 0.05f, 0.55f), Trunking);

        // Fire extinguisher on the south wall, right of the door
        Box(g, "Ext_Bracket", V(-1.5f, 1.0f, -2.985f),  V(0.16f, 0.05f, 0.03f), Metal);
        Cyl(g, "Ext_Body",    V(-1.5f, 1.0f, -2.92f),   V(0.14f, 0.22f, 0.14f), Red);
        Cyl(g, "Ext_Neck",    V(-1.5f, 1.25f, -2.92f),  V(0.05f, 0.03f, 0.05f), Black);
        Box(g, "Ext_Handle",  V(-1.5f, 1.3f, -2.9f),    V(0.04f, 0.03f, 0.12f), Black);
        Box(g, "Ext_Sign",    V(-1.5f, 1.65f, -2.995f), V(0.4f, 0.25f, 0.01f),  Red);
        Label(g, "Ext_SignText", "FIRE\nEXTINGUISHER", V(-1.5f, 1.65f, -2.985f), 180f,
              new Vector2(0.4f, 0.25f), 0.4f, Color.white);

        // Storage shelf in the south-east corner (against the east wall)
        float[] postX = { 3.545f, 3.905f };
        float[] postZ = { -2.78f, -1.92f };
        foreach (float x in postX)
            foreach (float z in postZ)
                Box(g, "Shelf_Post", V(x, 0.9f, z), V(0.04f, 1.8f, 0.04f), Steel);

        float[] boardY = { 0.2f, 0.7f, 1.2f, 1.75f };
        foreach (float y in boardY)
            Box(g, "Shelf_Board", V(3.725f, y, -2.35f), V(0.4f, 0.03f, 0.9f), Metal);

        Box(g, "Shelf_BoxLow",  V(3.72f, 0.365f, -2.55f), V(0.3f, 0.3f, 0.3f),   Cardboard);
        Box(g, "Shelf_BoxMid",  V(3.72f, 0.84f, -2.2f),   V(0.3f, 0.25f, 0.4f),  Cardboard);
        Box(g, "Shelf_Toolbox", V(3.72f, 1.29f, -2.573f),   V(0.25f, 0.15f, 0.3f), Red);

        // Safe isolation poster on the west wall, between the workstation and the PPE area
        Box(g, "Poster_Board",  V(-3.92f, 1.5f, 0.6f),   V(0.01f, 0.8f, 0.6f),   Poster);
        Box(g, "Poster_Header", V(-3.918f, 1.83f, 0.6f), V(0.012f, 0.14f, 0.6f), ExitGreen);
        Label(g, "Poster_Title", "SAFE ISOLATION", V(-3.908f, 1.83f, 0.6f), -90f,
              new Vector2(0.6f, 0.14f), 0.55f, Color.white);
        Label(g, "Poster_Steps",
              "1. Wear PPE\n2. Switch OFF\n3. Lock and tag\n4. Test for dead\n5. Repair\n6. Restore",
              V(-3.908f, 1.42f, 0.6f), -90f, new Vector2(0.55f, 0.6f), 0.5f, Color.black);

        // Yellow keep-clear box on the floor in front of the breaker board
        Box(g, "KeepClear_Front", V(2.9f, 0.005f, 0.5f),  V(0.08f, 0.01f, 1.28f), Yellow);
        Box(g, "KeepClear_Left",  V(3.4f, 0.005f, -0.1f), V(1.0f, 0.01f, 0.08f),  Yellow);
        Box(g, "KeepClear_Right", V(3.4f, 0.005f, 1.1f),  V(1.0f, 0.01f, 0.08f),  Yellow);
    }

    // ---------- helpers ----------

    static Vector3 V(float x, float y, float z) { return new Vector3(x, y, z); }

    // Opens the prefab, runs one builder, saves, closes.
    static void Build(string what, System.Action<GameObject> builder)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            builder(root);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log("Workshop: built " + what + ".");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // Makes an empty group under the prefab root. If it already exists it is rebuilt.
    static Transform Group(GameObject root, string name)
    {
        Transform old = root.transform.Find(name);
        if (old != null) Object.DestroyImmediate(old.gameObject);

        GameObject go = new GameObject(name);
        go.transform.SetParent(root.transform, false);
        return go.transform;
    }

    // Uses the material if it exists (never changes it); otherwise creates it.
    static Material Mat(string name, Color color, float smoothness, bool glows)
    {
        string path = MaterialFolder + "/" + name + ".mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null) return mat;

        mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.SetColor("_BaseColor", color);
        mat.SetFloat("_Smoothness", smoothness);
        if (glows)
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", color);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
        }
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    static void Box(Transform parent, string name, Vector3 position, Vector3 scale, Material mat)
    {
        Part(PrimitiveType.Cube, parent, name, position, scale, mat);
    }

    // Cylinder: scale X and Z are the diameter, scale Y is HALF the height.
    static void Cyl(Transform parent, string name, Vector3 position, Vector3 scale, Material mat)
    {
        Part(PrimitiveType.Cylinder, parent, name, position, scale, mat);
    }

    static void Part(PrimitiveType type, Transform parent, string name, Vector3 position, Vector3 scale, Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        Object.DestroyImmediate(go.GetComponent<Collider>());   // decoration only
    }

    static void Label(Transform parent, string name, string text, Vector3 position,
                      float yRotation, Vector2 size, float fontSize, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);

        TextMeshPro tmp = go.AddComponent<TextMeshPro>();
        tmp.rectTransform.sizeDelta = size;
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = color;

        go.transform.localPosition = position;
        go.transform.localRotation = Quaternion.Euler(0f, yRotation, 0f);
    }
}