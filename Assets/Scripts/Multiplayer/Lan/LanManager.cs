using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TiltBrush;
using UnityEngine;

namespace OpenBrush.Multiplayer {

/// <summary>
/// Internet-free classroom transport.
/// Windows/monoscopic client is the host; Android VIVE clients discover it by room code
/// on the local Wi-Fi and connect directly over TCP.
/// </summary>
public class LanManager : IDataConnectionHandler {
  private const int TcpPort = 45871;
  private const int DiscoveryPort = 45870;
  private const string DiscoveryPrefix = "OPENBRUSH-LAN";
  private const int MaxFrameBytes = 32 * 1024 * 1024;

  [Serializable]
  private class PrimitiveState {
    public string id;
    public int stencilType;
    public Vector3 localPosition;
    public Quaternion localRotation;
    public float size;
    public Vector3 extents;
    public bool visible;
  }

  [Serializable]
  private class Packet {
    public string type;
    public int playerId;
    public int targetId;
    public int percentage;
    public int playerCount;
    public int timestamp;
    public long sentAt;
    public bool flag;
    public string nickname;
    public string room;
    public string payload;
    public string commandGuid;
  }

  private sealed class ClientPeer {
    public int Id;
    public string Nickname;
    public string UserId;
    public DateTime LastSeenUtc;
    public int PingMs = -1;
    public TcpClient Client;
    public NetworkStream Stream;
    public readonly object WriteLock = new object();
  }

  private sealed class LocalTransientData : ITransientData<PlayerRigData> {
    public int PlayerId { get; set; }
    public bool IsSpawned => true;
    public PlayerRigData Data;
    public void TransmitData(PlayerRigData data) { Data = data; }
    public PlayerRigData ReceiveData() => Data;
  }

  private readonly MultiplayerManager m_Manager;
  private readonly ConcurrentQueue<Action> m_MainThread = new ConcurrentQueue<Action>();
  private readonly Dictionary<int, ClientPeer> m_Peers = new Dictionary<int, ClientPeer>();
  private readonly Dictionary<int, PlayerRigData> m_LastRigData = new Dictionary<int, PlayerRigData>();
  private readonly Dictionary<string, int> m_KnownPlayerIds = new Dictionary<string, int>();
  private readonly object m_PeersLock = new object();

  private CancellationTokenSource m_Cts;
  private TcpListener m_Listener;
  private TcpClient m_ServerClient;
  private NetworkStream m_ServerStream;
  private readonly object m_ServerWriteLock = new object();
  private Task m_AcceptTask;
  private Task m_ReadTask;
  private Task m_DiscoveryTask;
  private bool m_IsHost;
  private int m_NextPlayerId = 2;
  private int m_PlayerCount;
  private string m_Room;
  private LocalTransientData m_Local;
  private float m_NextRigSend;
  private float m_NextHeartbeat;
  private RoomCreateData m_CurrentRoomData;
  private bool m_ManualDisconnect;
  private bool m_Reconnecting;

  public event Action Disconnected;
  public ConnectionUserInfo UserInfo { get; set; }
  public ConnectionState State { get; private set; } = ConnectionState.INITIALIZED;
  public string LastError { get; private set; }

  public LanManager(MultiplayerManager manager) {
    m_Manager = manager;
    UserInfo = new ConnectionUserInfo {
      UserId = Guid.NewGuid().ToString("N"),
      Nickname = Application.platform == RuntimePlatform.Android ? "VIVE" : "Teacher PC",
      Role = Application.platform == RuntimePlatform.Android ? "participant" : "teacher"
    };
  }

  public Task<bool> Connect() {
    State = ConnectionState.IN_LOBBY;
    LastError = null;
    return Task.FromResult(true);
  }

