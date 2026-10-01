using System;
using System.Linq;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Management;

namespace OpenBrush.MixedReality {

/// <summary>
/// Mixed-reality behavior for XREAL glasses driven by Beam Pro.
/// XREAL is optical see-through: AR is the natural mode. "VR" here means a
/// virtual dark background, not physical occlusion of the real world.
/// </summary>
public class XrealBeamProMixedRealityController : MonoBehaviour {
  public static XrealBeamProMixedRealityController Instance { get; private set; }

  public ClassroomMrMode Mode { get; private set; } = ClassroomMrMode.AR;
  public float PassthroughAmount { get; private set; } = 1f;
  public string TrackingModeLabel { get; private set; } = "XREAL";
  public bool IsXrealRuntime => IsActiveXrealLoader();

  private CameraState[] m_CameraStates;

  private struct CameraState {
    public Camera Camera;
    public CameraClearFlags ClearFlags;
    public Color Background;
  }

  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
  private static void Bootstrap() {
    if (Application.platform != RuntimePlatform.Android || Instance != null) return;
    var go = new GameObject("XREAL Beam Pro Mixed Reality Controller");
    DontDestroyOnLoad(go);
    go.AddComponent<XrealBeamProMixedRealityController>();
  }

  private void Awake() {
    if (Instance != null && Instance != this) {
      Destroy(gameObject);
      return;
    }
    Instance = this;
    CacheCameraState();
  }

  private void Start() {
    UpdateTrackingLabel();
    if (!IsXrealRuntime) return;

    int saved = PlayerPrefs.GetInt("mr.mode", 1);
    float amount = PlayerPrefs.GetFloat("mr.amount", 1f);
    SetMode(saved == 0 ? ClassroomMrMode.VR : ClassroomMrMode.AR, amount);
  }

  private void Update() {
    if (IsXrealRuntime && Time.frameCount % 120 == 0) {
      UpdateTrackingLabel();
    }
  }

  public bool SetMode(ClassroomMrMode mode, float amount = 1f) {
    Mode = mode;
    PassthroughAmount = Mathf.Clamp01(amount);

    if (!IsXrealRuntime) return false;

    if (mode == ClassroomMrMode.AR) {
      ApplyOpticalArBackground(PassthroughAmount);
    } else {
      ApplyVirtualDarkBackground();
    }

    PlayerPrefs.SetInt("mr.mode", mode == ClassroomMrMode.AR ? 1 : 0);
    PlayerPrefs.SetFloat("mr.amount", PassthroughAmount);
    PlayerPrefs.Save();
    return true;
  }

  private void ApplyOpticalArBackground(float amount) {
    // XREAL is optical see-through. A black/transparent camera clear emits no light,
    // leaving the real world visible through the lenses. The percentage is retained
    // for common Teacher-PC semantics and future hardware dimming support.
    foreach (var camera in FindObjectsByType<Camera>(
                 FindObjectsInactive.Include, FindObjectsSortMode.None)) {
      if (camera == null) continue;
      camera.clearFlags = CameraClearFlags.SolidColor;
      camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
    }
  }

  private void ApplyVirtualDarkBackground() {
    // Optical glasses cannot reproduce headset-style opaque VR without hardware
    // dimming. This provides the darkest virtual backdrop available to the app.
    foreach (var camera in FindObjectsByType<Camera>(
                 FindObjectsInactive.Include, FindObjectsSortMode.None)) {
      if (camera == null) continue;
      camera.clearFlags = CameraClearFlags.SolidColor;
      camera.backgroundColor = Color.black;
    }
  }

  public void RestoreCameras() {
    if (m_CameraStates == null) return;
    foreach (var state in m_CameraStates) {
      if (state.Camera == null) continue;
      state.Camera.clearFlags = state.ClearFlags;
      state.Camera.backgroundColor = state.Background;
    }
  }

  private void CacheCameraState() {
    m_CameraStates = FindObjectsByType<Camera>(
        FindObjectsInactive.Include, FindObjectsSortMode.None)
      .Select(c => new CameraState {
        Camera = c,
        ClearFlags = c.clearFlags,
        Background = c.backgroundColor
      }).ToArray();
  }

  private void UpdateTrackingLabel() {
    var head = InputDevices.GetDeviceAtXRNode(XRNode.Head);
    bool positionTracked = false;
    if (head.isValid) {
      positionTracked = head.TryGetFeatureValue(CommonUsages.devicePosition, out var _);
    }
    TrackingModeLabel = positionTracked ? "6DoF" : "3DoF";
  }

  public static bool IsActiveXrealLoader() {
    try {
      string stamp = App.Config != null ? App.Config.m_BuildStamp : string.Empty;
      if (!string.IsNullOrWhiteSpace(stamp) &&
          stamp.IndexOf("xreal", StringComparison.OrdinalIgnoreCase) >= 0) {
        return true;
      }

      var loader = XRGeneralSettings.Instance?.Manager?.activeLoader;
      string name = loader?.GetType().FullName ?? string.Empty;
      return name.IndexOf("XREAL", StringComparison.OrdinalIgnoreCase) >= 0;
    } catch {
      return false;
    }
  }

  private void OnDestroy() {
    if (Instance == this) Instance = null;
  }
}

} // namespace OpenBrush.MixedReality
