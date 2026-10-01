using System;
using OpenBrush.Multiplayer;
using UnityEngine;

namespace TiltBrush {

public class PrimitiveVisualState : MonoBehaviour {
  public Color Color = Color.white;
  [Range(0.05f, 1f)] public float Alpha = 1f;
  public bool Wireframe;

  public void Apply() {
    foreach (var renderer in GetComponentsInChildren<Renderer>(true)) {
      if (renderer == null) continue;
      foreach (var material in renderer.materials) {
        if (material == null) continue;

        Color c = Color;
        c.a = Alpha;
        if (material.HasProperty("_Color")) material.SetColor("_Color", c);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", c);

        if (material.HasProperty("_Wireframe")) {
          material.SetFloat("_Wireframe", Wireframe ? 1f : 0f);
        }
        if (Wireframe) material.EnableKeyword("WIREFRAME_ON");
        else material.DisableKeyword("WIREFRAME_ON");

        if (Alpha < 0.999f) {
          material.SetOverrideTag("RenderType", "Transparent");
          material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
          material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
          material.SetInt("_ZWrite", 0);
          material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
          material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        } else {
          material.SetInt("_ZWrite", 1);
          material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
          material.renderQueue = -1;
        }
      }
    }
  }

  public static PrimitiveVisualState GetOrCreate(StencilWidget widget) {
    if (widget == null) return null;
    var state = widget.GetComponent<PrimitiveVisualState>();
    if (state == null) state = widget.gameObject.AddComponent<PrimitiveVisualState>();
    return state;
  }
}

} // namespace TiltBrush