  public async Task<bool> JoinRoom(RoomCreateData data) {
    try {
      State = ConnectionState.JOINING_ROOM;
      m_CurrentRoomData = data;
      m_Room = string.IsNullOrWhiteSpace(data.roomName) ? "000000" : data.roomName.Trim();
      m_ManualDisconnect = false;
      m_Reconnecting = false;
      m_Cts?.Cancel();
      m_Cts = new CancellationTokenSource();

      m_IsHost = Application.platform == RuntimePlatform.WindowsPlayer ||
                 Application.platform == RuntimePlatform.WindowsEditor ||
                 (App.Config != null && App.Config.m_SdkMode == SdkMode.Monoscopic);

      if (m_IsHost) {
        StartHost();
        m_PlayerCount = 1;
        m_Local = new LocalTransientData { PlayerId = 1 };
        m_Manager.localPlayerJoined?.Invoke(1, m_Local);
        State = ConnectionState.IN_ROOM;
        return true;
      }

      var endpoint = await DiscoverHostAsync(m_Room, m_Cts.Token);
      if (endpoint == null) {
        LastError = "LAN room " + m_Room + " not found. Check that PC host and headset are on the same Wi-Fi.";
        State = ConnectionState.ERROR;
        return false;
      }

      m_ServerClient = new TcpClient();
      m_ServerClient.NoDelay = true;
      m_ServerClient.ReceiveTimeout = 5000;
      m_ServerClient.SendTimeout = 5000;
      await m_ServerClient.ConnectAsync(endpoint.Address, endpoint.Port);
      m_ServerStream = m_ServerClient.GetStream();

      SendToServer(new Packet {
        type = "hello",
        room = m_Room,
        nickname = string.IsNullOrWhiteSpace(UserInfo.Nickname) ? "VIVE" : UserInfo.Nickname,
        payload = UserInfo.UserId
      });

      m_ReadTask = Task.Run(() => ReadServerLoop(m_Cts.Token));
      State = ConnectionState.IN_ROOM;
      return true;
    } catch (Exception ex) {
      LastError = "[LAN] Join failed: " + ex.Message;
      State = ConnectionState.ERROR;
      Debug.LogError(LastError);
      return false;
    }
  }

  private void StartHost() {
    m_Listener = new TcpListener(IPAddress.Any, TcpPort);
    m_Listener.Start();
    m_AcceptTask = Task.Run(() => AcceptLoop(m_Cts.Token));
    m_DiscoveryTask = Task.Run(() => BroadcastLoop(m_Cts.Token));
    Debug.Log("[LAN] Hosting room " + m_Room + " on TCP " + TcpPort);
  }

  private async Task BroadcastLoop(CancellationToken token) {
    using var udp = new UdpClient();
    udp.EnableBroadcast = true;
    var endpoint = new IPEndPoint(IPAddress.Broadcast, DiscoveryPort);
    while (!token.IsCancellationRequested) {
      try {
        string message = DiscoveryPrefix + "|" + m_Room + "|" + TcpPort;
        byte[] bytes = Encoding.UTF8.GetBytes(message);
        await udp.SendAsync(bytes, bytes.Length, endpoint);
      } catch { }
      try { await Task.Delay(700, token); } catch { break; }
    }
  }

  private static async Task<IPEndPoint> DiscoverHostAsync(string room, CancellationToken token) {
    using var udp = new UdpClient();
    udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
    udp.Client.Bind(new IPEndPoint(IPAddress.Any, DiscoveryPort));

    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
    timeout.CancelAfter(10000);

    while (!timeout.IsCancellationRequested) {
      try {
        var receiveTask = udp.ReceiveAsync();
        var done = await Task.WhenAny(receiveTask, Task.Delay(1000, timeout.Token));
        if (done != receiveTask) continue;
        var result = receiveTask.Result;
        string text = Encoding.UTF8.GetString(result.Buffer);
        string[] parts = text.Split('|');
        if (parts.Length == 3 && parts[0] == DiscoveryPrefix && parts[1] == room &&
            int.TryParse(parts[2], out int port)) {
          return new IPEndPoint(result.RemoteEndPoint.Address, port);
        }
      } catch (OperationCanceledException) {
        break;
      } catch { }
    }
    return null;
  }

  private async Task AcceptLoop(CancellationToken token) {
    while (!token.IsCancellationRequested) {
      try {
        TcpClient client = await m_Listener.AcceptTcpClientAsync();
        client.NoDelay = true;
        client.ReceiveTimeout = 5000;
        client.SendTimeout = 5000;
        _ = Task.Run(() => ReadClientLoop(client, token));
      } catch {
        if (!token.IsCancellationRequested) throw;
      }
    }
  }

