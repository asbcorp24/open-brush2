#if UNITY_EDITOR

using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;

namespace TiltBrush {

public static class MultiplayerBuildTools {
  private const string kBuildRoot = "Builds/Multiplayer";

  [MenuItem("Open Brush/Multiplayer/Build Windows Observer")]
  public static void BuildWindowsObserver() {
    if (!ValidateLanBuild(false)) {
      return;
    }

    string root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", kBuildRoot, "WindowsObserver"));
    Directory.CreateDirectory(root);

    var options = new BuildTiltBrush.TiltBuildOptions {
      AutoProfile = false,
      Il2Cpp = false,
      Target = BuildTarget.StandaloneWindows64,
      XrSdk = XrSdkMode.Monoscopic,
      Location = Path.Combine(root, "OpenBrushObserver.exe"),
      Stamp = "multiplayer-observer",
      UnityOptions = BuildOptions.None,
      Description = "Open Brush Multiplayer Observer"
    };

    BuildTiltBrush.DoBuild(options);
    Debug.Log("[Multiplayer] Windows Observer build: " + options.Location);
  }

  [MenuItem("Open Brush/Multiplayer/Build VIVE Focus Vision Multiplayer APK")]
  public static void BuildViveFocusVisionMultiplayer() {
    if (!ValidateLanBuild(true)) {
      return;
    }

    EnableVivePassthroughFeature();

    string root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", kBuildRoot, "ViveFocusVision"));
    Directory.CreateDirectory(root);

    var options = new BuildTiltBrush.TiltBuildOptions {
      AutoProfile = false,
      Il2Cpp = true,
      Target = BuildTarget.Android,
      XrSdk = XrSdkMode.OpenXR,
      Location = Path.Combine(root, "OpenBrushViveFocusVisionMultiplayer.apk"),
      Stamp = "multiplayer-vive-focus-vision",
      UnityOptions = BuildOptions.None,
      Description = "Open Brush Multiplayer VIVE Focus Vision",
      AndroidBuildAppBundle = false
    };

    BuildTiltBrush.DoBuild(options);
    Debug.Log("[Multiplayer] VIVE Focus Vision multiplayer APK: " + options.Location);
  }

  [MenuItem("Open Brush/Multiplayer/Build XREAL Beam Pro Multiplayer APK")]
  public static void BuildXrealBeamProMultiplayer() {
    if (!ValidateXrealSdk()) {
      return;
    }

    ConfigureXrealAndroidSettings();

    string root = Path.GetFullPath(Path.Combine(
        Application.dataPath, "..", kBuildRoot, "XrealBeamPro"));
    Directory.CreateDirectory(root);

    var options = new BuildTiltBrush.TiltBuildOptions {
      AutoProfile = false,
      Il2Cpp = true,
      Target = BuildTarget.Android,
      XrSdk = XrSdkMode.XREAL,
      Location = Path.Combine(root, "OpenBrushXrealBeamPro.apk"),
      Stamp = "multiplayer-xreal-beam-pro",
      UnityOptions = BuildOptions.None,
      Description = "Open Brush Multiplayer XREAL Beam Pro",
      AndroidBuildAppBundle = false,
      AndroidTargetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto
    };

    BuildTiltBrush.DoBuild(options);
    Debug.Log("[Multiplayer] XREAL Beam Pro APK: " + options.Location);
  }

