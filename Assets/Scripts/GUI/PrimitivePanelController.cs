using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TiltBrush {

/// <summary>
/// Converts a cloned GuideToolsPanel into a dedicated primitives panel.
/// Uses native StencilWidget/CreateWidgetCommand flow so primitives remain
/// grabbable, scalable, undoable and saved with the sketch.
/// </summary>
public class PrimitivePanelController : MonoBehaviour {
  private static readonly (StencilType type, string label)[] kPrimitives = {
    (StencilType.Cube, "Cube"),
    (StencilType.Sphere, "Sphere"),
    (StencilType.Cylinder, "Cylinder"),
    (StencilType.Cone, "Cone"),
    (StencilType.Pyramid, "Pyramid"),
    (StencilType.Plane, "Plane"),
    (StencilType.Capsule, "Capsule"),
    (StencilType.Ellipsoid, "Ellipsoid"),
    (StencilType.InteriorDome, "Dome"),
  };

  public void Configure() {
    var panel = GetComponent<BasePanel>();
    panel?.SetRuntimePanelDescription("PRIMITIVES");

    var allButtons = GetComponentsInChildren<BaseButton>(true).ToList();
    var stencilButtons = GetComponentsInChildren<StencilButton>(true).ToList();

    // Hide non-primitive controls copied from GuideToolsPanel.
    foreach (var button in allButtons) {
      if (button is not StencilButton) {
        button.gameObject.SetActive(false);
      }
    }

    if (stencilButtons.Count == 0) {
      Debug.LogWarning("[Primitives] No stencil-button template found.");
      return;
    }

    // Reuse the native button objects first.
    var buttons = new List<StencilButton>();
    buttons.AddRange(stencilButtons);

    // Clone enough native buttons to fill a 3x3 grid.
    StencilButton template = stencilButtons[0];
    while (buttons.Count < kPrimitives.Length) {
      var clone = Instantiate(template.gameObject, template.transform.parent);
      clone.name = "PrimitiveButton_Runtime";
      var button = clone.GetComponent<StencilButton>();
      buttons.Add(button);
    }

    // Remove extras in case the source panel grows in the future.
    for (int i = kPrimitives.Length; i < buttons.Count; ++i) {
      buttons[i].gameObject.SetActive(false);
    }

    // 3 x 3 layout in the same coordinate system as GuideToolsPanel.
    const float spacingX = 0.48f;
    const float spacingY = 0.46f;
    Vector3 start = new Vector3(-0.48f, 0.46f, 0.05f);

    for (int i = 0; i < kPrimitives.Length; ++i) {
      var button = buttons[i];
      int row = i / 3;
      int col = i % 3;

      button.gameObject.SetActive(true);
      button.transform.localPosition = start +
          new Vector3(col * spacingX, -row * spacingY, 0f);
      button.transform.localRotation = Quaternion.identity;
      button.transform.localScale = Vector3.one * 0.32f;
      button.Configure(kPrimitives[i].type, kPrimitives[i].label);
    }

    Debug.Log("[Primitives] Primitive panel configured with " +
              kPrimitives.Length + " native Open Brush shapes.");
  }
}

} // namespace TiltBrush