  private void ReadClientLoop(TcpClient client, CancellationToken token) {
    NetworkStream stream = null;
    ClientPeer peer = null;
    try {
      stream = client.GetStream();
      while (!token.IsCancellationRequested && client.Connected) {
        Packet packet = ReadPacket(stream);
        if (packet == null) break;

        if (peer == null) {
          if (packet.type != "hello" || packet.room != m_Room) break;
          int id;
          string userId = string.IsNullOrWhiteSpace(packet.payload)
              ? Guid.NewGuid().ToString("N") : packet.payload;
          lock (m_PeersLock) {
            if (!m_KnownPlayerIds.TryGetValue(userId, out id) || m_Peers.ContainsKey(id)) {
              id = m_NextPlayerId++;
              m_KnownPlayerIds[userId] = id;
            }
            peer = new ClientPeer {
              Id = id,
              Nickname = string.IsNullOrWhiteSpace(packet.nickname) ? "VIVE " + id : packet.nickname,
              UserId = userId,
              LastSeenUtc = DateTime.UtcNow,
              Client = client,
              Stream = stream
            };
            m_Peers[id] = peer;
            m_PlayerCount = 1 + m_Peers.Count;
          }

          SendToPeer(peer, new Packet {
            type = "welcome",
            playerId = id,
            playerCount = m_PlayerCount,
            room = m_Room
          });

          List<ClientPeer> existing;
          lock (m_PeersLock) existing = m_Peers.Values.Where(p => p.Id != id).ToList();
          foreach (var old in existing) {
            SendToPeer(peer, new Packet { type = "player_joined", playerId = old.Id, nickname = old.Nickname });
          }

          var captured = peer;
          m_MainThread.Enqueue(() => {
            CreateRemotePlayer(captured.Id, captured.Nickname);
            SendExistingPrimitivesToPlayer(captured.Id);
          });
          Broadcast(new Packet { type = "player_joined", playerId = id, nickname = peer.Nickname }, id);
          Broadcast(new Packet { type = "player_count", playerCount = m_PlayerCount });
          continue;
        }

        packet.playerId = peer.Id;
        HandleHostPacket(peer, packet);
      }
    } catch (Exception ex) {
      if (!token.IsCancellationRequested) Debug.LogWarning("[LAN] Client read ended: " + ex.Message);
    } finally {
      if (peer != null) RemovePeer(peer.Id);
      try { client.Close(); } catch { }
    }
  }

  private void ReadServerLoop(CancellationToken token) {
    string disconnectReason = "Host connection closed.";
    try {
      while (!token.IsCancellationRequested && m_ServerClient != null && m_ServerClient.Connected) {
        Packet packet = ReadPacket(m_ServerStream);
        if (packet == null) break;
        HandleClientPacket(packet);
      }
    } catch (Exception ex) {
      disconnectReason = ex.Message;
    } finally {
      if (!token.IsCancellationRequested && !m_ManualDisconnect && !m_IsHost) {
        BeginReconnect(disconnectReason);
      }
    }
  }

  private void HandleHostPacket(ClientPeer source, Packet packet) {
    source.LastSeenUtc = DateTime.UtcNow;
    switch (packet.type) {
      case "pong":
        if (packet.sentAt > 0) {
          long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
          source.PingMs = (int)Math.Max(0, Math.Min(9999, now - packet.sentAt));
        }
        break;
      case "rig":
        Broadcast(packet, source.Id);
        QueueRig(packet.playerId, packet.payload);
        break;
      case "stroke":
      case "delete":
      case "undo":
      case "redo":
      case "primitive_create":
      case "primitive_move":
      case "primitive_visibility":
        Broadcast(packet, source.Id);
        m_MainThread.Enqueue(() => ApplyCommandPacket(packet));
        break;
      case "scene_chunk":
        if (packet.targetId != 0) SendToPlayer(packet.targetId, packet);
        break;
      case "colocation":
        Broadcast(packet, source.Id);
        m_MainThread.Enqueue(() => ApplyColocation(packet.payload));
        break;
    }
  }

  private void HandleClientPacket(Packet packet) {
    switch (packet.type) {
      case "welcome":
        m_PlayerCount = packet.playerCount;
        bool wasReconnect = m_Reconnecting;
        m_Reconnecting = false;
        m_Local = new LocalTransientData { PlayerId = packet.playerId };
        m_MainThread.Enqueue(() => {
          ClearRemotePlayersForReconnect();
          SketchMemoryScript.m_Instance?.ClearMemory();
          m_Manager.localPlayerJoined?.Invoke(packet.playerId, m_Local);
          if (wasReconnect) m_Manager.NotifyLanReconnected();
        });
        break;
      case "ping":
        SendToServer(new Packet { type = "pong", sentAt = packet.sentAt });
        break;
      case "player_joined":
        if (m_Local == null || packet.playerId != m_Local.PlayerId)
          m_MainThread.Enqueue(() => CreateRemotePlayer(packet.playerId, packet.nickname));
        break;
      case "player_left":
        m_MainThread.Enqueue(() => {
          DestroyRemotePlayer(packet.playerId);
          m_Manager.playerLeft?.Invoke(packet.playerId);
        });
        break;
      case "player_count":
        m_PlayerCount = packet.playerCount;
        break;
      case "rig":
        QueueRig(packet.playerId, packet.payload);
        break;
      case "stroke":
      case "delete":
      case "undo":
      case "redo":
      case "primitive_create":
      case "primitive_move":
      case "primitive_visibility":
        m_MainThread.Enqueue(() => ApplyCommandPacket(packet));
        break;
      case "scene_chunk":
        if (packet.targetId == 0 || (m_Local != null && packet.targetId == m_Local.PlayerId)) {
          byte[] bytes = Convert.FromBase64String(packet.payload);
          m_MainThread.Enqueue(() => MultiplayerSceneSync.m_Instance?.onLargeDataReceived?.Invoke(bytes, packet.percentage));
        }
        break;
      case "view_only":
        if (m_Local != null && packet.targetId == m_Local.PlayerId)
          m_MainThread.Enqueue(() => m_Manager.IsViewOnly = packet.flag);
        break;
      case "kick":
        if (m_Local != null && packet.targetId == m_Local.PlayerId)
          m_MainThread.Enqueue(async () => await Disconnect());
        break;
      case "ownership":
        if (m_Local != null && packet.targetId == m_Local.PlayerId)
          m_MainThread.Enqueue(() => m_Manager.RoomOwnershipReceived(Array.Empty<RemotePlayerSettings>(), JsonUtility.FromJson<RoomCreateData>(packet.payload)));
        break;
      case "colocation":
        m_MainThread.Enqueue(() => ApplyColocation(packet.payload));
        break;
    }
  }

