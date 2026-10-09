using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class PpeCompletionAudio : MonoBehaviour
{
    public AudioClip completionClip;

    AudioSource source;
    bool played;

    void Awake()
    {
        source = GetComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
    }

    void Update()
    {
        if (played || completionClip == null) return;

        SessionManager session = SessionManager.Instance;
        ElectricalSafetyManager safety =
            ElectricalSafetyManager.Instance;

        if (session == null || safety == null) return;
        if (!session.IsPreCheck) return;
        if (safety.requiredPpeItems <= 0) return;
        if (safety.PpeWorn < safety.requiredPpeItems) return;

        played = true;
        source.PlayOneShot(completionClip);
    }
}