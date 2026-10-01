using System;
using System.Collections;
using System.Text;
using OpenBrush.Multiplayer;
using UnityEngine;
using UnityEngine.UI;

namespace TiltBrush {

/// <summary>
/// Lightweight desktop observer/admin overlay for the monoscopic build.
/// It uses the exact same MultiplayerManager/Photon room as VR clients, so the
/// PC sees the synchronized Open Brush scene without requiring a headset.
/// </summary>
public class DesktopMultiplayerObserver : MonoBehaviour {
  private const string kDefaultNickname = "Observer";
  private const int kDefaultMaxPlayers = 12;

  private InputField m_RoomInput;
  private InputField m_NicknameInput;
  private Text m_StatusText;
  private Text m_PlayersText;
  private Button m_CreateButton;
  private Button m_JoinButton;
  private Button m_LeaveButton;
  private Button m_NewCodeButton;

  private Font m_Font;
  private bool m_EventsHooked;

  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
  private static void Bootstrap() {
    bool nonVr = App.Config != null && App.Config.m_SdkMode == SdkMode.Monoscopic;
    if (!nonVr && App.VrSdk != null) {
      nonVr = !App.VrSdk.IsHmdInitialized();
    }

    if (!nonVr || FindFirstObjectByType<DesktopMultiplayerObserver>() != null) {
      return;
    }

    var go = new GameObject("Desktop Multiplayer Observer");
    DontDestroyOnLoad(go);
    go.AddComponent<DesktopMultiplayerObserver>();
  }

  private IEnumerator Start() {
    m_Font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    BuildUi();

    m_RoomInput.text = PlayerPrefs.GetString("observer.room", GenerateRoomCode());
    m_NicknameInput.text = PlayerPrefs.GetString("observer.nickname", kDefaultNickname);

    while (MultiplayerManager.m_Instance == null) {
      SetStatus("Waiting for multiplayer...");
      yield return null;
    }

    HookEvents();
    RefreshUi();

    var manager = MultiplayerManager.m_Instance;
    if (manager.State == ConnectionState.INITIALIZED ||
        manager.State == ConnectionState.DISCONNECTED) {
      SetStatus("Connecting to Photon lobby...");
      var task = manager.Connect();
      yield return new WaitUntil(() => task.IsCompleted);
      if (task.IsFaulted || !task.Result) {
        SetStatus("Photon connection failed: " + (manager.LastError ?? "unknown error"));
      }
    }
  }

  private void OnDestroy() {
    UnhookEvents();
  }

  private void Update() {
    if (Input.GetKeyDown(KeyCode.F2)) {
      gameObject.SetActive(!gameObject.activeSelf);
    }
  }

  private void HookEvents() {
    if (m_EventsHooked || MultiplayerManager.m_Instance == null) {
      return;
    }

    var manager = MultiplayerManager.m_Instance;
    manager.StateUpdated += OnStateUpdated;
    manager.remotePlayerJoined += OnRemotePlayerJoined;
    manager.playerLeft += OnPlayerLeft;
    m_EventsHooked = true;
  }

  private void UnhookEvents() {
    if (!m_EventsHooked || MultiplayerManager.m_Instance == null) {
      return;
    }

    var manager = MultiplayerManager.m_Instance;
    manager.StateUpdated -= OnStateUpdated;
    manager.remotePlayerJoined -= OnRemotePlayerJoined;
    manager.playerLeft -= OnPlayerLeft;
    m_EventsHooked = false;
  }

  private void OnStateUpdated(ConnectionState state) {
    RefreshUi();
  }

  private void OnRemotePlayerJoined(RemotePlayer player) {
    RefreshPlayers();
  }

  private void OnPlayerLeft(int playerId) {
    RefreshPlayers();
  }

  public void GenerateNewRoomCode() {
    m_RoomInput.text = GenerateRoomCode();
    PlayerPrefs.SetString("observer.room", m_RoomInput.text);
  }

  public async void CreateRoom() {
    // JoinRoom creates a room when the requested room does not exist.
    GenerateNewRoomCode();
    await JoinOrCreateCurrentRoom();
  }

  public async void JoinRoom() {
    await JoinOrCreateCurrentRoom();
  }