  private void BeginReconnect(string reason) {
    if (m_Reconnecting || m_ManualDisconnect || m_IsHost) return;
    m_Reconnecting = true;
    LastError = "[LAN] Connection lost: " + reason;
    m_MainThread.Enqueue(() => m_Manager.NotifyLanReconnecting(LastError));
    _ = Task.Run(ReconnectLoopAsync);
  }

  private async Task ReconnectLoopAsync() {
    for (int attempt = 1; attempt <= 6 && !m_ManualDisconnect; attempt++) {
      try {
        var endpoint = await DiscoverHostAsync(m_Room, m_Cts.Token);
        if (endpoint == null) {
          await Task.Delay(1000);
          continue;
        }

        try { m_ServerStream?.Close(); } catch { }
        try { m_ServerClient?.Close(); } catch { }

        m_ServerClient = new TcpClient();
        m_ServerClient.NoDelay = true;
        m_ServerClient.ReceiveTimeout = 5000;
        m_ServerClient.SendTimeout = 5000;
        await m_ServerClient.ConnectAsync(endpoint.Address, endpoint.Port);
        m_ServerStream = m_ServerClient.GetStream();

        SendToServer(new Packet {
          type = "hello",
          room = m_Room,
          nickname = string.IsNullOrWhiteSpace(UserInfo.Nickname) ? "VIVE" : UserInfo.Nickname,
          payload = UserInfo.UserId
        });

        m_ReadTask = Task.Run(() => ReadServerLoop(m_Cts.Token));
        return;
      } catch {
        if (!m_ManualDisconnect) await Task.Delay(1000);
      }
    }

    if (!m_ManualDisconnect) {
      m_Reconnecting = false;
      LastError = "[LAN] Unable to reconnect to room " + m_Room + ".";
      m_MainThread.Enqueue(() => {
        m_Manager.NotifyLanConnectionFailed(LastError);
        Disconnected?.Invoke();
      });
    }
  }

  private void ClearRemotePlayersForReconnect() {
    if (m_Manager.m_RemotePlayers == null) return;
    foreach (var player in m_Manager.m_RemotePlayers.List.ToArray()) {
      if (player?.PlayerGameObject != null) UnityEngine.Object.Destroy(player.PlayerGameObject);
    }
    m_Manager.m_RemotePlayers.ClearList();
  }

  private void QueueRig(int playerId, string payload) {
    if (string.IsNullOrEmpty(payload)) return;
    m_MainThread.Enqueue(() => {
      var data = JsonUtility.FromJson<PlayerRigData>(payload);
      m_LastRigData[playerId] = data;
      var remote = m_Manager.m_RemotePlayers?.GetPlayerById(playerId);
      var rig = remote?.TransientData as LanPlayerRig;
      rig?.Apply(data);
    });
  }

