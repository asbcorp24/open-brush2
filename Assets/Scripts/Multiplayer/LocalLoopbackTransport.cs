using System;
using System.Collections.Generic;
using UnityEngine;

namespace TiltBrush.Multiplayer {

/// <summary>
/// Editor/development transport. Multiple instances in the same Unity process that
/// use the same room id can exchange packets. This makes the multiplayer integration
/// testable before Photon credentials are configured.
/// </summary>
public class LocalLoopbackTransport : MonoBehaviour, IMultiplayerTransport {
  private static readonly Dictionary<string, List<LocalLoopbackTransport>> s_Rooms =
      new Dictionary<string, List<LocalLoopbackTransport>>();

  [SerializeField] private string m_PlayerId;

  private string m_RoomId;

  public MultiplayerConnectionState State { get; private set; } =
      MultiplayerConnectionState.Disconnected;

  public string LocalPlayerId {
    get {
      if (string.IsNullOrEmpty(m_PlayerId)) {
        m_PlayerId = Guid.NewGuid().ToString("N");
      }
      return m_PlayerId;
    }
  }

  public event Action<string> MessageReceived;
  public event Action Connected;
  public event Action Disconnected;

  public void Host(string roomId) {
    Connect(roomId);
  }

  public void Join(string roomId) {
    Connect(roomId);
  }

  public void Leave() {
    if (State == MultiplayerConnectionState.Disconnected) {
      return;
    }

    if (!string.IsNullOrEmpty(m_RoomId) && s_Rooms.TryGetValue(m_RoomId, out var clients)) {
      clients.Remove(this);
      if (clients.Count == 0) {
        s_Rooms.Remove(m_RoomId);
      }
    }

    m_RoomId = null;
    State = MultiplayerConnectionState.Disconnected;
    Disconnected?.Invoke();
  }

  public void Send(string message) {
    if (State != MultiplayerConnectionState.Connected ||
        string.IsNullOrEmpty(m_RoomId) ||
        !s_Rooms.TryGetValue(m_RoomId, out var clients)) {
      return;
    }

    // Copy to tolerate listeners leaving the room while a packet is dispatched.
    var snapshot = clients.ToArray();
    foreach (var client in snapshot) {
      if (client != null && client != this && client.State == MultiplayerConnectionState.Connected) {
        client.MessageReceived?.Invoke(message);
      }
    }
  }

  private void Connect(string roomId) {
    if (string.IsNullOrWhiteSpace(roomId)) {
      throw new ArgumentException("Room id must not be empty.", nameof(roomId));
    }

    Leave();
    State = MultiplayerConnectionState.Connecting;
    m_RoomId = roomId.Trim();

    if (!s_Rooms.TryGetValue(m_RoomId, out var clients)) {
      clients = new List<LocalLoopbackTransport>();
      s_Rooms.Add(m_RoomId, clients);
    }

    clients.Add(this);
    _ = LocalPlayerId;
    State = MultiplayerConnectionState.Connected;
    Connected?.Invoke();
  }

  private void OnDestroy() {
    Leave();
  }
}

} // namespace TiltBrush.Multiplayer