  private async System.Threading.Tasks.Task JoinOrCreateCurrentRoom() {
    var manager = MultiplayerManager.m_Instance;
    if (manager == null) {
      SetStatus("MultiplayerManager is not ready.");
      return;
    }

    if (manager.State == ConnectionState.INITIALIZED ||
        manager.State == ConnectionState.DISCONNECTED) {
      SetStatus("Connecting...");
      bool connected = await manager.Connect();
      if (!connected) {
        SetStatus("Connection failed: " + (manager.LastError ?? "unknown error"));
        return;
      }
    }

    if (!manager.CanJoinRoom()) {
      SetStatus("Not ready to join a room. State: " + manager.State);
      return;
    }

    string room = NormalizeRoomCode(m_RoomInput.text);
    if (string.IsNullOrEmpty(room)) {
      room = GenerateRoomCode();
      m_RoomInput.text = room;
    }

    string nickname = string.IsNullOrWhiteSpace(m_NicknameInput.text)
        ? kDefaultNickname : m_NicknameInput.text.Trim();

    manager.UserInfo = new ConnectionUserInfo {
      Nickname = nickname,
      UserId = manager.UserInfo.UserId,
      Role = manager.UserInfo.Role
    };

    var roomData = new RoomCreateData {
      roomName = room,
      @private = false,
      maxPlayers = kDefaultMaxPlayers,
      silentRoom = true,
      viewOnlyRoom = false
    };

    PlayerPrefs.SetString("observer.room", room);
    PlayerPrefs.SetString("observer.nickname", nickname);
    PlayerPrefs.Save();

    SetStatus("Joining room " + room + "...");
    bool success = await manager.JoinRoom(roomData);
    if (!success) {
      SetStatus("Unable to join room: " + (manager.LastError ?? "unknown error"));
      return;
    }

    SetStatus("Room " + room + " — observing live");
    RefreshUi();
  }

  public async void LeaveRoom() {
    var manager = MultiplayerManager.m_Instance;
    if (manager == null || !manager.CanLeaveRoom()) {
      return;
    }

    SetStatus("Leaving room...");
    await manager.LeaveRoom(false);
    RefreshUi();
  }

  private void RefreshUi() {
    var manager = MultiplayerManager.m_Instance;
    if (manager == null) {
      return;
    }

    bool inRoom = manager.State == ConnectionState.IN_ROOM;
    m_CreateButton.interactable = !inRoom;
    m_JoinButton.interactable = !inRoom;
    m_NewCodeButton.interactable = !inRoom;
    m_RoomInput.interactable = !inRoom;
    m_NicknameInput.interactable = !inRoom;
    m_LeaveButton.interactable = inRoom;

    if (manager.State == ConnectionState.ERROR) {
      SetStatus("Error: " + (manager.LastError ?? "unknown"));
    } else if (!inRoom) {
      SetStatus("State: " + manager.State);
    }

    RefreshPlayers();
  }

  private void RefreshPlayers() {
    var manager = MultiplayerManager.m_Instance;
    if (manager == null) {
      return;
    }

    var sb = new StringBuilder();
    int remoteCount = manager.m_RemotePlayers != null ? manager.m_RemotePlayers.List.Count : 0;
    int total = (manager.State == ConnectionState.IN_ROOM ? 1 : 0) + remoteCount;

    sb.AppendLine("Participants: " + total);
    if (manager.State == ConnectionState.IN_ROOM) {
      string localName = string.IsNullOrWhiteSpace(manager.UserInfo.Nickname)
          ? kDefaultNickname : manager.UserInfo.Nickname;
      sb.AppendLine("• " + localName + " (PC" +
          (manager.IsUserRoomOwner() ? ", owner" : "") + ")");
    }

    if (manager.m_RemotePlayers != null) {
      foreach (var player in manager.m_RemotePlayers.List) {
        string name = string.IsNullOrWhiteSpace(player.Nickname)
            ? "Player " + player.PlayerId : player.Nickname;
        sb.Append("• ").Append(name);
        if (manager.IsPlayerRoomOwner(player.PlayerId)) {
          sb.Append(" (owner)");
        }
        if (player.m_IsViewOnly) {
          sb.Append(" (view only)");
        }
        sb.AppendLine();
      }
    }

    m_PlayersText.text = sb.ToString();
  }

  private void SetStatus(string value) {
    if (m_StatusText != null) {
      m_StatusText.text = value;
    }
  }

  private static string GenerateRoomCode() {
    return UnityEngine.Random.Range(100000, 1000000).ToString();
  }

  private static string NormalizeRoomCode(string raw) {
    if (string.IsNullOrWhiteSpace(raw)) {
      return string.Empty;
    }

    var sb = new StringBuilder(6);
    foreach (char c in raw) {
      if (char.IsDigit(c) && sb.Length < 6) {
        sb.Append(c);
      }
    }
    return sb.ToString();
  }

