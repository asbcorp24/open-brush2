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
