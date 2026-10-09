using UnityEngine;

// Footsteps from the player's head movement.
// Normal shoes before the safety boots are worn, heavier boot steps after. Splashy steps in the spill.
public class FootstepAudio : MonoBehaviour
{
    public Transform head;
    [Tooltip("The safety boots PPE item. Worn = hidden/deactivated/attached to the player.")]
    public GameObject bootsItem;
    public Hazard puddle;
    public float stride = 0.65f;
    public float volume = 0.45f;

    Vector3 last;
    float travelled;
    int index;
    Bounds spill;
    bool hasSpill;

    void Start()
    {
        if (head == null && Camera.main != null) head = Camera.main.transform;
        if (head != null) last = head.position;
        if (puddle == null) puddle = Hazard.Find("Puddle");
        if (puddle != null)
        {
            var c = puddle.GetComponent<Collider>();
            if (c != null) { spill = c.bounds; hasSpill = true; }
        }
    }

    bool BootsOn
    {
        get
        {
            if (bootsItem == null) return false;
            if (!bootsItem.activeInHierarchy) return true;
            if (head != null && bootsItem.transform.IsChildOf(head.root)) return true;
            foreach (var r in bootsItem.GetComponentsInChildren<Renderer>()) if (r.enabled) return false;
            return true;
        }
    }

    void Update()
    {
        if (head == null) return;
        Vector3 p = head.position;
        Vector3 d = p - last; d.y = 0f;
        last = p;
        float m = d.magnitude;
        if (m > 0.8f) { travelled = 0f; return; } // teleport / snap
        travelled += m;
        if (travelled < stride) return;
        travelled = 0f;

        Vector3 foot = new Vector3(p.x, 0.03f, p.z);
        bool wet = hasSpill && puddle != null && !puddle.Controlled &&
                   p.x > spill.min.x && p.x < spill.max.x && p.z > spill.min.z && p.z < spill.max.z;
        bool boots = BootsOn;
        Sfx.PlayAt(Sfx.Step(boots, index++), foot, boots ? volume * 1.2f : volume, Random.Range(0.92f, 1.08f));
        if (wet) Sfx.PlayAt(Sfx.Splash, foot, 0.35f, Random.Range(1.2f, 1.5f));
    }
}