  private void BuildUi() {
    var canvasGo = new GameObject("Observer Canvas");
    canvasGo.transform.SetParent(transform, false);

    var canvas = canvasGo.AddComponent<Canvas>();
    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
    canvas.sortingOrder = 5000;
    canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
    canvasGo.AddComponent<GraphicRaycaster>();

    var panel = CreateUiObject("Panel", canvasGo.transform);
    var panelImage = panel.AddComponent<Image>();
    panelImage.color = new Color(0.035f, 0.045f, 0.06f, 0.94f);
    var panelRect = panel.GetComponent<RectTransform>();
    panelRect.anchorMin = new Vector2(0f, 1f);
    panelRect.anchorMax = new Vector2(0f, 1f);
    panelRect.pivot = new Vector2(0f, 1f);
    panelRect.anchoredPosition = new Vector2(18f, -18f);
    panelRect.sizeDelta = new Vector2(390f, 410f);

    CreateLabel(panel.transform, "OPEN BRUSH — OBSERVER", 22, new Vector2(18, -18), new Vector2(350, 32));
    CreateLabel(panel.transform, "Room code", 14, new Vector2(18, -62), new Vector2(100, 24));
    m_RoomInput = CreateInput(panel.transform, new Vector2(18, -88), new Vector2(210, 34), "123456");
    m_NewCodeButton = CreateButton(panel.transform, "New code", new Vector2(238, -88), new Vector2(130, 34), GenerateNewRoomCode);

    CreateLabel(panel.transform, "PC name", 14, new Vector2(18, -132), new Vector2(100, 24));
    m_NicknameInput = CreateInput(panel.transform, new Vector2(18, -158), new Vector2(350, 34), kDefaultNickname);

    m_CreateButton = CreateButton(panel.transform, "Create room", new Vector2(18, -208), new Vector2(170, 40), CreateRoom);
    m_JoinButton = CreateButton(panel.transform, "Join room", new Vector2(198, -208), new Vector2(170, 40), JoinRoom);
    m_LeaveButton = CreateButton(panel.transform, "Leave", new Vector2(18, -258), new Vector2(350, 36), LeaveRoom);

    m_StatusText = CreateLabel(panel.transform, "State: starting...", 14, new Vector2(18, -306), new Vector2(350, 42));
    m_PlayersText = CreateLabel(panel.transform, "Participants: 0", 14, new Vector2(18, -352), new Vector2(350, 110));
  }

  private GameObject CreateUiObject(string name, Transform parent) {
    var go = new GameObject(name, typeof(RectTransform));
    go.transform.SetParent(parent, false);
    return go;
  }

  private Text CreateLabel(Transform parent, string text, int size, Vector2 pos, Vector2 sizeDelta) {
    var go = CreateUiObject("Text", parent);
    var rect = go.GetComponent<RectTransform>();
    rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
    rect.pivot = new Vector2(0f, 1f);
    rect.anchoredPosition = pos;
    rect.sizeDelta = sizeDelta;

    var label = go.AddComponent<Text>();
    label.font = m_Font;
    label.fontSize = size;
    label.color = Color.white;
    label.alignment = TextAnchor.UpperLeft;
    label.text = text;
    label.horizontalOverflow = HorizontalWrapMode.Wrap;
    label.verticalOverflow = VerticalWrapMode.Overflow;
    return label;
  }

  private InputField CreateInput(Transform parent, Vector2 pos, Vector2 sizeDelta, string placeholder) {
    var go = CreateUiObject("Input", parent);
    var rect = go.GetComponent<RectTransform>();
    rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
    rect.pivot = new Vector2(0f, 1f);
    rect.anchoredPosition = pos;
    rect.sizeDelta = sizeDelta;

    var image = go.AddComponent<Image>();
    image.color = new Color(0.12f, 0.14f, 0.18f, 1f);

    var input = go.AddComponent<InputField>();
    var text = CreateLabel(go.transform, "", 17, new Vector2(8, -5), new Vector2(sizeDelta.x - 16, sizeDelta.y - 8));
    text.alignment = TextAnchor.MiddleLeft;
    input.textComponent = text;

    var ph = CreateLabel(go.transform, placeholder, 17, new Vector2(8, -5), new Vector2(sizeDelta.x - 16, sizeDelta.y - 8));
    ph.color = new Color(1f, 1f, 1f, 0.35f);
    ph.alignment = TextAnchor.MiddleLeft;
    input.placeholder = ph;
    return input;
  }

  private Button CreateButton(Transform parent, string text, Vector2 pos, Vector2 sizeDelta, UnityEngine.Events.UnityAction action) {
    var go = CreateUiObject("Button " + text, parent);
    var rect = go.GetComponent<RectTransform>();
    rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
    rect.pivot = new Vector2(0f, 1f);
    rect.anchoredPosition = pos;
    rect.sizeDelta = sizeDelta;

    var image = go.AddComponent<Image>();
    image.color = new Color(0.14f, 0.35f, 0.62f, 1f);

    var button = go.AddComponent<Button>();
    button.targetGraphic = image;
    button.onClick.AddListener(action);

    var label = CreateLabel(go.transform, text, 16, Vector2.zero, sizeDelta);
    label.alignment = TextAnchor.MiddleCenter;
    return button;
  }
}

} // namespace TiltBrush