  private async void ApplyCommandPacket(Packet packet) {
    try {
      if (packet.type == "stroke") {
        byte[] bytes = Convert.FromBase64String(packet.payload);
        var strokes = await MultiplayerStrokeSerialization.DecompressAndDeserializeMemoryListAsync(bytes);
        foreach (var stroke in strokes) {
          if (SketchMemoryScript.m_Instance.IsStrokeInMemory(stroke.m_Guid)) continue;
          Guid commandId = Guid.TryParse(packet.commandGuid, out var parsed) ? parsed : Guid.NewGuid();
          var command = new BrushStrokeCommand(stroke, commandId, packet.timestamp);
          SketchMemoryScript.m_Instance.MemoryListAdd(stroke);
          SketchMemoryScript.m_Instance.PerformAndRecordNetworkCommand(command);
        }
      } else if (packet.type == "delete") {
        if (!Guid.TryParse(packet.payload, out Guid strokeId)) return;
        var stroke = SketchMemoryScript.AllStrokes().FirstOrDefault(s => s.m_Guid == strokeId);
        if (stroke != null) {
          Guid commandId = Guid.TryParse(packet.commandGuid, out var parsed) ? parsed : Guid.NewGuid();
          SketchMemoryScript.m_Instance.PerformAndRecordNetworkCommand(
              new DeleteStrokeCommand(stroke, commandId, packet.timestamp));
        }
      } else if (packet.type == "primitive_create") {
        ApplyPrimitiveState(packet.payload, createIfMissing: true);
      } else if (packet.type == "primitive_move") {
        ApplyPrimitiveState(packet.payload, createIfMissing: false);
      } else if (packet.type == "primitive_visibility") {
        ApplyPrimitiveState(packet.payload, createIfMissing: false);
      } else if (packet.type == "undo" || packet.type == "redo") {
        if (!Guid.TryParse(packet.commandGuid, out Guid commandId)) return;
        var command = SketchMemoryScript.m_Instance.GetAllOperations()
            .FirstOrDefault(x => x.Guid == commandId);
        if (command != null) {
          if (packet.type == "undo") command.Undo();
          else command.Redo();
        }
      }
    } catch (Exception ex) {
      Debug.LogError("[LAN] Apply command failed: " + ex);
    }
  }

  private void SendExistingPrimitivesToPlayer(int playerId) {
    if (!m_IsHost || WidgetManager.m_Instance == null) return;

    foreach (var widget in WidgetManager.m_Instance.StencilWidgets.ToArray()) {
      if (widget == null || !widget.gameObject.activeSelf) continue;
      var marker = EnsurePrimitiveId(widget);
      var state = CapturePrimitiveState(widget, marker.Id, true);
      SendToPlayer(playerId, new Packet {
        type = "primitive_create",
        targetId = playerId,
        payload = JsonUtility.ToJson(state)
      });
    }
  }

  private void ApplyPrimitiveState(string payload, bool createIfMissing) {
    if (string.IsNullOrWhiteSpace(payload)) return;
    PrimitiveState state = JsonUtility.FromJson<PrimitiveState>(payload);
    if (state == null || string.IsNullOrWhiteSpace(state.id)) return;

    NetworkPrimitiveId marker = UnityEngine.Object.FindObjectsByType<NetworkPrimitiveId>(
        FindObjectsInactive.Include, FindObjectsSortMode.None)
        .FirstOrDefault(x => x.Id == state.id);

    StencilWidget widget = marker != null ? marker.GetComponent<StencilWidget>() : null;
    if (widget == null && createIfMissing) {
      var type = (StencilType)state.stencilType;
      GrabWidget prefab = WidgetManager.m_Instance.GetStencilPrefab(type);
      if (prefab == null) return;

      TrTransform worldXf = TrTransform.TRS(
          App.ActiveCanvas.transform.TransformPoint(state.localPosition),
          App.ActiveCanvas.transform.rotation * state.localRotation,
          state.size);

      var create = new CreateWidgetCommand(prefab, worldXf, null, true);
      SketchMemoryScript.m_Instance.PerformAndRecordNetworkCommand(create);
      widget = create.Widget as StencilWidget;
      if (widget == null) return;

      marker = widget.gameObject.GetComponent<NetworkPrimitiveId>();
      if (marker == null) marker = widget.gameObject.AddComponent<NetworkPrimitiveId>();
      marker.Id = state.id;
    }

    if (widget == null) return;

    widget.LocalTransform = TrTransform.TRS(
        state.localPosition, state.localRotation, state.size);
    try { widget.Extents = state.extents; } catch { }
    if (state.visible) {
      if (!widget.gameObject.activeSelf) {
        widget.gameObject.SetActive(true);
        widget.RestoreFromToss();
      }
    } else {
      widget.Hide();
    }
  }

  private static PrimitiveState CapturePrimitiveState(StencilWidget widget, string id, bool visible = true) {
    TrTransform xf = widget.LocalTransform;
    return new PrimitiveState {
      id = id,
      stencilType = (int)widget.Type,
      localPosition = xf.translation,
      localRotation = xf.rotation,
      size = xf.scale,
      extents = widget.Extents,
      visible = visible
    };
  }

  private static NetworkPrimitiveId EnsurePrimitiveId(StencilWidget widget, string preferredId = null) {
    if (widget == null) return null;
    var marker = widget.GetComponent<NetworkPrimitiveId>();
    if (marker == null) marker = widget.gameObject.AddComponent<NetworkPrimitiveId>();
    if (string.IsNullOrWhiteSpace(marker.Id)) {
      marker.Id = string.IsNullOrWhiteSpace(preferredId) ? Guid.NewGuid().ToString("N") : preferredId;
    }
    return marker;
  }

