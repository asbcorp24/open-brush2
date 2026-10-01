#if UNITY_EDITOR

using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.PackageManager;
using UnityEngine;

namespace TiltBrush {

public static class MultiplayerBuildTools {
  private const string kBuildRoot = "Builds/Multiplayer";

  [MenuItem("Open Brush/Multiplayer/Build Windows Observer")]
  public static void BuildWindowsObserver() {
    if (!ValidatePhotonBeforeBuild()) {
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
    if (!ValidatePhotonBeforeBuild()) {
      return;
    }

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

  private static bool ValidatePhotonBeforeBuild() {
    OpenBrush.Multiplayer.Editor.MultiplayerSetupValidator.ValidateSetup();

    bool fusionFound = FindType("Fusion.NetworkRunner") != null;
    bool voiceFound = FindType("Photon.Voice.Unity.VoiceConnection") != null;
    bool hasFusionId = App.Config != null &&
        App.Config.PhotonFusionSecrets != null &&
        !string.IsNullOrWhiteSpace(App.Config.PhotonFusionSecrets.ClientId);
    bool hasVoiceId = App.Config != null &&
        App.Config.PhotonVoiceSecrets != null &&
        !string.IsNullOrWhiteSpace(App.Config.PhotonVoiceSecrets.ClientId);
    bool standaloneDefine = HasDefine(NamedBuildTarget.Standalone, "MP_PHOTON");
    bool androidDefine = HasDefine(NamedBuildTarget.Android, "MP_PHOTON");
    bool viveOpenXrFound = PackageInfo.GetAllRegisteredPackages()
        .Any(p => p.name == "com.htc.upm.vive.openxr");

    if (!fusionFound || !voiceFound || !hasFusionId || !hasVoiceId ||
        !standaloneDefine || !androidDefine || !viveOpenXrFound) {
      EditorUtility.DisplayDialog(
          "Multiplayer build is not ready",
          "Required before build:\n" +
          "• Photon Fusion 2 SDK\n" +
          "• Photon Voice 2 SDK\n" +
          "• Fusion App ID in Secrets.asset\n" +
          "• Voice App ID in Secrets.asset\n" +
          "• MP_PHOTON enabled\n" +
          "• VIVE OpenXR Plugin installed (com.htc.upm.vive.openxr)\n\n" +
          "Run Open Brush > Multiplayer > Enable Photon Multiplayer after importing the SDKs.",
          "OK");
      return false;
    }

    return true;
  }

  private static bool HasDefine(NamedBuildTarget target, string define) {
    string symbols = PlayerSettings.GetScriptingDefineSymbols(target);
    foreach (string symbol in symbols.Split(';')) {
      if (string.Equals(symbol.Trim(), define, System.StringComparison.Ordinal)) {
        return true;
      }
    }
    return false;
  }

  private static System.Type FindType(string fullName) {
    foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies()) {
      var type = assembly.GetType(fullName, false);
      if (type != null) {
        return type;
      }
    }
    return null;
  }
}

} // namespace TiltBrush

#endif
