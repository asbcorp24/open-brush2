using System.Collections.Generic;
using System.Linq;
using OpenBrush.MixedReality;
using UnityEngine;

namespace TiltBrush {

public enum MixedRealityControlAction {
  VR,
  AR25,
  AR50,
  AR75,
  AR100
}

public class MixedRealityPanelController : MonoBehaviour {
  private static readonly (MixedRealityControlAction action, string label, string icon)[] kActions = {
    (MixedRealityControlAction.VR, "VR", "Icons/visibility_off"),
    (MixedRealityControlAction.AR25, "AR 25%", "Icons/visibility_on"),
    (MixedRealityControlAction.AR50, "AR 50%", "Icons/visibility_on"),
    (MixedRealityControlAction.AR75, "AR 75%", "Icons/visibility_on"),
    (MixedRealityControlAction.AR100, "AR 100%", "Icons/visibility_on"),
  };

  public void Configure() {
    var panel = GetComponent<BasePanel>();
    panel?.SetRuntimePanelType(BasePanel.PanelType.MixedReality);
    panel?.SetRuntimePanelDescription("MIXED REALITY");

    var allButtons = GetComponentsInChildren<BaseButton>(true).ToList();
    StencilButton template = GetComponentsInChildren<StencilButton>(true).FirstOrDefault();
    if (template == null) {
      Debug.LogWarning("[MR] No panel button template found.");
      return;
    }

    foreach (var button in allButtons) {
      button.gameObject.SetActive(false);
    }

    var buttons = new List<StencilButton>();
    for (int i = 0; i < kActions.Length; ++i) {
      var clone = Instantiate(template.gameObject, template.transform.parent);
      clone.SetActive(false);

      var button = clone.GetComponent<StencilButton>();
      button.ConfigureMixedReality(
          kActions[i].action,
          kActions[i].label,
          kActions[i].icon);
      buttons.Add(button);
      clone.SetActive(true);
    }

    const float spacingX = 0.48f;
    const float spacingY = 0.46f;
    Vector3 start = new Vector3(-0.48f, 0.30f, 0.05f);

    for (int i = 0; i < buttons.Count; ++i) {
      int row = i / 3;
      int col = i % 3;
      buttons[i].transform.localPosition = start +
          new Vector3(col * spacingX, -row * spacingY, 0f);
      buttons[i].transform.localRotation = Quaternion.identity;
      buttons[i].transform.localScale = Vector3.one * 0.32f;
    }
  }

  public static void ApplyAction(MixedRealityControlAction action) {
    var controller = VivePassthroughController.Instance;
    if (controller == null) {
      OutputWindowScript.Error("Mixed Reality controller is not ready.");
      return;
    }

    switch (action) {
      case MixedRealityControlAction.VR:
        controller.SetMode(ClassroomMrMode.VR, 0f);
        MultiplayerManager.m_Instance?.ReportLocalMixedRealityState(false, 0f);
        OutputWindowScript.Error("Mixed Reality: VR");
        break;
      case MixedRealityControlAction.AR25:
        controller.SetMode(ClassroomMrMode.AR, 0.25f);
        MultiplayerManager.m_Instance?.ReportLocalMixedRealityState(true, 0.25f);
        OutputWindowScript.Error("Mixed Reality: AR 25%");
        break;
      case MixedRealityControlAction.AR50:
        controller.SetMode(ClassroomMrMode.AR, 0.50f);
        MultiplayerManager.m_Instance?.ReportLocalMixedRealityState(true, 0.50f);
        OutputWindowScript.Error("Mixed Reality: AR 50%");
        break;
      case MixedRealityControlAction.AR75:
        controller.SetMode(ClassroomMrMode.AR, 0.75f);
        MultiplayerManager.m_Instance?.ReportLocalMixedRealityState(true, 0.75f);
        OutputWindowScript.Error("Mixed Reality: AR 75%");
        break;
      case MixedRealityControlAction.AR100:
        controller.SetMode(ClassroomMrMode.AR, 1.00f);
        MultiplayerManager.m_Instance?.ReportLocalMixedRealityState(true, 1.00f);
        OutputWindowScript.Error("Mixed Reality: AR 100%");
        break;
    }
  }
}

} // namespace TiltBrush
