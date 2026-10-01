using System;
using OpenBrush.Multiplayer;
using UnityEngine;

namespace TiltBrush {

public enum PrimitivePropertyAction {
  SizeXMinus,
  SizeXPlus,
  SizeYMinus,
  SizeYPlus,
  SizeZMinus,
  SizeZPlus,
  Duplicate,
  SnapToGrid,
  ColorNext,
  AlphaNext,
  ToggleWireframe,
  SizeStepNext
}

public class PrimitivePropertyButton : BaseButton {
  public PrimitivePropertyAction Action;

  public void Configure(PrimitivePropertyAction action, string description) {
    Action = action;
    m_LocalizedDescription = new UnityEngine.Localization.LocalizedString();
    SetDescriptionText(description);
    gameObject.name = "PanelButton_PrimitiveProperty_" + action;
  }

  protected override void OnButtonPressed() {
    PrimitivePropertiesPanelController.ApplyAction(Action);
    SketchControlsScript.m_Instance.EatGazeObjectInput();
  }
}

} // namespace TiltBrush
