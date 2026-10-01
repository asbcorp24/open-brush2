using System;
using System.Collections.Generic;
using System.Linq;
using OpenBrush.Multiplayer;
using UnityEngine;

namespace TiltBrush {

public class PrimitivePropertiesPanelController : MonoBehaviour {
  private static readonly Color[] kPalette = {
    Color.white,
    new Color(0.25f, 0.65f, 1f, 1f),
    new Color(1f, 0.35f, 0.3f, 1f),
    new Color(0.35f, 1f, 0.5f, 1f),
    new Color(1f, 0.85f, 0.25f, 1f),
    new Color(0.8f, 0.35f, 1f, 1f),
    new Color(0.15f, 0.15f, 0.15f, 1f),
  };

  private static readonly (PrimitivePropertyAction action, string label)[] kActions = {
    (PrimitivePropertyAction.SizeXMinus, "X -"),
    (PrimitivePropertyAction.SizeXPlus, "X +"),
    (PrimitivePropertyAction.SizeYMinus, "Y -"),
    (PrimitivePropertyAction.SizeYPlus, "Y +"),
    (PrimitivePropertyAction.SizeZMinus, "Z -"),
    (PrimitivePropertyAction.SizeZPlus, "Z +"),
    (PrimitivePropertyAction.Duplicate, "Duplicate"),
    (PrimitivePropertyAction.SnapToGrid, "Snap to grid"),
    (PrimitivePropertyAction.ColorNext, "Next color"),
    (PrimitivePropertyAction.AlphaNext, "Transparency"),
    (PrimitivePropertyAction.ToggleWireframe, "Wireframe"),
  };

  public void Configure() {
    GetComponent<BasePanel>()?.SetRuntimePanelDescription("PRIMITIVE PROPERTIES");

    var allButtons = GetComponentsInChildren<BaseButton>(true).ToList();
    StencilButton template = GetComponentsInChildren<StencilButton>(true).FirstOrDefault();
    if (template == null) {
      Debug.LogWarning("[Primitive Properties] No button template found.");
      return;
    }

    foreach (var button in allButtons) {
      button.gameObject.SetActive(false);
    }

    var buttons = new List<PrimitivePropertyButton>();
    for (int i = 0; i < kActions.Length; ++i) {
      var clone = Instantiate(template.gameObject, template.transform.parent);
      clone.SetActive(false);

      var stencil = clone.GetComponent<StencilButton>();
      if (stencil != null) stencil.enabled = false;

      var actionButton = clone.AddComponent<PrimitivePropertyButton>();
      actionButton.Configure(kActions[i].action, kActions[i].label);
      buttons.Add(actionButton);
      clone.SetActive(true);
    }

    const float spacingX = 0.40f;
    const float spacingY = 0.42f;
    Vector3 start = new Vector3(-0.60f, 0.48f, 0.05f);

    for (int i = 0; i < buttons.Count; ++i) {
      int row = i / 4;
      int col = i % 4;
      buttons[i].transform.localPosition = start +
          new Vector3(col * spacingX, -row * spacingY, 0f);
      buttons[i].transform.localRotation = Quaternion.identity;
      buttons[i].transform.localScale = Vector3.one * 0.28f;
    }
  }

  private static StencilWidget SelectedPrimitive {
    get {
      if (SelectionManager.m_Instance == null) return null;
      return SelectionManager.m_Instance.LastSelectedStencil;
    }
  }

  public static void ApplyAction(PrimitivePropertyAction action) {
    var widget = SelectedPrimitive;
    if (widget == null) {
      OutputWindowScript.Error("Select a primitive first.");
      return;
    }

    switch (action) {
      case PrimitivePropertyAction.SizeXMinus:
        Resize(widget, 0, -1);
        break;
      case PrimitivePropertyAction.SizeXPlus:
        Resize(widget, 0, 1);
        break;
      case PrimitivePropertyAction.SizeYMinus:
        Resize(widget, 1, -1);
        break;
      case PrimitivePropertyAction.SizeYPlus:
        Resize(widget, 1, 1);
        break;
      case PrimitivePropertyAction.SizeZMinus:
        Resize(widget, 2, -1);
        break;
      case PrimitivePropertyAction.SizeZPlus:
        Resize(widget, 2, 1);
        break;
      case PrimitivePropertyAction.Duplicate:
        Duplicate(widget);
        break;
      case PrimitivePropertyAction.SnapToGrid:
        SnapToGrid(widget);
        break;
      case PrimitivePropertyAction.ColorNext:
        NextColor(widget);
        break;
      case PrimitivePropertyAction.AlphaNext:
        NextAlpha(widget);
        break;
      case PrimitivePropertyAction.ToggleWireframe:
        ToggleWireframe(widget);
        break;
    }
  }

