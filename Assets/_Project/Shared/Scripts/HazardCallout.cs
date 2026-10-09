using TMPro;
using UnityEngine;

// Floating risk card next to a hazard. Hidden until the trainee spots the hazard
// (no glow or hint before that: finding it is the trainee's job).
public class HazardCallout : MonoBehaviour
{
    public Transform card;
    public Renderer bar;
    public TextMeshPro header;
    public TextMeshPro body;
    public float showControlledFor = 5f;

    static readonly Color Amber = new Color(1f, 0.7f, 0.05f);
    static readonly Color Red = new Color(0.82f, 0.13f, 0.1f);
    static readonly Color Green = new Color(0.18f, 0.62f, 0.28f);

    MaterialPropertyBlock mpb;
    float scale, target, hideAt = -1f;
    string baseBody = "";

    void Awake()
    {
        mpb = new MaterialPropertyBlock();
        HideImmediate();
    }

    public void HideImmediate()
    {
        scale = target = 0f;
        hideAt = -1f;
        if (card != null)
        {
            card.localScale = Vector3.zero;
            card.gameObject.SetActive(false);
        }
    }

    void SetBar(Color c, Color text)
    {
        if (bar != null)
        {
            bar.GetPropertyBlock(mpb);
            mpb.SetColor("_BaseColor", c);
            mpb.SetColor("_Color", c);
            bar.SetPropertyBlock(mpb);
        }
        if (header != null) header.color = text;
    }

    public void ShowSpotted(HazardLevel level, string title, string risk, string control)
    {
        bool danger = level == HazardLevel.Danger;
        SetBar(danger ? Red : Amber, danger ? Color.white : Color.black);
        header.text = "<b>" + (danger ? "DANGER" : "CAUTION") + "</b>   " + title;
        baseBody = "<color=#FF8A80><b>RISK</b></color>   " + risk +
                   "\n<color=#80D8FF><b>MAKE IT SAFE</b></color>   " + control;
        body.text = baseBody;
        Show(-1f);
    }

    public void ShowProgress(string text)
    {
        if (string.IsNullOrEmpty(baseBody)) return;
        body.text = baseBody + "\n<color=#FFFFFF><b>" + text + "</b></color>";
        Show(-1f);
    }

    public void ShowControlled(string text, bool late)
    {
        SetBar(late ? Amber : Green, late ? Color.black : Color.white);
        header.text = late ? "<b>MADE SAFE (LATE)</b>" : "<b>MADE SAFE</b>";
        body.text = text + (late
            ? "\n<color=#FFD180>Found during the job. It should be found in the pre-work check.</color>"
            : "");
        baseBody = "";
        Show(showControlledFor);
    }

    // Time up: a hazard the trainee missed
    public void ShowMissed(HazardLevel level, string title, string risk, string control)
    {
        SetBar(Red, Color.white);
        header.text = "<b>MISSED</b>   " + title;
        baseBody = "<color=#FF8A80><b>RISK</b></color>   " + risk +
                   "\n<color=#80D8FF><b>SHOULD HAVE</b></color>   " + control;
        body.text = baseBody;
        Show(-1f);
    }

    // After the review: the missed hazard has been made safe for the trainee
    public void ShowMadeSafeForYou(string text)
    {
        SetBar(Amber, Color.black);
        header.text = "<b>MADE SAFE FOR YOU</b>";
        body.text = text + "\n<color=#FFD180>You missed this one. It is recorded in your result.</color>";
        baseBody = "";
        Show(showControlledFor);
    }

    void Show(float seconds)
    {
        if (card == null) return;
        card.gameObject.SetActive(true);
        target = 1f;
        hideAt = seconds > 0f ? Time.time + seconds : -1f;
    }

    void LateUpdate()
    {
        if (card == null) return;
        if (target > 0f && HazardBridge.Phase == "Result") target = 0f;
        if (hideAt > 0f && Time.time > hideAt) { target = 0f; hideAt = -1f; }

        scale = Mathf.MoveTowards(scale, target, Time.deltaTime * 4f);
        card.localScale = Vector3.one * EaseOutBack(scale);

        if (scale <= 0f && target <= 0f)
        {
            if (card.gameObject.activeSelf) card.gameObject.SetActive(false);
            return;
        }

        var cam = Camera.main;
        if (cam != null)
        {
            Vector3 d = card.position - cam.transform.position;
            d.y = 0f;
            if (d.sqrMagnitude > 0.0001f) card.rotation = Quaternion.LookRotation(d);
        }
    }

    static float EaseOutBack(float x)
    {
        if (x <= 0f) return 0f;
        const float c1 = 1.4f, c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
    }
}
