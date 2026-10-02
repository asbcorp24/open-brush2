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

    string root = Path.GetFullPath(Path.Combine(
        Application.dataPath, "..", kBuildRoot, "WindowsObserver"));

    PrepareWindowsObserverBuild(root);

    var options = new BuildTiltBrush.TiltBuildOptions {
      AutoProfile = false,
      Il2Cpp = false,
      Target = BuildTarget.StandaloneWindows64,
      XrSdk = XrSdkMode.Monoscopic,
      Location = Path.Combine(root, "OpenBrushObserver.exe"),
      Stamp = "multiplayer-observer",
      UnityOptions = BuildOptions.CleanBuildCache,
      Description = "Open Brush Multiplayer Observer"
    };

    BuildTiltBrush.DoBuild(options);
    Debug.Log("[Multiplayer] Windows Observer build: " + options.Location);
  }

  private static void PrepareWindowsObserverBuild(string root) {
    if (Directory.Exists(root)) {
      FileUtil.DeleteFileOrDirectory(root);
    }
    Directory.CreateDirectory(root);

    if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64) {
      bool switched = EditorUserBuildSettings.SwitchActiveBuildTarget(
          BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);
      if (!switched) {
        throw new BuildTiltBrush.BuildFailedException(
            "Could not switch active build target to StandaloneWindows64.");
      }
    }

    PlayerSettings.SetGraphicsAPIs(
        BuildTarget.StandaloneWindows64,
        new[] { UnityEngine.Rendering.GraphicsDeviceType.Direct3D11 });

    RebuildAddressablesForActiveTarget();
    AssetDatabase.Refresh();

    Debug.Log(
        "[Multiplayer] Windows Observer prebuild complete: clean output, " +
        "StandaloneWindows64 active, D3D11 forced, Addressables rebuilt.");
  }

  private static void RebuildAddressablesForActiveTarget() {
    try {
      Type settingsType = AppDomain.CurrentDomain.GetAssemblies()
          .Select(a => a.GetType(
              "UnityEditor.AddressableAssets.Settings.AddressableAssetSettings", false))
          .FirstOrDefault(t => t != null);

      if (settingsType == null) {
        Debug.LogWarning(
            "[Multiplayer] Addressables editor API was not found. " +
            "Unity will use its normal player-build integration.");
        return;
      }

      MethodInfo clean = settingsType.GetMethods(
          BindingFlags.Public | BindingFlags.Static)
          .FirstOrDefault(m => m.Name == "CleanPlayerContent" &&
              m.GetParameters().Length == 0);
      clean?.Invoke(null, null);

      MethodInfo build = settingsType.GetMethods(
          BindingFlags.Public | BindingFlags.Static)
          .Where(m => m.Name == "BuildPlayerContent")
          .OrderBy(m => m.GetParameters().Length)
          .FirstOrDefault();

      if (build == null) {
        Debug.LogWarning(
            "[Multiplayer] Addressables BuildPlayerContent API was not found.");
        return;
      }

      ParameterInfo[] parameters = build.GetParameters();
      object[] args = parameters.Length == 0
          ? null
          : parameters.Select(_ => (object)null).ToArray();

      build.Invoke(null, args);
      Debug.Log(
          "[Multiplayer] Addressables rebuilt for " +
          EditorUserBuildSettings.activeBuildTarget + ".");
    } catch (TargetInvocationException ex) {
      throw new BuildTiltBrush.BuildFailedException(
          "Windows Addressables build failed: " +
          ex.GetBaseException().Message);
    } catch (Exception ex) {
      throw new BuildTiltBrush.BuildFailedException(
          "Windows Addressables preparation failed: " + ex.Message);
    }
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

  [MenuItem("Open Brush/Multiplayer/Build Phone Cardboard Viewer APK")]
  public static void BuildPhoneCardboardViewer() {
    if (!ValidateCardboardSdk()) {
      return;
    }

    ConfigureCardboardAndroidSettings();

    string root = Path.GetFullPath(Path.Combine(
        Application.dataPath, "..", kBuildRoot, "PhoneCardboardViewer"));
    if (Directory.Exists(root)) {
      FileUtil.DeleteFileOrDirectory(root);
    }
    Directory.CreateDirectory(root);

    var options = new BuildTiltBrush.TiltBuildOptions {
      AutoProfile = false,
      Il2Cpp = true,
      Target = BuildTarget.Android,
      XrSdk = XrSdkMode.Cardboard,
      Location = Path.Combine(root, "OpenBrushPhoneCardboardViewer.apk"),
      Stamp = "multiplayer-cardboard-viewer",
      UnityOptions = BuildOptions.CleanBuildCache,
      Description = "Open Brush Phone Cardboard Viewer",
      AndroidBuildAppBundle = false,
      AndroidTargetSdkVersion = AndroidSdkVersions.AndroidApiLevel35
    };

    BuildTiltBrush.DoBuild(options);
    Debug.Log("[Multiplayer] Phone Cardboard Viewer APK: " + options.Location);
  }

  [MenuItem("Open Brush/Multiplayer/Build XREAL Beam Pro Multiplayer APK")]
  public static void BuildXrealBeamProMultiplayer() {
    if (!ValidateXrealSdk()) {
      return;
    }

    PrepareXrealBuild();

    string root = Path.GetFullPath(Path.Combine(
        Application.dataPath, "..", kBuildRoot, "XrealBeamPro"));
    if (Directory.Exists(root)) {
      FileUtil.DeleteFileOrDirectory(root);
    }
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

  private static bool ValidateCardboardSdk() {
    bool found = PackageInfo.GetAllRegisteredPackages()
        .Any(p => p.name == "com.google.xr.cardboard");

    if (!found) {
      EditorUtility.DisplayDialog(
          "Phone Cardboard Viewer build is not ready",
          "Google Cardboard XR Plugin is missing.\n\n" +
          "Expected package: com.google.xr.cardboard\n" +
          "The repository manifest normally installs v1.35.0 automatically.",
          "OK");
      return false;
    }
    return true;
  }

  private static void ConfigureCardboardAndroidSettings() {
    PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
    PlayerSettings.SetScriptingBackend(
        BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
    PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
    PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
    PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel35;
    PlayerSettings.SetGraphicsAPIs(
        BuildTarget.Android,
        new[] { UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3 });

    SetAndroidPropertyIfAvailable("optimizedFramePacing", false);
    SetAndroidPropertyIfAvailable("forceInternetPermission", true);

    Debug.Log(
        "[Cardboard] Android settings: LandscapeLeft, IL2CPP, ARM64, " +
        "API 26-35, OpenGLES3, LAN Internet permission.");
  }

  private static void SetAndroidPropertyIfAvailable(string name, object value) {
    try {
      Type androidType = typeof(PlayerSettings).GetNestedType(
          "Android", BindingFlags.Public | BindingFlags.NonPublic);
      PropertyInfo property = androidType?.GetProperty(
          name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
      if (property != null && property.CanWrite) {
        property.SetValue(null, value);
      }
    } catch (Exception ex) {
      Debug.LogWarning("[Cardboard] Could not set Android property " +
          name + ": " + ex.Message);
    }
  }

  private static bool ValidateXrealSdk() {
    var xrealPackage = PackageInfo.GetAllRegisteredPackages()
        .FirstOrDefault(p => p.name != null &&
            p.name.Equals("com.xreal.xr", StringComparison.OrdinalIgnoreCase));

    if (xrealPackage == null) {
      EditorUtility.DisplayDialog(
          "XREAL Beam Pro build is not ready",
          "Import the official XREAL SDK for Unity first.\n\n" +
          "Recommended: XREAL SDK 3.1.0\n" +
          "Package Manager > + > Add package from tarball > com.xreal.xr.tar.gz\n\n" +
          "Then enable the XREAL XR Plug-in for Android and run Project Validation.",
          "OK");
      return false;
    }

    if (!Application.unityVersion.StartsWith("6000.0.", StringComparison.Ordinal) &&
        !Application.unityVersion.StartsWith("2022.3.", StringComparison.Ordinal) &&
        !Application.unityVersion.StartsWith("2021.3.", StringComparison.Ordinal)) {
      Debug.LogWarning(
          "[XREAL] Current Unity is " + Application.unityVersion +
          ". XREAL SDK 3.1.0 officially documents Unity 2021.3 LTS, " +
          "2022.3 LTS and 6000.0 LTS. If the XREAL package fails to compile, " +
          "open this project with a supported editor before diagnosing Open Brush code.");
    }

    Debug.Log("[XREAL] Found package " + xrealPackage.name + " " + xrealPackage.version + ".");
    return true;
  }

  private static void PrepareXrealBuild() {
    if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android) {
      bool switched = EditorUserBuildSettings.SwitchActiveBuildTarget(
          BuildTargetGroup.Android, BuildTarget.Android);
      if (!switched) {
        throw new BuildTiltBrush.BuildFailedException(
            "Could not switch active build target to Android for the XREAL build.");
      }
    }

    ConfigureXrealAndroidSettings();
    AssetDatabase.Refresh();
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

    SetAndroidPropertyIfAvailable("forceInternetPermission", true);
    SetAndroidPropertyIfAvailable("optimizedFramePacing", false);

    Debug.Log(
        "[XREAL] Android settings: Portrait, IL2CPP, ARM64, min API 29, " +
        "OpenGLES3, INTERNET enabled.");
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
