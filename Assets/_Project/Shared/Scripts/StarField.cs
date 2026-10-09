using UnityEngine;

// Night sky stars: a fixed dome of tiny particles, very slow twinkle. No textures needed.
[RequireComponent(typeof(ParticleSystem))]
public class StarField : MonoBehaviour
{
    public int count = 1400;
    public float radius = 300f;
    public int seed = 7;

    ParticleSystem ps;
    ParticleSystem.Particle[] stars;
    float[] phase;
    float nextTwinkle;

    void Start()
    {
        ps = GetComponent<ParticleSystem>();
        var rnd = new System.Random(seed);
        stars = new ParticleSystem.Particle[count];
        phase = new float[count];
        for (int i = 0; i < count; i++)
        {
            // upper hemisphere, more stars higher up
            float u = (float)rnd.NextDouble(), v = (float)rnd.NextDouble();
            float elev = Mathf.Asin(Mathf.Lerp(0.04f, 1f, Mathf.Sqrt(u)));
            float az = v * Mathf.PI * 2f;
            Vector3 dir = new Vector3(Mathf.Cos(elev) * Mathf.Cos(az), Mathf.Sin(elev), Mathf.Cos(elev) * Mathf.Sin(az));
            float big = (float)rnd.NextDouble();
            stars[i].position = transform.position + dir * radius;
            stars[i].startSize = big > 0.97f ? 2.6f : Mathf.Lerp(0.7f, 1.6f, (float)rnd.NextDouble());
            float warm = (float)rnd.NextDouble();
            stars[i].startColor = Color.Lerp(new Color(0.75f, 0.85f, 1f), new Color(1f, 0.92f, 0.8f), warm);
            stars[i].remainingLifetime = 1e6f;
            stars[i].startLifetime = 1e6f;
            phase[i] = (float)rnd.NextDouble() * 10f;
        }
        ps.Play();
        ps.SetParticles(stars, count);
    }

    void Update()
    {
        if (stars == null || Time.time < nextTwinkle) return;
        nextTwinkle = Time.time + 0.15f;
        for (int i = 0; i < count; i += 3)
        {
            float a = 0.65f + 0.35f * Mathf.Sin(Time.time * 1.3f + phase[i]);
            var c = (Color)stars[i].startColor; c.a = a;
            stars[i].startColor = c;
        }
        ps.SetParticles(stars, count);
    }
}
