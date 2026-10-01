using System;
using UnityEngine;

namespace TiltBrush.Multiplayer {

[Serializable]
public enum MultiplayerMessageType {
  Hello,
  PlayerPose,
  StrokeBegin,
  StrokePoint,
  StrokeEnd,
  StrokeDelete,
  ClearScene,
  SnapshotRequest,
  SnapshotChunk
}

[Serializable]
public struct MultiplayerPose {
  public Vector3 headPosition;
  public Quaternion headRotation;
  public Vector3 leftHandPosition;
  public Quaternion leftHandRotation;
  public Vector3 rightHandPosition;
  public Quaternion rightHandRotation;
}

[Serializable]
public struct NetworkStrokePoint {
  public Vector3 position;
  public Quaternion rotation;
  public float pressure;
  public float timestamp;
}

[Serializable]
public struct MultiplayerEnvelope {
  public MultiplayerMessageType type;
  public string roomId;
  public string senderId;
  public string strokeId;
  public string brushGuid;
  public Color color;
  public float brushSize;
  public MultiplayerPose pose;
  public NetworkStrokePoint point;
  public string payload;
}

/// <summary>
/// Transport-neutral wire format. JsonUtility is intentionally used here so this layer
/// works on Quest/Android without requiring an additional serializer.
/// </summary>
public static class MultiplayerProtocol {
  public static string Serialize(MultiplayerEnvelope envelope) {
    return JsonUtility.ToJson(envelope);
  }

  public static bool TryDeserialize(string json, out MultiplayerEnvelope envelope) {
    envelope = default;
    if (string.IsNullOrEmpty(json)) {
      return false;
    }

    try {
      envelope = JsonUtility.FromJson<MultiplayerEnvelope>(json);
      return true;
    } catch (Exception exception) {
      Debug.LogWarning($"Multiplayer packet parse failed: {exception.Message}");
      return false;
    }
  }
}

} // namespace TiltBrush.Multiplayer
