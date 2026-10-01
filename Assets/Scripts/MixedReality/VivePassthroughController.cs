using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace OpenBrush.MixedReality {

public enum ClassroomMrMode {
  VR = 0,
  AR = 1
}

/// <summary>
/// Runtime adapter for HTC VIVE OpenXR planar passthrough.
/// Uses reflection so editor/Windows builds do not require HTC passthrough types at compile time.
/// </summary>
public class VivePassthroughController : MonoBehaviour {
  public static VivePassthroughController Instance { get; private set; }

  public ClassroomMrMode Mode { get; private set; } = ClassroomMrMode.VR;
  public float PassthroughAmount { get; private set; } = 1f;
  public bool IsAvailable { get; private set; }
  public string LastError { get; private set; }

  private int m_PassthroughId = -1;
  private MethodInfo m_CreatePlanar;
  private MethodInfo m_Destroy;
  private Type m_LayerType;
  private CameraState[] m_CameraStates;

  private struct CameraState {
    public Camera Camera;
    public CameraClearFlags ClearFlags;
    public Color Background;
  }

  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
  private static void Bootstrap() {
    if (Instance != null) return;
    var go = new GameObject("VIVE Mixed Reality Controller");
    DontDestroyOnLoad(go);
    go.AddComponent<VivePassthroughController>();
  }

  private void Awake() {
    if (Instance != null && Instance != this) {
      Destroy(gameObject);
      return;
    }
    Instance = this;
    CacheCameraState();
    ResolveViveApi();
  }

  private void CacheCameraState() {
    m_CameraStates = FindObjectsByType<Camera>(FindObjectsInactive.Include,
        FindObjectsSortMode.None)
      .Select(c => new CameraState {
        Camera = c,
        ClearFlags = c.clearFlags,
        Background = c.backgroundColor
      }).ToArray();
  }

  private void ResolveViveApi() {
    try {
      Type api = AppDomain.CurrentDomain.GetAssemblies()
        .Select(a => a.GetType("VIVE.OpenXR.Passthrough.PassthroughAPI", false))
        .FirstOrDefault(t => t != null);
      m_LayerType = AppDomain.CurrentDomain.GetAssemblies()
        .Select(a => a.GetType("VIVE.OpenXR.Passthrough.LayerType", false))
        .FirstOrDefault(t => t != null);

      if (api == null || m_LayerType == null) {
        LastError = "VIVE XR Passthrough API was not found. Enable VIVE XR Passthrough in OpenXR features.";
        IsAvailable = false;
        return;
      }

      m_CreatePlanar = api.GetMethods(BindingFlags.Public | BindingFlags.Static)
        .FirstOrDefault(m => m.Name == "CreatePlanarPassthrough" &&
            m.GetParameters().Length >= 1 &&
            m.GetParameters()[0].ParameterType == m_LayerType);
      m_Destroy = api.GetMethods(BindingFlags.Public | BindingFlags.Static)
        .FirstOrDefault(m => m.Name == "DestroyPassthrough" &&
            m.GetParameters().Length == 1);

      IsAvailable = m_CreatePlanar != null && m_Destroy != null;
      if (!IsAvailable) {
        LastError = "VIVE XR Passthrough API methods were not found.";
      }
    } catch (Exception ex) {
      LastError = ex.Message;
      IsAvailable = false;
    }
  }

  public bool SetMode(ClassroomMrMode mode, float amount = 1f) {
    Mode = mode;
    PassthroughAmount = Mathf.Clamp01(amount);

    if (mode == ClassroomMrMode.VR) {
      StopPassthrough();
      RestoreCameras();
      PlayerPrefs.SetInt("mr.mode", 0);
      PlayerPrefs.SetFloat("mr.amount", PassthroughAmount);
      PlayerPrefs.Save();
      return true;
    }

    if (Application.platform != RuntimePlatform.Android) {
      // Windows observer keeps virtual rendering; only VIVE clients enable cameras.
      return true;
    }

    if (!IsAvailable) {
      ResolveViveApi();
      if (!IsAvailable) {
        Debug.LogWarning("[MR] " + LastError);
        return false;
      }
    }

    if (m_PassthroughId < 0 && !StartPassthrough()) {
      return false;
    }

    ApplyTransparentCamera(PassthroughAmount);
    PlayerPrefs.SetInt("mr.mode", 1);
    PlayerPrefs.SetFloat("mr.amount", PassthroughAmount);
    PlayerPrefs.Save();
    return true;
  }

  public void SetAmount(float amount) {
    SetMode(Mode, amount);
  }

  private bool StartPassthrough() {
    try {
      object underlay = Enum.Parse(m_LayerType, "Underlay");
      object result = m_CreatePlanar.Invoke(null, new[] { underlay });
      m_PassthroughId = Convert.ToInt32(result);
      if (m_PassthroughId < 0) {
        LastError = "VIVE passthrough could not be created.";
        return false;
      }
      return true;
    } catch (Exception ex) {
      LastError = ex.GetBaseException().Message;
      Debug.LogError("[MR] CreatePlanarPassthrough failed: " + LastError);
      return false;
    }
  }

  private void StopPassthrough() {
    if (m_PassthroughId < 0 || m_Destroy == null) return;
    try {
      m_Destroy.Invoke(null, new object[] { m_PassthroughId });
    } catch (Exception ex) {
      Debug.LogWarning("[MR] DestroyPassthrough: " + ex.GetBaseException().Message);
    } finally {
      m_PassthroughId = -1;
    }
  }

  private void ApplyTransparentCamera(float amount) {
    // HTC underlay is visible through transparent pixels. 1 = fully transparent VR background.
    float backgroundAlpha = 1f - Mathf.Clamp01(amount);
    foreach (var camera in FindObjectsByType<Camera>(FindObjectsInactive.Include,
                 FindObjectsSortMode.None)) {
      if (camera == null) continue;
      camera.clearFlags = CameraClearFlags.SolidColor;
      Color c = camera.backgroundColor;
      c.r = 0f;
      c.g = 0f;
      c.b = 0f;
      c.a = backgroundAlpha;
      camera.backgroundColor = c;
    }
  }

  private void RestoreCameras() {
    if (m_CameraStates == null) return;
    foreach (var state in m_CameraStates) {
      if (state.Camera == null) continue;
      state.Camera.clearFlags = state.ClearFlags;
      state.Camera.backgroundColor = state.Background;
    }
  }

  private void Start() {
    if (Application.platform != RuntimePlatform.Android) return;
    int saved = PlayerPrefs.GetInt("mr.mode", 0);
    float amount = PlayerPrefs.GetFloat("mr.amount", 1f);
    if (saved == 1) SetMode(ClassroomMrMode.AR, amount);
  }

  private void OnDestroy() {
    if (Instance == this) Instance = null;
    StopPassthrough();
    RestoreCameras();
  }
}

} // namespace OpenBrush.MixedReality
