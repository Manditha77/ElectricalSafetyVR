using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// Entrance door: point at it and press grip (G) to open; it swings shut again by itself
// (a self-closing fire door). Press again to close it early. Works in every phase.
// It rotates the door parts around the hinge line in world space, so nothing has to be
// re-parented (works even when the door is inside a prefab).
[RequireComponent(typeof(XRSimpleInteractable))]
public class SwingDoor : MonoBehaviour
{
    [Tooltip("Door panel, handle, kick plate, window... (this grab box is moved too)")]
    public Transform[] parts;
    public Vector3 hingePoint;       // world position on the hinge line
    public Transform handle;         // for sound position (optional)
    public float openAngle = 95f;    // signed: direction into the room
    public float openSpeed = 110f;   // degrees per second
    public float closeSpeed = 70f;
    public float autoCloseAfter = 5f;

    XRSimpleInteractable simple;
    Vector3[] pos0;
    Quaternion[] rot0;
    Vector3 selfPos0;
    Quaternion selfRot0;
    float angle, target, closeAt = -1f;
    bool wasClosed = true;

    void Awake()
    {
        int n = parts != null ? parts.Length : 0;
        pos0 = new Vector3[n]; rot0 = new Quaternion[n];
        for (int i = 0; i < n; i++)
            if (parts[i] != null) { pos0[i] = parts[i].position; rot0[i] = parts[i].rotation; }
        selfPos0 = transform.position; selfRot0 = transform.rotation;

        simple = GetComponent<XRSimpleInteractable>();
        simple.selectEntered.AddListener(OnSelect);
    }

    void OnSelect(SelectEnterEventArgs args)
    {
        if (Mathf.Abs(target) < 1f) Open();
        else Close();
    }

    Vector3 SoundPos => handle != null ? handle.position : transform.position;

    void Open()
    {
        target = openAngle;
        closeAt = Time.time + autoCloseAfter;
        Sfx.PlayAt(Sfx.Latch, SoundPos, 0.8f);
        Sfx.PlayAt(Sfx.Creak, hingePoint + Vector3.up, 0.5f, Random.Range(0.9f, 1.1f));
    }

    void Close()
    {
        target = 0f;
        closeAt = -1f;
        Sfx.PlayAt(Sfx.Creak, hingePoint + Vector3.up, 0.35f, Random.Range(0.75f, 0.9f));
    }

    void Update()
    {
        if (closeAt > 0f && Time.time > closeAt) Close();

        float before = angle;
        float speed = Mathf.Abs(target) > Mathf.Abs(angle) ? openSpeed : closeSpeed;
        angle = Mathf.MoveTowards(angle, target, speed * Time.deltaTime);

        if (!Mathf.Approximately(before, angle))
        {
            Quaternion q = Quaternion.Euler(0f, angle, 0f);
            for (int i = 0; i < pos0.Length; i++)
            {
                if (parts[i] == null) continue;
                parts[i].SetPositionAndRotation(hingePoint + q * (pos0[i] - hingePoint), q * rot0[i]);
            }
            transform.SetPositionAndRotation(hingePoint + q * (selfPos0 - hingePoint), q * selfRot0);
        }

        bool closed = Mathf.Abs(angle) < 0.5f;
        if (closed && !wasClosed)
        {
            Sfx.PlayAt(Sfx.DoorShut, SoundPos, 0.8f);
            Sfx.PlayAt(Sfx.Latch, SoundPos, 0.6f);
        }
        wasClosed = closed;
    }
}