using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// Shows WHERE an object goes while the trainee holds it:
//  Ghost  - a see-through copy of the held object sitting at its place (tool trolley, tool board)
//  Marker - a glowing ring + soft light at the point (fuse holder, test points, proving unit)
// Far: soft pulse. Close enough: bright + "RELEASE TO PLACE" (or the custom text).
// Optional: tint small parts (the W2 test-point threads, the proving-unit contact) at the same time.
public class PlacementHint : MonoBehaviour
{
    public enum Mode { Ghost, Marker }
    public enum Condition { Always, CoverOpen }

    public XRGrabInteractable item;
    public Transform target;
    public Mode mode = Mode.Marker;
    public Condition condition = Condition.Always;
    public Material hintMaterial;            // transparent unlit
    public Color color = new Color(0.2f, 1f, 0.4f, 0.5f);
    public float showRadius = 1.8f;
    public float placeRadius = 0.3f;
    public string closeText = "RELEASE TO PLACE";
    public Renderer[] glowParts;             // tinted while the hint shows
    public bool ghostUsesItemStartPose;      // tool board: the place is where the item started

    GameObject root;
    Renderer[] hintRenderers;
    Light glow;
    TextMeshPro label;
    MaterialPropertyBlock mpb;
    Vector3 posePos;
    Vector3 targetStart;
    Quaternion poseRot;
    bool visible;

    void Start()
    {
        mpb = new MaterialPropertyBlock();
        if (item == null || target == null) { enabled = false; return; }

        targetStart = target.position;
        posePos = ghostUsesItemStartPose ? item.transform.position : target.position;
        poseRot = ghostUsesItemStartPose ? item.transform.rotation : target.rotation;

        root = new GameObject("Hint_" + item.name);
        root.transform.SetPositionAndRotation(posePos, poseRot);

        if (mode == Mode.Ghost) BuildGhost();
        else BuildMarker();

        var lg = new GameObject("HintLight");
        lg.transform.SetParent(root.transform, false);
        lg.transform.localPosition = Vector3.up * 0.06f;
        glow = lg.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.color = new Color(color.r, color.g, color.b);
        glow.range = 0.6f;
        glow.shadows = LightShadows.None;

        var tg = new GameObject("HintLabel");
        tg.transform.SetParent(root.transform, false);
        label = tg.AddComponent<TextMeshPro>();
        label.text = "<mark=#000000CC padding=\"10,10,4,4\"><b>" + closeText + "</b></mark>";
        label.fontSize = 0.28f;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.color = new Color(color.r, color.g, color.b);
        label.rectTransform.sizeDelta = new Vector2(0.5f, 0.08f);

        hintRenderers = root.GetComponentsInChildren<Renderer>(true);
        root.SetActive(false);
    }

    void BuildGhost()
    {
        foreach (var mf in item.GetComponentsInChildren<MeshFilter>(true))
        {
            var mr = mf.GetComponent<MeshRenderer>();
            if (mr == null || !mr.enabled || mf.sharedMesh == null) continue;
            var g = new GameObject("Ghost_" + mf.name);
            g.transform.SetParent(root.transform, false);
            g.transform.localPosition = item.transform.InverseTransformPoint(mf.transform.position);
            g.transform.localRotation = Quaternion.Inverse(item.transform.rotation) * mf.transform.rotation;
            Vector3 a = mf.transform.lossyScale, b = item.transform.lossyScale;
            g.transform.localScale = new Vector3(a.x / b.x, a.y / b.y, a.z / b.z);
            g.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
            var r = g.AddComponent<MeshRenderer>();
            var mats = new Material[mf.sharedMesh.subMeshCount];
            for (int i = 0; i < mats.Length; i++) mats[i] = hintMaterial;
            r.sharedMaterials = mats;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        if (root.GetComponentsInChildren<Renderer>().Length == 0) BuildMarker();
    }

    void BuildMarker()
    {
        var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(ring.GetComponent<Collider>());
        ring.name = "HintRing";
        ring.transform.SetParent(root.transform, false);
        ring.transform.localScale = new Vector3(0.09f, 0.002f, 0.09f);
        ring.GetComponent<Renderer>().sharedMaterial = hintMaterial;
        var dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(dot.GetComponent<Collider>());
        dot.name = "HintDot";
        dot.transform.SetParent(root.transform, false);
        dot.transform.localScale = Vector3.one * 0.03f;
        dot.GetComponent<Renderer>().sharedMaterial = hintMaterial;
    }

    bool ConditionOk()
    {
        if (condition == Condition.CoverOpen)
        {
            var m = ElectricalSafetyManager.Instance;
            return m != null && m.CoverOpen;
        }
        return true;
    }

    void Update()
    {
        if (root == null) return;
        bool held = item != null && item.isSelected;
        float d = held ? Vector3.Distance(item.transform.position, posePos) : 999f;
        // a marker target that has been taken away (e.g. the old fuse in the puller) = step done
        bool moved = !ghostUsesItemStartPose && mode == Mode.Marker && Vector3.Distance(target.position, targetStart) > 0.05f;
        bool show = held && !moved && target.gameObject.activeInHierarchy && ConditionOk() && d < showRadius;

        if (show != visible)
        {
            visible = show;
            root.SetActive(show);
            if (!show) ClearGlow();
        }
        if (!show) return;

        // follow a moving target (marker mode)
        if (!ghostUsesItemStartPose) root.transform.SetPositionAndRotation(target.position, mode == Mode.Ghost ? target.rotation : Quaternion.identity);

        bool near = d < placeRadius;
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.PI * 2f * (near ? 2f : 1f));
        Color c = color;
        c.a = near ? 0.75f : Mathf.Lerp(0.15f, 0.45f, pulse);
        foreach (var r in hintRenderers)
        {
            if (r == null || r is TextMeshPro || r.GetComponent<TMP_Text>() != null) continue;
            r.GetPropertyBlock(mpb);
            mpb.SetColor("_BaseColor", c);
            r.SetPropertyBlock(mpb);
        }
        glow.intensity = near ? 1.6f : Mathf.Lerp(0.3f, 0.9f, pulse);
        label.gameObject.SetActive(near);

        var cam = Camera.main;
        if (cam != null && near)
        {
            label.transform.position = root.transform.position + Vector3.up * 0.12f;
            Vector3 dir = label.transform.position - cam.transform.position;
            if (dir.sqrMagnitude > 0.0001f) label.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
        }

        if (glowParts != null)
            foreach (var r in glowParts)
            {
                if (r == null) continue;
                r.GetPropertyBlock(mpb);
                Color g = Color.Lerp(Color.white, new Color(color.r, color.g, color.b), near ? 1f : pulse);
                mpb.SetColor("_BaseColor", g);
                mpb.SetColor("_EmissionColor", new Color(color.r, color.g, color.b) * (near ? 2f : pulse));
                r.SetPropertyBlock(mpb);
            }
    }

    void ClearGlow()
    {
        if (glowParts == null) return;
        foreach (var r in glowParts) if (r != null) r.SetPropertyBlock(null);
    }

    void OnDestroy()
    {
        if (root != null) Destroy(root);
    }
}
