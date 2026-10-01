using System;

namespace TiltBrush.Multiplayer {

public enum MultiplayerConnectionState {
  Disconnected,
  Connecting,
  Connected
}

/// <summary>
/// Network backend abstraction. Photon Fusion, Unity Transport, LAN UDP, etc. can
/// implement this interface without changing Open Brush integration code.
/// </summary>
public interface IMultiplayerTransport {
  MultiplayerConnectionState State { get; }
  string LocalPlayerId { get; }

  event Action<string> MessageReceived;
  event Action Connected;
  event Action Disconnected;

  void Host(string roomId);
  void Join(string roomId);
  void Leave();
  void Send(string message);
}

} // namespace TiltBrush.Multiplayer
