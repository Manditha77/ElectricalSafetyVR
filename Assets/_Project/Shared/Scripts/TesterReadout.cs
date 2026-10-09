using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// Big, easy-to-read copy of the voltage tester's small screen.
// Floats just above the tester, always faces the trainee, colour-coded:
//   230 V -> red "LIVE",  0 V -> green "DEAD",  "OK" on the proving unit -> green "TESTER PROVEN".
// Shows whenever the reading changes, and stays while the tester is held.
public class TesterReadout : MonoBehaviour
{
    [Tooltip("The tester's own small screen text (found automatically if empty)")]
    public TMP_Text source;
    public float height = 0.16f;       // metres above the tester
    public float fontSize = 0.55f;
    public float showSeconds = 5f;     // after a change, when not held

    TextMeshPro label;
    XRGrabInteractable grab;
    string last = "";
    float until;

    static readonly Regex Tags = new Regex("<.*?>");

    void Start()
    {
        if (source == null)
            foreach (var t in GetComponentsInChildren<TMP_Text>(true)) { source = t; break; }
        grab = GetComponentInParent<XRGrabInteractable>();
        if (grab == null) grab = GetComponentInChildren<XRGrabInteractable>();

        var go = new GameObject("TesterReadout");
        label = go.AddComponent<TextMeshPro>();
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = fontSize;
        label.enableAutoSizing = false;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.rectTransform.sizeDelta = new Vector2(0.6f, 0.15f);
        label.outlineWidth = 0.15f;
        label.outlineColor = Color.black;
        go.SetActive(false);

        if (source != null) last = Clean(source.text);
    }

    static string Clean(string s) => string.IsNullOrEmpty(s) ? "" : Tags.Replace(s, "").Replace("\n", " ").Trim();

    void OnDestroy()
    {
        if (label != null) Destroy(label.gameObject);
    }

    void LateUpdate()
    {
        if (source == null || label == null) return;

        string now = Clean(source.text);
        if (now != last) { last = now; until = Time.time + showSeconds; }

        bool held = grab != null && grab.isSelected;
        bool show = now.Length > 0 && (held || Time.time < until);
        if (label.gameObject.activeSelf != show) label.gameObject.SetActive(show);
        if (!show) return;

        string u = now.ToUpperInvariant();
        string txt; Color c;
        if (u.Contains("OK")) { txt = "TESTER PROVEN\n<size=70%>" + now + "</size>"; c = new Color(0.4f, 1f, 0.5f); }
        else if (u.Contains("230")) { txt = now + "  <size=80%>LIVE!</size>"; c = new Color(1f, 0.3f, 0.25f); }
        else if (Regex.IsMatch(u, @"(^|[^0-9])0\s*V")) { txt = now + "  <size=80%>DEAD</size>"; c = new Color(0.4f, 1f, 0.5f); }
        else { txt = now; c = Color.white; }

        label.color = c;
        label.text = "<mark=#000000CC padding=\"14,14,6,6\"><b>" + txt + "</b></mark>";

        label.transform.position = transform.position + Vector3.up * height;
        var cam = Camera.main;
        if (cam != null)
        {
            Vector3 d = label.transform.position - cam.transform.position;
            if (d.sqrMagnitude > 0.0001f) label.transform.rotation = Quaternion.LookRotation(d, Vector3.up);
        }
    }
}
