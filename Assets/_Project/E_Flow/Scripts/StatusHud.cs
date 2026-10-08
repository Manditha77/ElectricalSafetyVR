using System.Text;
using TMPro;
using UnityEngine;

// Status panel pinned to the top-right of the view. It is world-space (not a screen overlay),
// so it also works in a headset, and it draws on top of walls so it is never hidden.
public class StatusHud : MonoBehaviour
{
    public Transform head;               // leave empty: uses the main camera
    public float distance = 1.0f;        // metres in front of the eyes
    public float right = 0.38f;          // how far to the right of the centre of view
    public float up = 0.22f;             // how far above the centre of view
    public float followSpeed = 12f;
    public float messageSeconds = 4f;
    public float bannerSeconds = 2.5f;
    public int warningSeconds = 10;      // timer turns red and blinks below this

    static readonly Color Red = new Color(1f, 0.25f, 0.2f);
    static readonly Color Amber = new Color(1f, 0.8f, 0.2f);
    static readonly Color Green = new Color(0.35f, 1f, 0.45f);

    TextMeshPro text;
    string message = "";
    Color messageColor = Color.white;
    float messageUntil;
    string banner = "";
    Color bannerColor = Color.white;
    float bannerUntil;
    bool placed;

    void Start()
    {
        if (head == null && Camera.main != null) head = Camera.main.transform;

        GameObject go = new GameObject("HudText");
        go.transform.SetParent(transform, false);
        text = go.AddComponent<TextMeshPro>();
        text.rectTransform.sizeDelta = new Vector2(0.55f, 0.35f);
        text.fontSize = 0.3f;
        text.alignment = TextAlignmentOptions.TopRight;
        text.outlineWidth = 0.2f;
        text.outlineColor = Color.black;

        // Draw on top of walls and objects, so the panel is never hidden.
        Shader overlay = Shader.Find("TextMeshPro/Distance Field Overlay");
        if (overlay != null) text.fontMaterial.shader = overlay;
    }

    void OnEnable()
    {
        ElectricalSafetyManager.MessageRaised += OnMessage;
        SessionManager.PhaseChanged += OnPhase;
    }

    void OnDisable()
    {
        ElectricalSafetyManager.MessageRaised -= OnMessage;
        SessionManager.PhaseChanged -= OnPhase;
    }

    void OnMessage(string t, bool isUnsafe)
    {
        Show(t, isUnsafe ? Red : Color.white);
    }

    void OnPhase(SessionPhase phase)
    {
        SessionManager s = SessionManager.Instance;
        ElectricalSafetyManager m = ElectricalSafetyManager.Instance;

        if (phase == SessionPhase.PreCheck)
        {
            Banner("CHECK STARTED", Green);
            Show("Put on your PPE and report every hazard.", Color.white);
        }
        else if (phase == SessionPhase.Briefing && s != null && s.PreCheckAttempts > 0)
        {
            bool done = m != null && m.PpeWorn >= m.requiredPpeItems && m.HazardsFound >= m.TotalHazards;
            if (done) Banner("CHECK COMPLETE", Green);
            else Banner("TIME UP", Red);
            Show("Go back to the welcome board.", Amber);
        }
        else if (phase == SessionPhase.Training)
        {
            Banner("TRAINING STARTED", Green);
            Show("Follow the job card.", Color.white);
        }
    }

    void Show(string t, Color c)
    {
        message = t;
        messageColor = c;
        messageUntil = Time.time + messageSeconds;
    }

    void Banner(string t, Color c)
    {
        banner = t;
        bannerColor = c;
        bannerUntil = Time.time + bannerSeconds;
    }

    static string Colorize(string t, Color c)
    {
        return "<color=#" + ColorUtility.ToHtmlStringRGBA(c) + ">" + t + "</color>";
    }

    void LateUpdate()
    {
        if (text == null || head == null || SessionManager.Instance == null) return;
        SessionManager s = SessionManager.Instance;
        ElectricalSafetyManager m = ElectricalSafetyManager.Instance;

        StringBuilder body = new StringBuilder();

        if (Time.time < bannerUntil)
        {
            body.Append("<size=170%><b>").Append(Colorize(banner, bannerColor)).Append("</b></size>\n");
        }
        else if (s.IsPreCheck && m != null)
        {
            int secs = Mathf.CeilToInt(Mathf.Max(0f, s.PreCheckSecondsLeft));
            string clock = (secs / 60) + ":" + (secs % 60).ToString("00");

            Color clockColor = Color.white;
            if (secs <= warningSeconds)
            {
                bool blinkOn = Mathf.FloorToInt(Time.time * 4f) % 2 == 0;   // 2 blinks per second
                clockColor = blinkOn ? Red : new Color(Red.r, Red.g, Red.b, 0.2f);
            }

            body.Append("PRE-WORK CHECK\n");
            body.Append("<size=170%><b>").Append(Colorize(clock, clockColor)).Append("</b></size>\n");
            body.Append("PPE ").Append(m.PpeWorn).Append(" / ").Append(m.requiredPpeItems)
                .Append("    Hazards ").Append(m.HazardsFound).Append(" / ").Append(m.TotalHazards).Append("\n");
        }

        if (Time.time < messageUntil) body.Append(Colorize(message, messageColor));

        string result = body.ToString().TrimEnd('\n');
        text.gameObject.SetActive(result.Length > 0);
        if (result.Length == 0) return;
        text.text = "<mark=#00000099>" + result + "</mark>";

        // Pin to the top-right of the view, with light smoothing.
        Vector3 target = head.position + head.forward * distance + head.right * right + head.up * up;
        transform.position = placed ? Vector3.Lerp(transform.position, target, followSpeed * Time.deltaTime) : target;
        transform.rotation = Quaternion.LookRotation(transform.position - head.position, head.up);
        placed = true;
    }
}