using TMPro;
using UnityEngine;

// "DAMAGED PARTS" bin beside Workstation 2.
// After the blown fuse is pulled out with the fuse puller, hold the puller over the bin:
// the damaged fuse drops in and stays there (it no longer just vanishes when the puller is put away).
public class FuseDisposalBin : MonoBehaviour
{
    public Transform blownFuse;
    public Transform dropPoint;        // centre of the bin opening
    public Light hintLight;
    public float catchRadius = 0.22f;

    Vector3 fuseStart;
    bool disposed, warned;
    Renderer[] fuseRenderers;

    public bool Disposed => disposed;

    void Start()
    {
        if (blownFuse == null || dropPoint == null) { enabled = false; return; }
        fuseStart = blownFuse.position;
        fuseRenderers = blownFuse.GetComponentsInChildren<Renderer>(true);
        if (hintLight != null) hintLight.enabled = false;
    }

    bool FuseVisible()
    {
        if (blownFuse == null || !blownFuse.gameObject.activeInHierarchy) return false;
        foreach (var r in fuseRenderers) if (r != null && r.enabled) return true;
        return false;
    }

    void Update()
    {
        if (disposed) { if (hintLight != null) hintLight.enabled = false; return; }

        bool visible = FuseVisible();
        bool pulled = blownFuse != null && Vector3.Distance(blownFuse.position, fuseStart) > 0.06f;

        // put away with the puller instead of the bin
        if (pulled && !visible && !warned)
        {
            warned = true;
            HazardBridge.Say("[GUIDANCE] Damaged parts go in the DAMAGED PARTS bin beside Workstation 2, not back on the tool board.");
        }

        if (hintLight != null)
        {
            hintLight.enabled = pulled && visible;
            if (hintLight.enabled) hintLight.intensity = 0.6f + 0.5f * Mathf.Sin(Time.time * 6f);
        }

        if (!pulled || !visible) return;
        Vector3 f = blownFuse.position, d = dropPoint.position;
        float flat = Vector2.Distance(new Vector2(f.x, f.z), new Vector2(d.x, d.z));
        if (flat < catchRadius && f.y > d.y - 0.08f && f.y < d.y + 0.5f) Dispose();
    }

    void Dispose()
    {
        disposed = true;

        // a copy of the fuse lies in the bin; the original is hidden (whatever the puller does with it later)
        var copy = new GameObject("DisposedFuse");
        copy.transform.SetParent(transform, true);
        copy.transform.SetPositionAndRotation(dropPoint.position + Vector3.down * 0.12f, Quaternion.Euler(0f, Random.Range(0f, 360f), 90f));
        foreach (var mf in blownFuse.GetComponentsInChildren<MeshFilter>(true))
        {
            var mr = mf.GetComponent<MeshRenderer>();
            if (mr == null || !mr.enabled || mf.sharedMesh == null) continue;
            var g = new GameObject(mf.name);
            g.transform.SetParent(copy.transform, false);
            g.transform.localPosition = blownFuse.InverseTransformPoint(mf.transform.position);
            g.transform.localRotation = Quaternion.Inverse(blownFuse.rotation) * mf.transform.rotation;
            g.transform.localScale = mf.transform.lossyScale;
            g.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
            g.AddComponent<MeshRenderer>().sharedMaterials = mr.sharedMaterials;
        }
        foreach (var r in fuseRenderers) if (r != null) r.enabled = false;
        foreach (var c in blownFuse.GetComponentsInChildren<Collider>(true)) c.enabled = false;

        Sfx.PlayAt(Sfx.Clank, dropPoint.position, 0.5f, 1.9f);
        HazardBridge.Say("Damaged fuse disposed of. Fit the new fuse.");
    }

    // used by the editor set-up to label the bin
    public static TextMeshPro Label(Transform parent, Vector3 localPos, float yRot, string text)
    {
        var go = new GameObject("Label");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.Euler(0f, yRot, 0f);
        var t = go.AddComponent<TextMeshPro>();
        t.text = text;
        t.fontSize = 0.5f;
        t.enableAutoSizing = true; t.fontSizeMin = 0.1f; t.fontSizeMax = 0.5f;
        t.alignment = TextAlignmentOptions.Center;
        t.color = Color.white;
        t.rectTransform.sizeDelta = new Vector2(0.28f, 0.12f);
        return t;
    }
}
