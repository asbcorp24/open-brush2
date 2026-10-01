// Copyright 2026 The Open Brush Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.

#if UNITY_EDITOR

using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace OpenBrush.Multiplayer.Editor {

public static class MultiplayerSetupValidator {
  private const string kPhotonDefine = "MP_PHOTON";

  [MenuItem("Open Brush/Multiplayer/Validate Photon Setup")]
  public static void ValidateSetup() {
    bool fusionFound = FindType("Fusion.NetworkRunner") != null;
    bool voiceFound = FindType("Photon.Voice.Unity.VoiceConnection") != null;

    bool standaloneDefine = HasDefine(NamedBuildTarget.Standalone, kPhotonDefine);
    bool androidDefine = HasDefine(NamedBuildTarget.Android, kPhotonDefine);

    Debug.Log(
        "[Multiplayer] Photon setup validation\n" +
        $"Fusion SDK: {(fusionFound ? "OK" : "MISSING")}\n" +
        $"Photon Voice SDK: {(voiceFound ? "OK" : "MISSING")}\n" +
        $"Standalone MP_PHOTON: {(standaloneDefine ? "OK" : "MISSING")}\n" +
        $"Android MP_PHOTON: {(androidDefine ? "OK" : "MISSING")}\n" +
        "The Main scene is already configured to use MultiplayerType.Photon.");

    if (!fusionFound || !voiceFound) {
      EditorUtility.DisplayDialog(
          "Open Brush Multiplayer",
          "Photon SDK is not complete. Install Photon Fusion 2 and Photon Voice 2 first. " +
          "Then run Open Brush > Multiplayer > Enable Photon Multiplayer.",
          "OK");
      return;
    }

    if (!standaloneDefine || !androidDefine) {
      EditorUtility.DisplayDialog(
          "Open Brush Multiplayer",
          "Photon SDKs are present, but MP_PHOTON is not enabled for every target. " +
          "Use Open Brush > Multiplayer > Enable Photon Multiplayer.",
          "OK");
      return;
    }

    EditorUtility.DisplayDialog(
        "Open Brush Multiplayer",
        "Photon code is enabled for Standalone and Android. " +
        "Next configure Photon Fusion and Photon Voice App IDs in Secrets.asset.",
        "OK");
  }

  [MenuItem("Open Brush/Multiplayer/Enable Photon Multiplayer")]
  public static void EnablePhotonMultiplayer() {
    bool fusionFound = FindType("Fusion.NetworkRunner") != null;
    bool voiceFound = FindType("Photon.Voice.Unity.VoiceConnection") != null;

    if (!fusionFound || !voiceFound) {
      EditorUtility.DisplayDialog(
          "Cannot enable Photon",
          "Install both Photon Fusion 2 and Photon Voice 2 before enabling MP_PHOTON. " +
          "This prevents compiler errors caused by enabling the symbol without the SDKs.",
          "OK");
      return;
    }

    AddDefine(NamedBuildTarget.Standalone, kPhotonDefine);
    AddDefine(NamedBuildTarget.Android, kPhotonDefine);

    AssetDatabase.SaveAssets();
    Debug.Log("[Multiplayer] MP_PHOTON enabled for Standalone and Android.");
    EditorUtility.DisplayDialog(
        "Photon Multiplayer Enabled",
        "MP_PHOTON was added for Standalone and Android. Unity will recompile. " +
        "Configure the Fusion and Voice App IDs in Secrets.asset before connecting.",
        "OK");
  }

  [MenuItem("Open Brush/Multiplayer/Disable Photon Multiplayer")]
  public static void DisablePhotonMultiplayer() {
    RemoveDefine(NamedBuildTarget.Standalone, kPhotonDefine);
    RemoveDefine(NamedBuildTarget.Android, kPhotonDefine);
    AssetDatabase.SaveAssets();
    Debug.Log("[Multiplayer] MP_PHOTON disabled for Standalone and Android.");
  }

  private static Type FindType(string fullName) {
    foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) {
      var type = assembly.GetType(fullName, false);
      if (type != null) {
        return type;
      }
    }
    return null;
  }

  private static bool HasDefine(NamedBuildTarget target, string define) {
    return GetDefines(target).Contains(define);
  }

  private static string[] GetDefines(NamedBuildTarget target) {
    string symbols = PlayerSettings.GetScriptingDefineSymbols(target);
    return symbols
        .Split(new[] {';'}, StringSplitOptions.RemoveEmptyEntries)
        .Select(x => x.Trim())
        .Where(x => !string.IsNullOrEmpty(x))
        .Distinct()
        .ToArray();
  }

  private static void AddDefine(NamedBuildTarget target, string define) {
    var symbols = GetDefines(target).ToList();
    if (!symbols.Contains(define)) {
      symbols.Add(define);
      PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", symbols));
    }
  }

  private static void RemoveDefine(NamedBuildTarget target, string define) {
    var symbols = GetDefines(target)
        .Where(x => !string.Equals(x, define, StringComparison.Ordinal))
        .ToArray();
    PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", symbols));
  }
}

} // namespace OpenBrush.Multiplayer.Editor

#endif
