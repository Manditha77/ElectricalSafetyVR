using UnityEngine;

public enum ConsequenceType { SlipOnSpill, LiveDamagedLead, OverheatingStrip, ToolLeftOnMachine }

// What happens during the job if a hazard was NOT made safe in the pre-work check.
// Each one fires once, is logged as unsafe and fails the "Hazards" decision.
public class HazardConsequence : MonoBehaviour
{
    public Hazard hazard;
    public ConsequenceType type;

    [Header("Effects (optional)")]
    public ParticleSystem sparks;
    public ParticleSystem smoke;
    public Light glow;
    public AudioSource loop;

    [Header("Settings")]
    public LooseToolControl tool;
    public float nearRadius = 1.0f;
    public float overheatAfter = 25f;

    bool fired;
    float trainingTime, nextSpark, flashUntil;
    string lastState = "";
    Bounds spill;

    void Start()
    {
        if (glow != null) glow.intensity = 0f;
        if (loop != null)
        {
            loop.clip = Sfx.Fizz;
            loop.loop = true;
            loop.volume = 0f;
            loop.spatialBlend = 1f;
            loop.Play();
        }
        if (type == ConsequenceType.SlipOnSpill && hazard != null)
        {
            var c = hazard.GetComponent<Collider>();
            spill = c != null ? c.bounds : new Bounds(hazard.transform.position, new Vector3(1.2f, 0.4f, 0.8f));
        }
        nextSpark = Time.time + 1f;
    }

    void Update()
    {
        if (hazard == null) return;

        bool live = !hazard.Controlled;
        string phase = HazardBridge.Phase;
        bool active = phase == "PreCheck" || phase == "Training";
        bool training = phase == "Training";
        if (training) trainingTime += Time.deltaTime;

        Transform cam = Camera.main != null ? Camera.main.transform : null;

        switch (type)
        {
            case ConsequenceType.LiveDamagedLead:
                // Intermittent arcing at the damaged spot (also a clue during the check). Max 1 flash per 2 s.
                if (live && active && Time.time > nextSpark)
                {
                    Vector3 at = sparks != null ? sparks.transform.position : transform.position;
                    if (sparks != null) sparks.Emit(Random.Range(10, 24));
                    Sfx.PlayAt(Sfx.Crackle, at, 0.7f);
                    flashUntil = Time.time + 0.07f;
                    nextSpark = Time.time + Random.Range(2f, 4.5f);
                }
                if (glow != null) glow.intensity = Time.time < flashUntil ? 1.5f : 0f;

                if (live && training && !fired && cam != null &&
                    HazardBridge.FlatDistance(cam.position, transform.position) < nearRadius)
                    Fire("UNSAFE: You worked right next to a damaged lead that was still plugged in and live. Isolating W2 does not make a wall socket dead.",
                         "Still live and sparking when you worked next to it");
                break;

            case ConsequenceType.OverheatingStrip:
                float heat = 0f;
                if (live && phase == "PreCheck") heat = 0.15f;                 // faint warm smell, a wisp of smoke
                else if (live && training)
                    heat = trainingTime < overheatAfter ? 0.15f + 0.3f * trainingTime / overheatAfter : 1f;

                if (smoke != null)
                {
                    var em = smoke.emission;
                    em.rateOverTime = heat < 0.5f ? heat * 8f : heat * 16f;
                }
                if (loop != null) loop.volume = Mathf.MoveTowards(loop.volume, heat * 0.55f, Time.deltaTime * 0.5f);
                if (glow != null)
                    glow.intensity = heat >= 1f ? 0.5f + 0.5f * Mathf.PerlinNoise(Time.time * 2f, 0.3f) : heat * 0.4f;

                if (live && training && heat >= 1f && !fired)
                    Fire("UNSAFE: The overloaded power strip overheated and started smoking. Fire risk. Never overload or daisy-chain power strips.",
                         "Overheated and smoked during the job");
                break;

            case ConsequenceType.SlipOnSpill:
                if (live && training && !fired && cam != null)
                {
                    Vector3 p = cam.position;
                    if (p.x > spill.min.x - 0.1f && p.x < spill.max.x + 0.1f &&
                        p.z > spill.min.z - 0.1f && p.z < spill.max.z + 0.1f)
                    {
                        Sfx.PlayAt(Sfx.Splash, new Vector3(p.x, 0.05f, p.z), 1f);
                        Fire("UNSAFE: You stood in the spill while working on 230 V equipment. Slip risk, and water near live parts. Clean spills before work starts.",
                             "You stood in it while working on W2");
                    }
                }
                break;

            case ConsequenceType.ToolLeftOnMachine:
                string st = HazardBridge.State;
                if (st != lastState)
                {
                    if (st == "Restored" && training && live && !fired && tool != null && tool.OnMachine)
                        Fire("UNSAFE: Workstation 2 was switched back on with a loose metal spanner still on it. Remove all tools before restoring the supply.",
                             "Still on W2 when the power was restored");
                    lastState = st;
                }
                break;
        }
    }

    void Fire(string message, string outcome)
    {
        fired = true;
        hazard.Outcome = outcome;
        HazardBridge.Say(message);
        HazardBridge.AddUnsafe(message.Replace("UNSAFE: ", ""));
        HazardBridge.FailDecision("Hazards");
        var cam = Camera.main;
        if (cam != null) Sfx.PlayAt(Sfx.Buzz, cam.transform.position + cam.transform.forward * 0.5f, 0.6f);
    }
}
