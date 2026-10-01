using System.Collections.Generic;
using UnityEngine;

namespace TiltBrush {

public class PrimitiveVisualState : MonoBehaviour {
  private const string kWireChildName = "__PrimitiveWireframe";

  public Color Color = Color.white;
  [Range(0.05f, 1f)] public float Alpha = 1f;
  public bool Wireframe;

  public void Apply() {
    foreach (var renderer in GetComponentsInChildren<Renderer>(true)) {
      if (renderer == null || renderer.gameObject.name == kWireChildName) continue;
      foreach (var material in renderer.materials) {
        if (material == null) continue;

        Color c = Color;
        c.a = Alpha;
        if (material.HasProperty("_Color")) material.SetColor("_Color", c);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", c);

        if (Alpha < 0.999f) {
          material.SetOverrideTag("RenderType", "Transparent");
          if (material.HasProperty("_SrcBlend")) {
            material.SetInt("_SrcBlend",
                (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
          }
          if (material.HasProperty("_DstBlend")) {
            material.SetInt("_DstBlend",
                (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
          }
          if (material.HasProperty("_ZWrite")) material.SetInt("_ZWrite", 0);
          material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
          material.renderQueue =
              (int)UnityEngine.Rendering.RenderQueue.Transparent;
        } else {
          if (material.HasProperty("_ZWrite")) material.SetInt("_ZWrite", 1);
          material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
          material.renderQueue = -1;
        }
      }
    }

    RefreshWireframe();
  }

  private void RefreshWireframe() {
    var sources = GetComponentsInChildren<MeshFilter>(true);

    foreach (var source in sources) {
      if (source == null || source.gameObject.name == kWireChildName) continue;

      Transform existing = source.transform.Find(kWireChildName);
      if (!Wireframe) {
        if (existing != null) Destroy(existing.gameObject);
        continue;
      }

      if (source.sharedMesh == null || existing != null) continue;

      Mesh lineMesh = BuildWireMesh(source.sharedMesh);
      if (lineMesh == null) continue;

      var wire = new GameObject(kWireChildName);
      wire.transform.SetParent(source.transform, false);
      wire.layer = source.gameObject.layer;

      var mf = wire.AddComponent<MeshFilter>();
      mf.sharedMesh = lineMesh;

      var mr = wire.AddComponent<MeshRenderer>();
      mr.sharedMaterial = CreateWireMaterial();
      mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
      mr.receiveShadows = false;
    }
  }

  private Material CreateWireMaterial() {
    Shader shader =
        Shader.Find("Universal Render Pipeline/Unlit") ??
        Shader.Find("Unlit/Color") ??
        Shader.Find("Sprites/Default");

    Material material = shader != null
        ? new Material(shader)
        : new Material(GetComponentInChildren<Renderer>().sharedMaterial);

    Color wireColor = Color;
    wireColor.a = Mathf.Max(0.35f, Alpha);

    if (material.HasProperty("_BaseColor")) {
      material.SetColor("_BaseColor", wireColor);
    }
    if (material.HasProperty("_Color")) {
      material.SetColor("_Color", wireColor);
    }
    if (material.HasProperty("_ZWrite")) material.SetInt("_ZWrite", 0);
    material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 20;
    return material;
  }

  private static Mesh BuildWireMesh(Mesh source) {
    int[] triangles = source.triangles;
    if (triangles == null || triangles.Length < 3) return null;

    var edges = new HashSet<ulong>();
    var indices = new List<int>();

    void AddEdge(int a, int b) {
      uint lo = (uint)Mathf.Min(a, b);
      uint hi = (uint)Mathf.Max(a, b);
      ulong key = ((ulong)lo << 32) | hi;
      if (edges.Add(key)) {
        indices.Add(a);
        indices.Add(b);
      }
    }

    for (int i = 0; i + 2 < triangles.Length; i += 3) {
      int a = triangles[i];
      int b = triangles[i + 1];
      int c = triangles[i + 2];
      AddEdge(a, b);
      AddEdge(b, c);
      AddEdge(c, a);
    }

    var mesh = new Mesh {
      name = source.name + "_Wireframe"
    };
    if (source.vertexCount > 65535) {
      mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
    }
    mesh.vertices = source.vertices;
    mesh.SetIndices(indices, MeshTopology.Lines, 0, false);
    mesh.RecalculateBounds();
    return mesh;
  }

  private void OnDestroy() {
    foreach (var filter in GetComponentsInChildren<MeshFilter>(true)) {
      if (filter != null &&
          filter.gameObject.name == kWireChildName &&
          filter.sharedMesh != null) {
        Destroy(filter.sharedMesh);
      }
    }
  }

  public static PrimitiveVisualState GetOrCreate(StencilWidget widget) {
    if (widget == null) return null;
    var state = widget.GetComponent<PrimitiveVisualState>();
    if (state == null) {
      state = widget.gameObject.AddComponent<PrimitiveVisualState>();
    }
    return state;
  }
}

} // namespace TiltBrush
