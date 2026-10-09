using TMPro;
using UnityEngine;
namespace MyJobFlowBackup {

// Two-pole voltage tester: shows the reading on its display and lamp.
public class TesterProbe : MonoBehaviour
{
    public TextMeshPro display;
    public Renderer lamp;
    public float holdSeconds = 4f;

    float clearAt;
    MaterialPropertyBlock block;

    void Start() { Idle(); }

    public void Show(string text, Color color)
    {
        Apply(text, color);
        clearAt = Time.time + holdSeconds;
    }

    void Idle()
    {
        Apply("- - -", new Color(0.5f, 0.5f, 0.5f));
        clearAt = 0f;
    }

    void Apply(string text, Color color)
    {
        if (display != null) { display.text = text; display.color = color; }
        if (lamp != null)
        {
            if (block == null) block = new MaterialPropertyBlock();
            lamp.GetPropertyBlock(block);
            block.SetColor("_BaseColor", color);
            block.SetColor("_EmissionColor", color * 2f);
            lamp.SetPropertyBlock(block);
        }
    }

    void Update()
    {
        if (clearAt > 0f && Time.time > clearAt) Idle();
    }
}
}
