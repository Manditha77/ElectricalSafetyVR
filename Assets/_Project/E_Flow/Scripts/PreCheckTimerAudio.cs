using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class PreCheckTimerAudio : MonoBehaviour
{
    public AudioClip timeOverClip;

    [Range(0f, 1f)]
    public float beepVolume = 0.6f;

    AudioSource source;
    AudioClip beepClip;
    int lastSecond = -1;

    void Awake()
    {
        source = GetComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;

        // Generate a short beep, so no separate beep file is needed.
        const int sampleRate = 44100;
        const float duration = 0.12f;
        const float frequency = 880f;

        int sampleCount = Mathf.RoundToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = i / (float)sampleRate;

            // Fade the edges to avoid audio clicks.
            float fadeIn = Mathf.Clamp01(t / 0.01f);
            float fadeOut = Mathf.Clamp01((duration - t) / 0.02f);

            samples[i] =
                Mathf.Sin(2f * Mathf.PI * frequency * t)
                * fadeIn * fadeOut * 0.5f;
        }

        beepClip = AudioClip.Create(
            "CountdownBeep", sampleCount, 1, sampleRate, false);

        beepClip.SetData(samples, 0);
    }

    void OnEnable()
    {
        SessionManager.PhaseChanged += OnPhaseChanged;
        SessionManager.PreCheckTimedOut += OnTimeOver;
    }

    void OnDisable()
    {
        SessionManager.PhaseChanged -= OnPhaseChanged;
        SessionManager.PreCheckTimedOut -= OnTimeOver;

        if (source != null)
            source.Stop();
    }

    void Update()
    {
        SessionManager session = SessionManager.Instance;

        if (session == null || !session.IsPreCheck)
            return;

        int secondsLeft =
            Mathf.CeilToInt(session.PreCheckSecondsLeft);

        if (secondsLeft >= 1 && secondsLeft <= 10 &&
            secondsLeft != lastSecond)
        {
            lastSecond = secondsLeft;
            source.PlayOneShot(beepClip, beepVolume);
        }
    }

    void OnPhaseChanged(SessionPhase phase)
    {
        if (phase == SessionPhase.PreCheck)
        {
            lastSecond = -1;
            source.Stop();
        }
        else
        {
            // Stop countdown audio when the check ends early.
            source.Stop();
        }
    }

    void OnTimeOver()
    {
        source.Stop();

        if (timeOverClip != null)
            source.PlayOneShot(timeOverClip);
    }

    void OnDestroy()
    {
        if (beepClip != null)
            Destroy(beepClip);
    }
}