  private void ApplyColocation(string payload) {
    if (string.IsNullOrEmpty(payload)) return;
    var reference = JsonUtility.FromJson<ManualColocationReference>(payload);
    m_Manager.ReceiveManualColocationReference(reference);
  }

  private void CreateRemotePlayer(int id, string nickname) {
    if (m_Manager.m_RemotePlayers == null) return;
    if (m_Manager.m_RemotePlayers.GetPlayerById(id) != null) return;
    var rig = LanPlayerRig.Create(id, nickname);
    var remote = new RemotePlayer {
      PlayerId = id,
      Nickname = nickname,
      TransientData = rig,
      PlayerGameObject = rig.gameObject
    };
    m_Manager.remotePlayerJoined?.Invoke(remote);
  }

  private void DestroyRemotePlayer(int id) {
    var player = m_Manager.m_RemotePlayers?.GetPlayerById(id);
    if (player?.PlayerGameObject != null) UnityEngine.Object.Destroy(player.PlayerGameObject);
  }

  private void RemovePeer(int id) {
    ClientPeer peer = null;
    lock (m_PeersLock) {
      if (m_Peers.TryGetValue(id, out peer)) m_Peers.Remove(id);
      m_PlayerCount = 1 + m_Peers.Count;
    }
    if (peer != null) {
      Broadcast(new Packet { type = "player_left", playerId = id });
      Broadcast(new Packet { type = "player_count", playerCount = m_PlayerCount });
      m_MainThread.Enqueue(() => {
        DestroyRemotePlayer(id);
        m_Manager.playerLeft?.Invoke(id);
      });
    }
  }

  public void Update() {
    while (m_MainThread.TryDequeue(out var action)) {
      try { action(); } catch (Exception ex) { Debug.LogError("[LAN] Main-thread action: " + ex); }
    }

    if (State != ConnectionState.IN_ROOM || m_Local == null) return;

    if (m_IsHost && Time.unscaledTime >= m_NextHeartbeat) {
      m_NextHeartbeat = Time.unscaledTime + 1f;
      long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
      Broadcast(new Packet { type = "ping", sentAt = nowMs });
      DropTimedOutPeers();
    }

    if (Time.unscaledTime >= m_NextRigSend) {
      m_NextRigSend = Time.unscaledTime + 0.05f;
      var packet = new Packet {
        type = "rig",
        playerId = m_Local.PlayerId,
        payload = JsonUtility.ToJson(m_Local.Data)
      };

      if (m_IsHost) Broadcast(packet);
      else SendToServer(packet);
    }
  }

  public int GetPingMilliseconds(int playerId) {
    if (!m_IsHost) return -1;
    lock (m_PeersLock) {
      return m_Peers.TryGetValue(playerId, out var peer) ? peer.PingMs : -1;
    }
  }

  public string GetConnectionQuality(int playerId) {
    int ping = GetPingMilliseconds(playerId);
    if (ping < 0) return "connecting";
    if (ping <= 30) return "excellent";
    if (ping <= 80) return "good";
    if (ping <= 160) return "unstable";
    return "poor";
  }

  private void DropTimedOutPeers() {
    List<ClientPeer> stale;
    lock (m_PeersLock) {
      stale = m_Peers.Values
          .Where(p => p.LastSeenUtc != default &&
              (DateTime.UtcNow - p.LastSeenUtc).TotalSeconds > 6)
          .ToList();
    }
    foreach (var peer in stale) {
      try { peer.Client.Close(); } catch { }
    }
  }

  public int GetPlayerCount() => Math.Max(1, m_PlayerCount);

  public int GetNetworkedTimestampMilliseconds() {
    return (int)(Time.realtimeSinceStartup * 1000f);
  }

  public bool GetPlayerRoomOwnershipStatus(int playerId) {
    if (playerId == 1 && m_IsHost) return true;
    return m_LastRigData.TryGetValue(playerId, out var data) && data.IsRoomOwner;
  }

  public GameObject GetPlayerPrefab(int playerId) {
    return m_Manager.m_RemotePlayers?.GetPlayerById(playerId)?.PlayerGameObject;
  }

  public void SendLargeDataToPlayer(int playerId, byte[] largeData, int percentage) {
    var packet = new Packet {
      type = "scene_chunk",
      targetId = playerId,
      percentage = percentage,
      payload = Convert.ToBase64String(largeData)
    };
    if (m_IsHost) SendToPlayer(playerId, packet);
    else SendToServer(packet);
  }

