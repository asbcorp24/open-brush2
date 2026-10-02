#if UNITY_EDITOR && UNITY_ANDROID

using System;
using System.IO;
using UnityEditor.Android;
using UnityEngine;

namespace TiltBrush {

/// <summary>
/// Adds the Android dependencies required by Google Cardboard only to the
/// generated Cardboard Gradle project. VIVE and XREAL builds are untouched.
/// </summary>
public class CardboardGradleBuildProcessor : IPostGenerateGradleAndroidProject {
  public int callbackOrder => 500;

  public void OnPostGenerateGradleAndroidProject(string path) {
    if (BuildTiltBrush.CurrentBuildXrSdk != XrSdkMode.Cardboard) {
      return;
    }

    string unityLibraryGradle = Path.Combine(path, "unityLibrary", "build.gradle");
    if (!File.Exists(unityLibraryGradle)) {
      unityLibraryGradle = Path.Combine(path, "build.gradle");
    }

    if (File.Exists(unityLibraryGradle)) {
      string gradle = File.ReadAllText(unityLibraryGradle);
      const string marker = "dependencies {";
      const string deps =
          "\n    implementation 'androidx.appcompat:appcompat:1.6.1'" +
          "\n    implementation 'com.google.android.gms:play-services-vision:20.1.3'" +
          "\n    implementation 'com.google.android.material:material:1.12.0'" +
          "\n    implementation 'com.google.protobuf:protobuf-javalite:3.19.4'";

      if (!gradle.Contains("play-services-vision:20.1.3")) {
        int index = gradle.IndexOf(marker, StringComparison.Ordinal);
        if (index >= 0) {
          index += marker.Length;
          gradle = gradle.Insert(index, deps);
          File.WriteAllText(unityLibraryGradle, gradle);
        } else {
          Debug.LogWarning("[Cardboard] Could not find Gradle dependencies block.");
        }
      }
    }

    string gradleProperties = Path.Combine(path, "gradle.properties");
    string properties = File.Exists(gradleProperties)
        ? File.ReadAllText(gradleProperties)
        : string.Empty;

    if (!properties.Contains("android.useAndroidX=true")) {
      properties += "\nandroid.useAndroidX=true";
    }
    if (!properties.Contains("android.enableJetifier=true")) {
      properties += "\nandroid.enableJetifier=true";
    }

    File.WriteAllText(gradleProperties, properties + "\n");
    Debug.Log("[Cardboard] Added required AndroidX / Vision / Material / protobuf Gradle dependencies.");
  }
}

} // namespace TiltBrush

#endif
