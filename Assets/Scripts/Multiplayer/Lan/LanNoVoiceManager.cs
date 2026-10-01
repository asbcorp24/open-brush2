using System.Threading.Tasks;

namespace OpenBrush.Multiplayer {

public class LanNoVoiceManager : IVoiceConnectionHandler {
  public ConnectionState State { get; private set; } = ConnectionState.INITIALIZED;
  public ConnectionUserInfo UserInfo { get; set; }
  public string LastError { get; private set; }
  public bool isTransmitting => false;

  public Task<bool> Connect() { State = ConnectionState.IN_LOBBY; return Task.FromResult(true); }
  public Task<bool> JoinRoom(RoomCreateData data) { State = ConnectionState.IN_ROOM; return Task.FromResult(true); }
  public Task<bool> LeaveRoom(bool force = false) { State = ConnectionState.IN_LOBBY; return Task.FromResult(true); }
  public Task<bool> Disconnect() { State = ConnectionState.DISCONNECTED; return Task.FromResult(true); }
  public void Update() {}
  public bool StartSpeaking() => false;
  public bool StopSpeaking() => false;
}

} // namespace OpenBrush.Multiplayer