  public async Task<bool> PerformCommand(BaseCommand command) {
    if (command is CreateWidgetCommand createWidget &&
        createWidget.Widget is StencilWidget createdStencil) {
      var marker = EnsurePrimitiveId(createdStencil, command.Guid.ToString("N"));
      var state = CapturePrimitiveState(createdStencil, marker.Id, true);
      SendRoomPacket(new Packet {
        type = "primitive_create",
        payload = JsonUtility.ToJson(state),
        commandGuid = command.Guid.ToString()
      });
      return true;
    }

    if (command is MoveWidgetCommand moveWidget &&
        moveWidget.Widget is StencilWidget movedStencil) {
      var marker = EnsurePrimitiveId(movedStencil);
      var state = CapturePrimitiveState(movedStencil, marker.Id, true);
      SendRoomPacket(new Packet {
        type = "primitive_move",
        payload = JsonUtility.ToJson(state),
        commandGuid = command.Guid.ToString()
      });
      return true;
    }

    if (command is HideWidgetCommand hideWidget &&
        hideWidget.Widget is StencilWidget hiddenStencil) {
      var marker = EnsurePrimitiveId(hiddenStencil);
      var state = CapturePrimitiveState(hiddenStencil, marker.Id, false);
      SendRoomPacket(new Packet {
        type = "primitive_visibility",
        payload = JsonUtility.ToJson(state),
        commandGuid = command.Guid.ToString()
      });
      return true;
    }

    if (command is BrushStrokeCommand brush && brush.m_Stroke != null) {
      byte[] bytes = await MultiplayerStrokeSerialization.SerializeAndCompressMemoryListAsync(
          new List<Stroke> { brush.m_Stroke });
      SendRoomPacket(new Packet {
        type = "stroke",
        payload = Convert.ToBase64String(bytes),
        commandGuid = command.Guid.ToString(),
        timestamp = command.NetworkTimestamp ?? command.Timestamp
      });
      return true;
    }

    if (command is DeleteStrokeCommand delete && delete.m_TargetStroke != null) {
      SendRoomPacket(new Packet {
        type = "delete",
        payload = delete.m_TargetStroke.m_Guid.ToString(),
        commandGuid = command.Guid.ToString(),
        timestamp = command.NetworkTimestamp ?? command.Timestamp
      });
      return true;
    }

    return true;
  }

  public Task<bool> SendCommandToPlayer(BaseCommand command, int playerId) {
    // Scene late-join sync uses SendLargeDataToPlayer for strokes, so direct command
    // targeting is not required for the default Strokes sync mode.
    return Task.FromResult(true);
  }

  public Task<bool> CheckCommandReception(BaseCommand command, int playerId) => Task.FromResult(true);
  public Task<bool> CheckStrokeReception(Stroke stroke, int playerId) => Task.FromResult(true);

  public Task<bool> UndoCommand(BaseCommand command) {
    if (TrySendPrimitiveStateAfterUndoRedo(command)) return Task.FromResult(true);
    SendRoomPacket(new Packet { type = "undo", commandGuid = command.Guid.ToString() });
    return Task.FromResult(true);
  }

  public Task<bool> RedoCommand(BaseCommand command) {
    if (TrySendPrimitiveStateAfterUndoRedo(command)) return Task.FromResult(true);
    SendRoomPacket(new Packet { type = "redo", commandGuid = command.Guid.ToString() });
    return Task.FromResult(true);
  }

  private bool TrySendPrimitiveStateAfterUndoRedo(BaseCommand command) {
    StencilWidget widget = null;
    if (command is MoveWidgetCommand move) widget = move.Widget as StencilWidget;
    else if (command is CreateWidgetCommand create) widget = create.Widget as StencilWidget;
    else if (command is HideWidgetCommand hide) widget = hide.Widget as StencilWidget;
    if (widget == null) return false;

    var marker = EnsurePrimitiveId(widget, command.Guid.ToString("N"));
    bool visible = widget.gameObject.activeSelf && !widget.IsHiding();
    var state = CapturePrimitiveState(widget, marker.Id, visible);
    SendRoomPacket(new Packet {
      type = visible ? "primitive_move" : "primitive_visibility",
      payload = JsonUtility.ToJson(state),
      commandGuid = command.Guid.ToString()
    });
    return true;
  }

  public Task<bool> RpcPublishManualColocationReference(ManualColocationReference reference) {
    SendRoomPacket(new Packet { type = "colocation", payload = JsonUtility.ToJson(reference) });
    return Task.FromResult(true);
  }

  public Task<bool> RpcSendManualColocationReferenceToPlayer(ManualColocationReference reference, int playerId) {
    var packet = new Packet { type = "colocation", targetId = playerId, payload = JsonUtility.ToJson(reference) };
    if (m_IsHost) SendToPlayer(playerId, packet); else SendToServer(packet);
    return Task.FromResult(true);
  }

