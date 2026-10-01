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

    string icon = action switch {
      PrimitivePropertyAction.Duplicate => "Icons/copy",
      PrimitivePropertyAction.SnapToGrid => "Icons/pointersnap",
      PrimitivePropertyAction.ColorNext => "Icons/colorpalette",
      PrimitivePropertyAction.AlphaNext => "Icons/visibility_on",
      PrimitivePropertyAction.ToggleWireframe => "Icons/max_edges",
      PrimitivePropertyAction.SizeStepNext => "Icons/grid_thick",
      PrimitivePropertyAction.SizeXMinus => "Icons/grid_contract",
      PrimitivePropertyAction.SizeYMinus => "Icons/grid_contract",
      PrimitivePropertyAction.SizeZMinus => "Icons/grid_contract",
      _ => "Icons/grid_expand"
    };

    Texture2D texture = Resources.Load<Texture2D>(icon);
    if (texture != null) {
      m_ButtonTexture = texture;
      m_CurrentButtonTexture = texture;
      ConfigureTextureAtlas();
    }

    gameObject.name = "PanelButton_PrimitiveProperty_" + action;
  }

  protected override void OnButtonPressed() {
    PrimitivePropertiesPanelController.ApplyAction(Action);
    SketchControlsScript.m_Instance.EatGazeObjectInput();
  }
}

} // namespace TiltBrush
