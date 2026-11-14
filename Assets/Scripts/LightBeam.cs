using UnityEngine;

/// Builds a see-through cone mesh that fakes a visible spotlight beam.
/// Put it on an empty GameObject at the spotlight's position (rotation 0,0,0).
/// The cone points straight down: narrow at the top, wide at the bottom.
/// Needs a transparent additive material (URP Particles/Unlit) on the Mesh Renderer.
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class LightBeam : MonoBehaviour
{
    public float height = 6f;            // distance from the light down to the table
    public float topRadius = 0.1f;       // width at the lamp
    public float bottomRadius = 2.8f;    // width on the table: height * tan(outer spot angle / 2)
    public int segments = 48;
    public Color color = new Color(1f, 0.95f, 0.69f);   // light yellow, like the StageLight
    [Range(0f, 1f)] public float topAlpha = 0.35f;      // brightness near the lamp
    [Range(0f, 1f)] public float bottomAlpha = 0.03f;   // brightness near the table

    void Start() => Build();

    void Build()
    {
        var mesh = new Mesh { name = "LightBeam" };
        var verts = new Vector3[(segments + 1) * 2];
        var cols = new Color[verts.Length];
        var uvs = new Vector2[verts.Length];
        var tris = new int[segments * 6];

        Color top = color; top.a = topAlpha;
        Color bottom = color; bottom.a = bottomAlpha;

        for (int i = 0; i <= segments; i++)
        {
            float a = i / (float)segments * Mathf.PI * 2f;
            Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            verts[i * 2] = dir * topRadius;
            verts[i * 2 + 1] = dir * bottomRadius + Vector3.down * height;
            cols[i * 2] = top;
            cols[i * 2 + 1] = bottom;
            uvs[i * 2] = new Vector2(i / (float)segments, 1f);
            uvs[i * 2 + 1] = new Vector2(i / (float)segments, 0f);
        }

        for (int i = 0; i < segments; i++)
        {
            int v = i * 2, t = i * 6;
            tris[t] = v; tris[t + 1] = v + 2; tris[t + 2] = v + 1;
            tris[t + 3] = v + 1; tris[t + 4] = v + 2; tris[t + 5] = v + 3;
        }

        mesh.vertices = verts;
        mesh.colors = cols;     // the fade from bright top to faint bottom
        mesh.uv = uvs;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        GetComponent<MeshFilter>().mesh = mesh;
    }
}