using System;
using System.Collections;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace OpenBrush.Multiplayer {

/// <summary>
/// On Android/VIVE, automatically finds the first advertised classroom on the local Wi-Fi
/// and joins it. This is intended for dedicated classroom headsets.
/// </summary>
public class LanAutoJoin : MonoBehaviour {
  private const int DiscoveryPort = 45870;
  private const string DiscoveryPrefix = "OPENBRUSH-LAN";
  private bool m_Joining;

  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
  private static void Bootstrap() {
    if (Application.platform != RuntimePlatform.Android) return;
    if (FindFirstObjectByType<LanAutoJoin>() != null) return;

    var go = new GameObject("LAN Classroom Auto Join");
    DontDestroyOnLoad(go);
    go.AddComponent<LanAutoJoin>();
  }

  private IEnumerator Start() {
    yield return new WaitForSecondsRealtime(2f);

    while (true) {
      var manager = MultiplayerManager.m_Instance;
      if (manager != null && manager.IsLanMode &&
          manager.State != ConnectionState.IN_ROOM &&
          manager.State != ConnectionState.JOINING_ROOM &&
          manager.State != ConnectionState.RECONNECTING &&
          !m_Joining) {
        _ = FindAndJoinAsync();
      }

      yield return new WaitForSecondsRealtime(2f);
    }
  }

  private async Task FindAndJoinAsync() {
    m_Joining = true;
    try {
      var manager = MultiplayerManager.m_Instance;
      if (manager == null) return;

      if (manager.State == ConnectionState.INITIALIZED ||
          manager.State == ConnectionState.DISCONNECTED ||
          manager.State == ConnectionState.ERROR) {
        bool connected = await manager.Connect();
        if (!connected) return;
      }

      if (!manager.CanJoinRoom()) return;

      string room = await DiscoverAnyRoomAsync(5000);
      if (string.IsNullOrWhiteSpace(room)) return;

      string nickname = PlayerPrefs.GetString("lan.nickname", "VIVE");
      var info = manager.UserInfo;
      info.Nickname = nickname;
      manager.UserInfo = info;

      var data = new RoomCreateData {
        roomName = room,
        @private = false,
        maxPlayers = 16,
        silentRoom = true,
        viewOnlyRoom = false
      };

      PlayerPrefs.SetString("lan.lastRoom", room);
      PlayerPrefs.Save();
      await manager.JoinRoom(data);
    } catch (Exception ex) {
      Debug.LogWarning("[LAN AutoJoin] " + ex.Message);
    } finally {
      m_Joining = false;
    }
  }

  private static async Task<string> DiscoverAnyRoomAsync(int timeoutMs) {
    using var udp = new UdpClient();
    udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
    udp.Client.Bind(new IPEndPoint(IPAddress.Any, DiscoveryPort));

    var receive = udp.ReceiveAsync();
    var timeout = Task.Delay(timeoutMs);
    var completed = await Task.WhenAny(receive, timeout);
    if (completed != receive) return null;

    var result = receive.Result;
    string text = Encoding.UTF8.GetString(result.Buffer);
    string[] parts = text.Split('|');
    if (parts.Length == 3 && parts[0] == DiscoveryPrefix) {
      return parts[1];
    }
    return null;
  }
}

} // namespace OpenBrush.Multiplayer
