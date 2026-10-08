using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// Shows a small label above a held PPE item: what it is, when to release it,
// and the result after it is worn. Goes on the WearZone next to PpeLocker.
public class PpeFeedback : MonoBehaviour
{
    public Transform head;                 // leave empty: uses the main camera
    public float messageSeconds = 2.5f;

    static readonly Color Neutral = Color.white;
    static readonly Color Good = new Color(0.3f, 1f, 0.4f);
    static readonly Color Warning = new Color(1f, 0.6f, 0.1f);

    Collider zone;
    TextMeshPro label;
    PpeItem[] items;

    Vector3 lastPosition;
    string message;
    Color messageColor;
    float messageUntil;

    void Start()
    {
        if (head == null && Camera.main != null) head = Camera.main.transform;
        zone = GetComponent<Collider>();
        items = FindObjectsByType<PpeItem>(FindObjectsSortMode.None);

        label = new GameObject("PpeHint").AddComponent<TextMeshPro>();
        label.rectTransform.sizeDelta = new Vector2(0.6f, 0.2f);
        label.fontSize = 0.4f;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.outlineWidth = 0.2f;
        label.outlineColor = Color.black;
        label.gameObject.SetActive(false);
    }

    void OnEnable()  { ElectricalSafetyManager.MessageRaised += OnMessage; }
    void OnDisable() { ElectricalSafetyManager.MessageRaised -= OnMessage; }

    // Only PPE messages are shown on the item label.
    void OnMessage(string text, bool isUnsafe)
    {
        bool worn = text.StartsWith("PPE on");
        bool rejected = text.Contains("not suitable");
        if (!worn && !rejected) return;

        message = text;
        messageColor = worn ? Good : Warning;
        messageUntil = Time.time + messageSeconds;
    }

    void Update()
    {
        if (label == null || head == null) return;

        PpeItem held = HeldItem();
        if (held != null)
        {
            Vector3 p = held.transform.position;
            bool inZone = zone != null && (zone.ClosestPoint(p) - p).sqrMagnitude < 0.0001f;

            label.text = held.itemName + "\n" +
                         (inZone ? "Release grip to put it on" : "Hold it to your body to put it on");
            label.color = inZone ? Good : Neutral;
            lastPosition = p + Vector3.up * 0.18f;
            Show(lastPosition);
        }
        else if (Time.time < messageUntil)
        {
            label.text = message;
            label.color = messageColor;
            Show(lastPosition);
        }
        else
        {
            label.gameObject.SetActive(false);
        }
    }

    PpeItem HeldItem()
    {
        foreach (PpeItem item in items)
        {
            if (item == null || !item.gameObject.activeInHierarchy) continue;
            XRGrabInteractable grab = item.GetComponent<XRGrabInteractable>();
            if (grab != null && grab.isSelected) return item;
        }
        return null;
    }

    void Show(Vector3 position)
    {
        label.gameObject.SetActive(true);
        label.transform.position = position;
        label.transform.rotation = Quaternion.LookRotation(position - head.position);
    }
}