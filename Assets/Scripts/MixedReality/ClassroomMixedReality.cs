using UnityEngine;

namespace OpenBrush.MixedReality {

/// <summary>
/// Cross-device mixed-reality facade used by VR panels and Teacher-PC LAN commands.
/// Chooses XREAL when the active XR loader is XREAL; otherwise uses the VIVE provider.
/// </summary>
public static class ClassroomMixedReality {
  public static string ProviderName {
    get {
      if (XrealBeamProMixedRealityController.IsActiveXrealLoader()) return "XREAL";
      if (VivePassthroughController.Instance != null) return "VIVE";
      return "Unknown";
    }
  }

  public static ClassroomMrMode Mode {
    get {
      if (XrealBeamProMixedRealityController.IsActiveXrealLoader() &&
          XrealBeamProMixedRealityController.Instance != null) {
        return XrealBeamProMixedRealityController.Instance.Mode;
      }
      return VivePassthroughController.Instance != null
          ? VivePassthroughController.Instance.Mode
          : ClassroomMrMode.VR;
    }
  }

  public static float Amount {
    get {
      if (XrealBeamProMixedRealityController.IsActiveXrealLoader() &&
          XrealBeamProMixedRealityController.Instance != null) {
        return XrealBeamProMixedRealityController.Instance.PassthroughAmount;
      }
      return VivePassthroughController.Instance != null
          ? VivePassthroughController.Instance.PassthroughAmount
          : 0f;
    }
  }

  public static bool SetMode(ClassroomMrMode mode, float amount) {
    if (XrealBeamProMixedRealityController.IsActiveXrealLoader()) {
      var xreal = XrealBeamProMixedRealityController.Instance;
      return xreal != null && xreal.SetMode(mode, amount);
    }

    var vive = VivePassthroughController.Instance;
    return vive != null && vive.SetMode(mode, amount);
  }

  public static string TrackingLabel {
    get {
      if (XrealBeamProMixedRealityController.IsActiveXrealLoader() &&
          XrealBeamProMixedRealityController.Instance != null) {
        return XrealBeamProMixedRealityController.Instance.TrackingModeLabel;
      }
      return "6DoF";
    }
  }
}

} // namespace OpenBrush.MixedReality