  public Task<bool> RpcTransferRoomOwnership(int playerId, RemotePlayerSettings[] playerSettings, RoomCreateData roomData) {
    var packet = new Packet { type = "ownership", targetId = playerId, payload = JsonUtility.ToJson(roomData) };
    if (m_IsHost) SendToPlayer(playerId, packet); else SendToServer(packet);
    return Task.FromResult(true);
  }

  public Task<bool> RpcSetUserViewOnlyMode(bool value, int playerId) {
    var packet = new Packet { type = "view_only", targetId = playerId, flag = value };
    if (m_IsHost) SendToPlayer(playerId, packet); else SendToServer(packet);
    return Task.FromResult(true);
  }

  public Task<bool> RpcKickPlayerOut(int playerId) {
    if (m_IsHost) {
      SendToPlayer(playerId, new Packet { type = "kick", targetId = playerId });
      ClientPeer peer;
      lock (m_PeersLock) m_Peers.TryGetValue(playerId, out peer);
      try { peer?.Client.Close(); } catch { }
    } else {
      SendToServer(new Packet { type = "kick", targetId = playerId });
    }
    return Task.FromResult(true);
  }

  public bool RpcMutePlayer(bool mute, int playerId) {
    // LAN mode currently has no voice transport; keep room-admin state compatible.
    return true;
  }

  public Task<bool> LeaveRoom(bool force = false) => DisconnectInternal(true);

  public Task<bool> Disconnect() => DisconnectInternal(false);

  private Task<bool> DisconnectInternal(bool returnToLobby) {
    try {
      m_ManualDisconnect = true;
      m_Reconnecting = false;
      m_Cts?.Cancel();
      try { m_ServerStream?.Close(); } catch { }
      try { m_ServerClient?.Close(); } catch { }
      try { m_Listener?.Stop(); } catch { }

      lock (m_PeersLock) {
        foreach (var peer in m_Peers.Values) {
          try { peer.Client.Close(); } catch { }
        }
        m_Peers.Clear();
      }

      m_PlayerCount = 0;
      m_Local = null;
      State = returnToLobby ? ConnectionState.IN_LOBBY : ConnectionState.DISCONNECTED;
      Disconnected?.Invoke();
      return Task.FromResult(true);
    } catch (Exception ex) {
      LastError = "[LAN] Disconnect failed: " + ex.Message;
      State = ConnectionState.ERROR;
      return Task.FromResult(false);
    }
  }

  private void SendRoomPacket(Packet packet) {
    if (m_IsHost) Broadcast(packet);
    else SendToServer(packet);
  }

  private void Broadcast(Packet packet, int exceptPlayerId = -1) {
    List<ClientPeer> peers;
    lock (m_PeersLock) peers = m_Peers.Values.Where(p => p.Id != exceptPlayerId).ToList();
    foreach (var peer in peers) SendToPeer(peer, packet);
  }

  private void SendToPlayer(int playerId, Packet packet) {
    ClientPeer peer;
    lock (m_PeersLock) m_Peers.TryGetValue(playerId, out peer);
    if (peer != null) SendToPeer(peer, packet);
  }

  private void SendToServer(Packet packet) {
    if (m_ServerStream == null) return;
    WritePacket(m_ServerStream, m_ServerWriteLock, packet);
  }

  private static void SendToPeer(ClientPeer peer, Packet packet) {
    if (peer?.Stream == null) return;
    WritePacket(peer.Stream, peer.WriteLock, packet);
  }

  private static void WritePacket(NetworkStream stream, object writeLock, Packet packet) {
    try {
      byte[] payload = Encoding.UTF8.GetBytes(JsonUtility.ToJson(packet));
      if (payload.Length > MaxFrameBytes) throw new InvalidDataException("LAN packet too large");
      byte[] len = BitConverter.GetBytes(payload.Length);
      lock (writeLock) {
        stream.Write(len, 0, len.Length);
        stream.Write(payload, 0, payload.Length);
        stream.Flush();
      }
    } catch { }
  }

  private static Packet ReadPacket(NetworkStream stream) {
    byte[] lenBytes = ReadExact(stream, 4);
    if (lenBytes == null) return null;
    int len = BitConverter.ToInt32(lenBytes, 0);
    if (len <= 0 || len > MaxFrameBytes) throw new InvalidDataException("Invalid LAN frame length");
    byte[] payload = ReadExact(stream, len);
    if (payload == null) return null;
    return JsonUtility.FromJson<Packet>(Encoding.UTF8.GetString(payload));
  }

  private static byte[] ReadExact(Stream stream, int count) {
    byte[] buffer = new byte[count];
    int offset = 0;
    while (offset < count) {
      int read = stream.Read(buffer, offset, count - offset);
      if (read <= 0) return null;
      offset += read;
    }
    return buffer;
  }
}

} // namespace OpenBrush.Multiplayer