  private static void EnableVivePassthroughFeature() {
    try {
      Type settingsType = AppDomain.CurrentDomain.GetAssemblies()
          .Select(a => a.GetType("UnityEngine.XR.OpenXR.OpenXRSettings", false))
          .FirstOrDefault(t => t != null);
      if (settingsType == null) {
        Debug.LogWarning("[MR] OpenXRSettings type not found; enable VIVE XR Passthrough manually.");
        return;
      }

      MethodInfo getSettings = settingsType.GetMethods(
          BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
          .FirstOrDefault(m => m.Name == "GetSettingsForBuildTargetGroup" &&
              m.GetParameters().Length == 1);
      if (getSettings == null) {
        Debug.LogWarning("[MR] Android OpenXR settings API not found.");
        return;
      }

      object settings = getSettings.Invoke(null, new object[] { BuildTargetGroup.Android });
      if (settings == null) return;

      object featuresObject = null;
      var featuresProperty = settingsType.GetProperty("features",
          BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
      if (featuresProperty != null) featuresObject = featuresProperty.GetValue(settings);
      if (featuresObject == null) {
        var featuresField = settingsType.GetField("features",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (featuresField != null) featuresObject = featuresField.GetValue(settings);
      }

      if (!(featuresObject is IEnumerable features)) {
        Debug.LogWarning("[MR] OpenXR feature list unavailable.");
        return;
      }

      bool found = false;
      foreach (object feature in features) {
        if (feature == null) continue;
        Type type = feature.GetType();
        string fullName = type.FullName ?? type.Name;

        string company = type.GetProperty("company",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            ?.GetValue(feature)?.ToString() ?? string.Empty;
        string nameUi = type.GetProperty("nameUi",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            ?.GetValue(feature)?.ToString() ?? string.Empty;

        bool vive = fullName.IndexOf("VIVE", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    company.IndexOf("VIVE", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    company.IndexOf("HTC", StringComparison.OrdinalIgnoreCase) >= 0;
        bool passthrough = fullName.IndexOf("Passthrough", StringComparison.OrdinalIgnoreCase) >= 0 ||
                           nameUi.IndexOf("Passthrough", StringComparison.OrdinalIgnoreCase) >= 0;
        if (!vive || !passthrough) continue;

        var enabledProp = type.GetProperty("enabled",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (enabledProp != null && enabledProp.CanWrite) {
          enabledProp.SetValue(feature, true);
          found = true;
          if (feature is UnityEngine.Object unityObject) {
            EditorUtility.SetDirty(unityObject);
          }
          Debug.Log("[MR] Enabled OpenXR feature: " + fullName);
        }
      }

      if (found) {
        AssetDatabase.SaveAssets();
      } else {
        Debug.LogWarning("[MR] VIVE XR Passthrough feature not found after package import.");
      }
    } catch (Exception ex) {
      Debug.LogWarning("[MR] Automatic passthrough enable failed: " +
          ex.GetBaseException().Message);
    }
  }

  private static bool ValidateXrealSdk() {
    bool xrealFound = PackageInfo.GetAllRegisteredPackages()
        .Any(p => p.name != null &&
            p.name.StartsWith("com.xreal.xr", StringComparison.OrdinalIgnoreCase));

    if (!xrealFound) {
      EditorUtility.DisplayDialog(
          "XREAL Beam Pro build is not ready",
          "Import the official XREAL SDK for Unity first.\n\n" +
          "Recommended: XREAL SDK 3.1.0\n" +
          "Package Manager > + > Add package from tarball > com.xreal.xr.tar.gz\n\n" +
          "Then enable the XREAL XR Plug-in for Android and run Project Validation.",
          "OK");
      return false;
    }

    return true;
  }

  private static void ConfigureXrealAndroidSettings() {
    PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
    PlayerSettings.SetScriptingBackend(
        BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
    PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
    PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
    PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
    PlayerSettings.SetGraphicsAPIs(BuildTarget.Android,
        new[] { UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3 });

    Debug.Log(
        "[XREAL] Android settings: Portrait, IL2CPP, ARM64, min API 29, OpenGLES3.");
  }

  private static bool ValidateLanBuild(bool requireViveOpenXr) {
    if (!requireViveOpenXr) {
      return true;
    }

    bool viveOpenXrFound = PackageInfo.GetAllRegisteredPackages()
        .Any(p => p.name == "com.htc.upm.vive.openxr");

    if (!viveOpenXrFound) {
      EditorUtility.DisplayDialog(
          "VIVE Focus Vision build is not ready",
          "The VIVE OpenXR Plugin is required for the headset build.\n\n" +
          "Expected package: com.htc.upm.vive.openxr\n\n" +
          "No Photon, Fusion, Voice or App ID is required for LAN multiplayer.",
          "OK");
      return false;
    }

    return true;
  }

}

} // namespace TiltBrush

#endif
