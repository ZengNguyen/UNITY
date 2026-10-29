using System;
using UnityEngine;

public static class NoiseSystem
{
    public static event Action<Vector2, float> OnNoise;
    public static bool showDebugRing = true;

    public static void Emit(Vector2 position, float radius)
    {
        OnNoise?.Invoke(position, radius);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (showDebugRing) NoiseRing.Spawn(position, radius);
#endif
    }
}

#if UNITY_EDITOR || DEVELOPMENT_BUILD
// Vòng tròn vàng cho biết tiếng động lan tới đâu (chỉ để debug)
public class NoiseRing : MonoBehaviour
{
    private const int Segments = 48;
    private const float Duration = 0.7f;
    private LineRenderer lr;
    private Material mat;
    private float maxRadius;
    private float t;

    public static void Spawn(Vector2 position, float radius)
    {
        GameObject go = new GameObject("NoiseRing");
        go.transform.position = position;
        NoiseRing ring = go.AddComponent<NoiseRing>();
        ring.maxRadius = radius;
        ring.Init();
    }

    private void Init()
    {
        lr = gameObject.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.loop = true;
        lr.positionCount = Segments;
        lr.widthMultiplier = 0.06f;
        mat = new Material(Shader.Find("Sprites/Default"));
        lr.material = mat;
        lr.sortingOrder = 100;
    }

    private void Update()
    {
        t += Time.deltaTime;
        float k = t / Duration;
        if (k >= 1f) { Destroy(gameObject); return; }

        float r = Mathf.Lerp(0.2f, maxRadius, k);
        for (int i = 0; i < Segments; i++)
        {
            float a = (i / (float)Segments) * Mathf.PI * 2f;
            lr.SetPosition(i, new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f));
        }
        Color c = new Color(1f, 0.9f, 0.2f, 1f - k);
        lr.startColor = c;
        lr.endColor = c;
    }

    private void OnDestroy()
    {
        if (mat != null) Destroy(mat);
    }
}
#endif