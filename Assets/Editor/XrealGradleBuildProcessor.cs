#if UNITY_EDITOR && UNITY_ANDROID

using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using UnityEditor.Android;
using UnityEngine;

namespace TiltBrush {

/// <summary>
/// Keeps Open Brush's shared Android manifest from stealing the launcher role
/// from the XREAL SDK. XREAL supplies its own XR launcher/runtime wiring.
/// </summary>
public class XrealGradleBuildProcessor : IPostGenerateGradleAndroidProject {
  private static readonly XNamespace Android =
      "http://schemas.android.com/apk/res/android";

  public int callbackOrder => 10000;

  public void OnPostGenerateGradleAndroidProject(string path) {
    if (BuildTiltBrush.CurrentBuildXrSdk != XrSdkMode.XREAL) {
      return;
    }

    string[] candidates = {
      Path.Combine(path, "src", "main", "AndroidManifest.xml"),
      Path.Combine(path, "unityLibrary", "src", "main", "AndroidManifest.xml")
    };

    string manifestPath = candidates.FirstOrDefault(File.Exists);
    if (manifestPath == null) {
      Debug.LogWarning(
          "[XREAL] Generated AndroidManifest.xml was not found. " +
          "Verify the XREAL SDK Project Validation results before installing the APK.");
      return;
    }

    try {
      XDocument document = XDocument.Load(manifestPath);
      XElement manifest = document.Root;
      XElement application = manifest?.Element("application");
      if (application == null) {
        Debug.LogWarning("[XREAL] Android manifest has no <application> element.");
        return;
      }

      bool changed = false;

      foreach (XElement activity in application.Elements("activity").ToList()) {
        string activityName = (string)activity.Attribute(Android + "name") ?? string.Empty;
        if (!activityName.EndsWith("UnityPlayerActivity", StringComparison.Ordinal)) {
          continue;
        }

        foreach (XElement filter in activity.Elements("intent-filter").ToList()) {
          bool isMain = filter.Elements("action").Any(
              e => (string)e.Attribute(Android + "name") == "android.intent.action.MAIN");
          bool isLauncher = filter.Elements("category").Any(
              e => (string)e.Attribute(Android + "name") == "android.intent.category.LAUNCHER");

          if (isMain && isLauncher) {
            filter.Remove();
            changed = true;
            Debug.Log(
                "[XREAL] Removed UnityPlayerActivity MAIN/LAUNCHER so the " +
                "XREAL SDK can own XR startup on Beam Pro.");
          }
        }
      }

      foreach (XElement meta in application.Elements("meta-data").ToList()) {
        string name = (string)meta.Attribute(Android + "name") ?? string.Empty;
        if (name == "pvr.app.type") {
          meta.Remove();
          changed = true;
          Debug.Log("[XREAL] Removed PICO-only pvr.app.type metadata.");
        }
      }

      if (changed) {
        document.Save(manifestPath);
      }
    } catch (Exception ex) {
      throw new Exception(
          "[XREAL] Failed to prepare generated Android manifest: " +
          ex.GetBaseException().Message, ex);
    }
  }
}

} // namespace TiltBrush

#endif