  private static void Resize(StencilWidget widget, int axis, int direction) {
    float step = 0.1f * App.METERS_TO_UNITS / Mathf.Max(0.001f, App.Scene.Pose.scale);
    Vector3 extents = widget.Extents;
    extents[axis] = Mathf.Max(step, extents[axis] + direction * step);

    SketchMemoryScript.m_Instance.PerformAndRecordCommand(
        new ResizeStencilCommand(widget, extents));
    MultiplayerManager.m_Instance?.SyncPrimitiveNow(widget);
  }

  private static void SnapToGrid(StencilWidget widget) {
    float step = SelectionManager.m_Instance != null
        ? SelectionManager.m_Instance.SnappingGridSize : 0f;
    if (step <= 0.0001f) {
      step = 0.1f * App.METERS_TO_UNITS / Mathf.Max(0.001f, App.Scene.Pose.scale);
    }

    TrTransform xf = widget.LocalTransform;
    xf.translation = new Vector3(
        Mathf.Round(xf.translation.x / step) * step,
        Mathf.Round(xf.translation.y / step) * step,
        Mathf.Round(xf.translation.z / step) * step);

    SketchMemoryScript.m_Instance.PerformAndRecordCommand(
        new MoveWidgetCommand(widget, xf, widget.CustomDimension, final: true));
    MultiplayerManager.m_Instance?.SyncPrimitiveNow(widget);
  }

  private static void Duplicate(StencilWidget widget) {
    GrabWidget prefab = WidgetManager.m_Instance.GetStencilPrefab(widget.Type);
    TrTransform worldXf = TrTransform.FromTransform(widget.transform);
    worldXf.translation += widget.transform.right *
        (0.15f * App.METERS_TO_UNITS);

    var create = new CreateWidgetCommand(prefab, worldXf, null, true);
    SketchMemoryScript.m_Instance.PerformAndRecordCommand(create);

    var clone = create.Widget as StencilWidget;
    if (clone == null) return;
    clone.Extents = widget.Extents;

    var sourceVisual = PrimitiveVisualState.GetOrCreate(widget);
    var cloneVisual = PrimitiveVisualState.GetOrCreate(clone);
    cloneVisual.Color = sourceVisual.Color;
    cloneVisual.Alpha = sourceVisual.Alpha;
    cloneVisual.Wireframe = sourceVisual.Wireframe;
    cloneVisual.Apply();

    SelectionManager.m_Instance.LastSelectedStencil = clone;
    MultiplayerManager.m_Instance?.SyncPrimitiveNow(clone);
  }

  private static void NextColor(StencilWidget widget) {
    var state = PrimitiveVisualState.GetOrCreate(widget);
    int current = 0;
    float best = float.MaxValue;
    for (int i = 0; i < kPalette.Length; ++i) {
      float d = (new Vector4(state.Color.r, state.Color.g, state.Color.b, state.Color.a) -
                 new Vector4(kPalette[i].r, kPalette[i].g, kPalette[i].b, kPalette[i].a)).sqrMagnitude;
      if (d < best) { best = d; current = i; }
    }
    state.Color = kPalette[(current + 1) % kPalette.Length];
    state.Apply();
    MultiplayerManager.m_Instance?.SyncPrimitiveNow(widget);
  }

  private static void NextAlpha(StencilWidget widget) {
    var state = PrimitiveVisualState.GetOrCreate(widget);
    if (state.Alpha > 0.8f) state.Alpha = 0.65f;
    else if (state.Alpha > 0.45f) state.Alpha = 0.35f;
    else state.Alpha = 1f;
    state.Apply();
    MultiplayerManager.m_Instance?.SyncPrimitiveNow(widget);
  }

  private static void ToggleWireframe(StencilWidget widget) {
    var state = PrimitiveVisualState.GetOrCreate(widget);
    state.Wireframe = !state.Wireframe;
    state.Apply();
    MultiplayerManager.m_Instance?.SyncPrimitiveNow(widget);
  }
}

} // namespace TiltBrush
