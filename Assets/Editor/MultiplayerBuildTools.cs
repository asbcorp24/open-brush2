#if UNITY_EDITOR

using System.IO;
using UnityEditor;
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

  [MenuItem("Open Brush/Multiplayer/Build Quest Multiplayer APK")]
  public static void BuildQuestMultiplayer() {
    if (!ValidatePhotonBeforeBuild()) {
      return;
    }

    string root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", kBuildRoot, "Quest"));
    Directory.CreateDirectory(root);

    var options = new BuildTiltBrush.TiltBuildOptions {
      AutoProfile = false,
      Il2Cpp = true,
      Target = BuildTarget.Android,
      XrSdk = XrSdkMode.OpenXR,
      Location = Path.Combine(root, "OpenBrushMultiplayer.apk"),
      Stamp = "multiplayer-quest",
      UnityOptions = BuildOptions.None,
      Description = "Open Brush Multiplayer Quest",
      AndroidBuildAppBundle = false
    };

    BuildTiltBrush.DoBuild(options);
    Debug.Log("[Multiplayer] Quest multiplayer APK: " + options.Location);
  }

  private static bool ValidatePhotonBeforeBuild() {
    MultiplayerSetupValidator.ValidateSetup();

    bool fusionFound = FindType("Fusion.NetworkRunner") != null;
    bool voiceFound = FindType("Photon.Voice.Unity.VoiceConnection") != null;
    bool hasFusionId = App.Config != null &&
        App.Config.PhotonFusionSecrets != null &&
        !string.IsNullOrWhiteSpace(App.Config.PhotonFusionSecrets.ClientId);
    bool hasVoiceId = App.Config != null &&
        App.Config.PhotonVoiceSecrets != null &&
        !string.IsNullOrWhiteSpace(App.Config.PhotonVoiceSecrets.ClientId);

    if (!fusionFound || !voiceFound || !hasFusionId || !hasVoiceId) {
      EditorUtility.DisplayDialog(
          "Multiplayer build is not ready",
          "Required before build:\n" +
          "• Photon Fusion 2 SDK\n" +
          "• Photon Voice 2 SDK\n" +
          "• Fusion App ID in Secrets.asset\n" +
          "• Voice App ID in Secrets.asset\n" +
          "• MP_PHOTON enabled\n\n" +
          "Run Open Brush > Multiplayer > Enable Photon Multiplayer after importing the SDKs.",
          "OK");
      return false;
    }

    return true;
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
