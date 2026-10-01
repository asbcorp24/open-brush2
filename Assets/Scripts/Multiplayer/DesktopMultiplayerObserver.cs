using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenBrush.Multiplayer;
using UnityEngine;
using UnityEngine.UI;

namespace TiltBrush {

/// <summary>
/// Desktop observer/admin client for the monoscopic build.
/// Uses the same Photon room and command stream as VR clients.
/// </summary>
public class DesktopMultiplayerObserver : MonoBehaviour {
  private const string kDefaultNickname = "Teacher PC";
  private const int kDefaultMaxPlayers = 12;
  private const float kAutosaveIntervalSeconds = 180f;

  private InputField m_RoomInput;
  private InputField m_NicknameInput;
  private Text m_StatusText;
  private Text m_PlayersText;
  private Button m_CreateButton;
  private Button m_JoinButton;
  private Button m_LeaveButton;
  private Button m_NewCodeButton;
  private Button m_SaveButton;
  private Button m_ClearButton;
  private Button m_AllViewButton;
  private Button m_AllDrawButton;
  private Button m_MuteAllButton;
  private Button m_UnmuteAllButton;
  private Button m_AllVrButton;
  private Button m_AllAr25Button;
  private Button m_AllAr50Button;
  private Button m_AllAr75Button;
  private Button m_AllAr100Button;

  private Transform m_PlayerRowsRoot;
  private readonly List<GameObject> m_PlayerRows = new List<GameObject>();

  private Font m_Font;
  private bool m_EventsHooked;
  private float m_NextRefreshTime;
  private float m_NextAutosaveTime;

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
      SetStatus("Starting LAN lobby...");
      var task = manager.Connect();
      yield return new WaitUntil(() => task.IsCompleted);
      if (task.IsFaulted || !task.Result) {
        SetStatus("LAN startup failed: " + (manager.LastError ?? "unknown error"));
      }
    }
  }

  private void OnDestroy() {
    UnhookEvents();
  }

  private void Update() {
    if (Input.GetKeyDown(KeyCode.F2)) {
      var canvas = GetComponentInChildren<Canvas>(true);
      if (canvas != null) {
        canvas.gameObject.SetActive(!canvas.gameObject.activeSelf);
      }
    }

    if (Time.unscaledTime >= m_NextRefreshTime) {
      m_NextRefreshTime = Time.unscaledTime + 1f;
      RefreshUi();
    }

    var manager = MultiplayerManager.m_Instance;
    if (manager != null && manager.State == ConnectionState.IN_ROOM &&
        manager.IsUserRoomOwner() && Time.unscaledTime >= m_NextAutosaveTime) {
      m_NextAutosaveTime = Time.unscaledTime + kAutosaveIntervalSeconds;
      AutoSaveSharedSketch();
    }
  }

  private void HookEvents() {
    if (m_EventsHooked || MultiplayerManager.m_Instance == null) {
      return;
    }

    var manager = MultiplayerManager.m_Instance;
    manager.StateUpdated += OnStateUpdated;
    manager.RoomOwnershipUpdated += OnRoomOwnershipUpdated;
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
    manager.RoomOwnershipUpdated -= OnRoomOwnershipUpdated;
    manager.remotePlayerJoined -= OnRemotePlayerJoined;
    manager.playerLeft -= OnPlayerLeft;
    m_EventsHooked = false;
  }

  private void OnStateUpdated(ConnectionState state) {
    RefreshUi();
  }

  private void OnRoomOwnershipUpdated(bool isOwner) {
    RefreshUi();
  }

  private void OnRemotePlayerJoined(RemotePlayer player) {
    RefreshUi();
  }

  private void OnPlayerLeft(int playerId) {
    RefreshUi();
  }

  public void GenerateNewRoomCode() {
    m_RoomInput.text = GenerateRoomCode();
    PlayerPrefs.SetString("observer.room", m_RoomInput.text);
  }

  public async void CreateRoom() {
    GenerateNewRoomCode();
    SetStatus("Starting classroom...");
    await JoinOrCreateCurrentRoom();
  }

  public void StartClass() {
    CreateRoom();
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
      silentRoom = false,
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

    m_NextAutosaveTime = Time.unscaledTime + kAutosaveIntervalSeconds;
    SetStatus("Classroom " + room + " started — VIVE headsets can auto-join");
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

  public void SaveSharedSketch() {
    if (SaveLoadScript.m_Instance == null || !SaveLoadScript.m_Instance.IsSavingAllowed()) {
      SetStatus("Save is currently unavailable.");
      return;
    }

    string room = NormalizeRoomCode(m_RoomInput.text);
    string fileName = "Multiplayer_" + (string.IsNullOrEmpty(room) ? "Room" : room) +
        "_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");

    StartCoroutine(SaveLoadScript.m_Instance.SaveAs(fileName));
    SetStatus("Saving shared sketch: " + fileName);
  }

  private void AutoSaveSharedSketch() {
    if (SaveLoadScript.m_Instance == null ||
        !SaveLoadScript.m_Instance.IsSavingAllowed() ||
        SketchMemoryScript.m_Instance == null ||
        SketchMemoryScript.m_Instance.StrokeCount == 0) {
      return;
    }

    string room = NormalizeRoomCode(m_RoomInput.text);
    string fileName = "Autosave_" + (string.IsNullOrEmpty(room) ? "Room" : room) +
        "_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
    StartCoroutine(SaveLoadScript.m_Instance.SaveAs(fileName));
    SetStatus("Autosaved classroom: " + fileName);
  }

  public void ClearSharedStrokes() {
    var manager = MultiplayerManager.m_Instance;
    if (!CanAdmin(manager)) {
      return;
    }

    var strokes = SketchMemoryScript.m_Instance.GetAllActiveStrokes().ToArray();
    if (strokes.Length == 0) {
      SetStatus("Scene has no active strokes.");
      return;
    }

    // Each DeleteStrokeCommand is recorded normally, so MultiplayerManager's
    // CommandPerformed hook broadcasts the deletion to every client.
    foreach (var stroke in strokes) {
      SketchMemoryScript.m_Instance.PerformAndRecordCommand(new DeleteStrokeCommand(stroke));
    }

    SetStatus("Cleared " + strokes.Length + " strokes for all participants.");
  }

  public void SetAllViewOnly() {
    var manager = MultiplayerManager.m_Instance;
    if (!CanAdmin(manager)) {
      return;
    }
    manager.SetRoomViewOnly(true);
    SetStatus("All VR participants switched to view-only.");
    RefreshUi();
  }

  public void AllowAllDrawing() {
    var manager = MultiplayerManager.m_Instance;
    if (!CanAdmin(manager)) {
      return;
    }
    manager.SetRoomViewOnly(false);
    SetStatus("Drawing enabled for all participants.");
    RefreshUi();
  }

  public void MuteAllForAll() {
    var manager = MultiplayerManager.m_Instance;
    if (!CanAdmin(manager)) {
      return;
    }
    foreach (var player in manager.m_RemotePlayers.List.ToArray()) {
      manager.MutePlayerForAll(true, player.PlayerId);
    }
    SetStatus("All remote participants muted.");
    RefreshUi();
  }

  public void UnmuteAllForAll() {
    var manager = MultiplayerManager.m_Instance;
    if (!CanAdmin(manager)) {
      return;
    }
    foreach (var player in manager.m_RemotePlayers.List.ToArray()) {
      manager.MutePlayerForAll(false, player.PlayerId);
    }
    SetStatus("Mute removed from all remote participants.");
    RefreshUi();
  }

  public void SetAllVr() {
    var manager = MultiplayerManager.m_Instance;
    if (!CanAdmin(manager)) return;
    manager.SetAllMixedReality(false, 0f);
    SetStatus("All VIVE headsets switched to VR.");
    RefreshUi();
  }

  public void SetAllAr25() { SetAllAr(0.25f); }
  public void SetAllAr50() { SetAllAr(0.50f); }
  public void SetAllAr75() { SetAllAr(0.75f); }
  public void SetAllAr100() { SetAllAr(1.00f); }

  private void SetAllAr(float amount) {
    var manager = MultiplayerManager.m_Instance;
    if (!CanAdmin(manager)) return;
    manager.SetAllMixedReality(true, amount);
    SetStatus("All VIVE headsets switched to AR " +
        Mathf.RoundToInt(amount * 100f) + "%.");
    RefreshUi();
  }

  private void TogglePlayerMixedReality(int playerId) {
    var manager = MultiplayerManager.m_Instance;
    if (!CanAdmin(manager)) return;

    bool isAr = manager.GetPlayerMixedRealityEnabled(playerId);
    if (isAr) {
      manager.SetPlayerMixedReality(playerId, false, 0f);
      SetStatus("Player " + playerId + " switched to VR.");
    } else {
      manager.SetPlayerMixedReality(playerId, true, 1f);
      SetStatus("Player " + playerId + " switched to AR 100%.");
    }
    RefreshUi();
  }

  private bool CanAdmin(MultiplayerManager manager) {
    if (manager == null || manager.State != ConnectionState.IN_ROOM) {
      SetStatus("Join a room first.");
      return false;
    }
    if (!manager.IsUserRoomOwner()) {
      SetStatus("Only the room owner can use teacher controls.");
      return false;
    }
    return true;
  }

  private void TogglePlayerViewOnly(int playerId) {
    var manager = MultiplayerManager.m_Instance;
    if (!CanAdmin(manager)) return;
    var player = manager.GetPlayerById(playerId);
    if (player == null) return;
    manager.SetUserViewOnlyMode(!player.m_IsViewOnly, playerId);
    RefreshUi();
  }

  private void TogglePlayerMute(int playerId) {
    var manager = MultiplayerManager.m_Instance;
    if (!CanAdmin(manager)) return;
    var player = manager.GetPlayerById(playerId);
    if (player == null) return;
    manager.MutePlayerForAll(!player.m_IsMutedForAll, playerId);
    RefreshUi();
  }

  private void KickPlayer(int playerId) {
    var manager = MultiplayerManager.m_Instance;
    if (!CanAdmin(manager)) return;
    manager.KickPlayerOut(playerId);
    SetStatus("Participant removed from room.");
  }

  private void TransferOwnership(int playerId) {
    var manager = MultiplayerManager.m_Instance;
    if (!CanAdmin(manager)) return;
    manager.RoomOwnershipTransferToUser(playerId);
    SetStatus("Room ownership transferred.");
    RefreshUi();
  }

  private void RefreshUi() {
    var manager = MultiplayerManager.m_Instance;
    if (manager == null || m_CreateButton == null) {
      return;
    }

    bool inRoom = manager.State == ConnectionState.IN_ROOM;
    bool isOwner = inRoom && manager.IsUserRoomOwner();

    m_CreateButton.interactable = !inRoom;
    m_JoinButton.interactable = !inRoom;
    m_NewCodeButton.interactable = !inRoom;
    m_RoomInput.interactable = !inRoom;
    m_NicknameInput.interactable = !inRoom;
    m_LeaveButton.interactable = inRoom;

    m_SaveButton.interactable = inRoom;
    m_ClearButton.interactable = isOwner;
    m_AllViewButton.interactable = isOwner;
    m_AllDrawButton.interactable = isOwner;
    m_MuteAllButton.interactable = isOwner;
    m_UnmuteAllButton.interactable = isOwner;
    m_AllVrButton.interactable = isOwner;
    m_AllAr25Button.interactable = isOwner;
    m_AllAr50Button.interactable = isOwner;
    m_AllAr75Button.interactable = isOwner;
    m_AllAr100Button.interactable = isOwner;

    if (manager.State == ConnectionState.ERROR) {
      SetStatus("Error: " + (manager.LastError ?? "unknown"));
    } else if (!inRoom) {
      SetStatus("State: " + manager.State);
    }

    RefreshPlayers();
    RebuildPlayerRows();
  }

  private void RefreshPlayers() {
    var manager = MultiplayerManager.m_Instance;
    if (manager == null) {
      return;
    }

    var sb = new StringBuilder();
    int remoteCount = manager.m_RemotePlayers != null ? manager.m_RemotePlayers.List.Count : 0;
    int total = (manager.State == ConnectionState.IN_ROOM ? 1 : 0) + remoteCount;

    sb.Append("Participants: ").Append(total);
    if (manager.State == ConnectionState.IN_ROOM) {
      sb.Append("   |   Role: ").Append(manager.IsUserRoomOwner() ? "Teacher / Owner" : "Observer");
    }
    m_PlayersText.text = sb.ToString();
  }

  private void RebuildPlayerRows() {
    foreach (var row in m_PlayerRows) {
      if (row != null) Destroy(row);
    }
    m_PlayerRows.Clear();

    var manager = MultiplayerManager.m_Instance;
    if (manager == null || m_PlayerRowsRoot == null || manager.m_RemotePlayers == null) {
      return;
    }

    bool canAdmin = manager.State == ConnectionState.IN_ROOM && manager.IsUserRoomOwner();
    float y = 0f;

    foreach (var player in manager.m_RemotePlayers.List.ToArray()) {
      var row = CreateUiObject("Player " + player.PlayerId, m_PlayerRowsRoot);
      m_PlayerRows.Add(row);

      var rowRect = row.GetComponent<RectTransform>();
      rowRect.anchorMin = rowRect.anchorMax = new Vector2(0f, 1f);
      rowRect.pivot = new Vector2(0f, 1f);
      rowRect.anchoredPosition = new Vector2(0f, -y);
      rowRect.sizeDelta = new Vector2(890f, 38f);

      string name = string.IsNullOrWhiteSpace(player.Nickname)
          ? "Player " + player.PlayerId : player.Nickname;
      int ping = manager.GetLanPingMilliseconds(player.PlayerId);
      string quality = manager.GetLanConnectionQuality(player.PlayerId);
      string network = ping >= 0 ? " • " + ping + " ms • " + quality : " • connecting";
      bool mrEnabled = manager.GetPlayerMixedRealityEnabled(player.PlayerId);
      float mrAmount = manager.GetPlayerMixedRealityAmount(player.PlayerId);
      string mrStatus = mrEnabled
          ? " • AR " + Mathf.RoundToInt(mrAmount * 100f) + "%"
          : " • VR";
      string provider = manager.GetPlayerMixedRealityProvider(player.PlayerId);
      string tracking = manager.GetPlayerTrackingMode(player.PlayerId);
      string deviceStatus = string.IsNullOrWhiteSpace(provider)
          ? string.Empty
          : " • " + provider +
              (string.IsNullOrWhiteSpace(tracking) ? string.Empty : " " + tracking);
      var label = CreateLabel(row.transform, name + network + mrStatus + deviceStatus,
          13, new Vector2(0, -4), new Vector2(280, 30));
      label.alignment = TextAnchor.MiddleLeft;

      int id = player.PlayerId;
      var draw = CreateButton(row.transform,
          player.m_IsViewOnly ? "Allow draw" : "View only",
          new Vector2(290, 0), new Vector2(100, 32), () => TogglePlayerViewOnly(id));
      var mute = CreateButton(row.transform,
          player.m_IsMutedForAll ? "Unmute" : "Mute",
          new Vector2(398, 0), new Vector2(80, 32), () => TogglePlayerMute(id));
      var kick = CreateButton(row.transform, "Kick",
          new Vector2(486, 0), new Vector2(65, 32), () => KickPlayer(id));
      var owner = CreateButton(row.transform, "Owner",
          new Vector2(559, 0), new Vector2(90, 32), () => TransferOwnership(id));
      var mr = CreateButton(row.transform,
          mrEnabled ? "Switch VR" : "Switch AR",
          new Vector2(657, 0), new Vector2(105, 32),
          () => TogglePlayerMixedReality(id));

      draw.interactable = canAdmin;
      mute.interactable = canAdmin;
      kick.interactable = canAdmin;
      owner.interactable = canAdmin;
      mr.interactable = canAdmin;

      y += 42f;
    }
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
    var scaler = canvasGo.AddComponent<CanvasScaler>();
    scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
    scaler.referenceResolution = new Vector2(1920, 1080);
    canvasGo.AddComponent<GraphicRaycaster>();

    var panel = CreateUiObject("Panel", canvasGo.transform);
    var panelImage = panel.AddComponent<Image>();
    panelImage.color = new Color(0.035f, 0.045f, 0.06f, 0.96f);
    var panelRect = panel.GetComponent<RectTransform>();
    panelRect.anchorMin = new Vector2(0f, 1f);
    panelRect.anchorMax = new Vector2(0f, 1f);
    panelRect.pivot = new Vector2(0f, 1f);
    panelRect.anchoredPosition = new Vector2(18f, -18f);
    panelRect.sizeDelta = new Vector2(940f, 760f);

    CreateLabel(panel.transform, "OPEN BRUSH — TEACHER OBSERVER", 22,
        new Vector2(18, -18), new Vector2(900, 32));

    CreateLabel(panel.transform, "Room code", 14,
        new Vector2(18, -60), new Vector2(100, 24));
    m_RoomInput = CreateInput(panel.transform, new Vector2(18, -86),
        new Vector2(220, 34), "123456");
    m_NewCodeButton = CreateButton(panel.transform, "New code",
        new Vector2(248, -86), new Vector2(120, 34), GenerateNewRoomCode);

    CreateLabel(panel.transform, "PC name", 14,
        new Vector2(388, -60), new Vector2(100, 24));
    m_NicknameInput = CreateInput(panel.transform, new Vector2(388, -86),
        new Vector2(330, 34), kDefaultNickname);

    m_CreateButton = CreateButton(panel.transform, "START CLASS",
        new Vector2(18, -136), new Vector2(170, 38), StartClass);
    m_JoinButton = CreateButton(panel.transform, "Join room",
        new Vector2(198, -136), new Vector2(170, 38), JoinRoom);
    m_LeaveButton = CreateButton(panel.transform, "Leave",
        new Vector2(378, -136), new Vector2(160, 38), LeaveRoom);
    m_SaveButton = CreateButton(panel.transform, "Save sketch",
        new Vector2(548, -136), new Vector2(170, 38), SaveSharedSketch);

    m_StatusText = CreateLabel(panel.transform, "State: starting...", 14,
        new Vector2(18, -184), new Vector2(700, 30));
    m_PlayersText = CreateLabel(panel.transform, "Participants: 0", 14,
        new Vector2(18, -214), new Vector2(700, 28));

    CreateLabel(panel.transform, "Teacher controls", 16,
        new Vector2(18, -252), new Vector2(180, 26));

    m_AllViewButton = CreateButton(panel.transform, "All view-only",
        new Vector2(18, -282), new Vector2(135, 34), SetAllViewOnly);
    m_AllDrawButton = CreateButton(panel.transform, "Allow all draw",
        new Vector2(163, -282), new Vector2(135, 34), AllowAllDrawing);
    m_MuteAllButton = CreateButton(panel.transform, "Mute all",
        new Vector2(308, -282), new Vector2(110, 34), MuteAllForAll);
    m_UnmuteAllButton = CreateButton(panel.transform, "Unmute all",
        new Vector2(428, -282), new Vector2(110, 34), UnmuteAllForAll);
    m_ClearButton = CreateButton(panel.transform, "Clear strokes",
        new Vector2(548, -282), new Vector2(170, 34), ClearSharedStrokes);

    CreateLabel(panel.transform, "Mixed Reality", 16,
        new Vector2(18, -332), new Vector2(180, 26));

    m_AllVrButton = CreateButton(panel.transform, "VR ALL",
        new Vector2(18, -362), new Vector2(115, 34), SetAllVr);
    m_AllAr25Button = CreateButton(panel.transform, "AR 25%",
        new Vector2(143, -362), new Vector2(115, 34), SetAllAr25);
    m_AllAr50Button = CreateButton(panel.transform, "AR 50%",
        new Vector2(268, -362), new Vector2(115, 34), SetAllAr50);
    m_AllAr75Button = CreateButton(panel.transform, "AR 75%",
        new Vector2(393, -362), new Vector2(115, 34), SetAllAr75);
    m_AllAr100Button = CreateButton(panel.transform, "AR 100%",
        new Vector2(518, -362), new Vector2(125, 34), SetAllAr100);

    CreateLabel(panel.transform, "Participants", 16,
        new Vector2(18, -412), new Vector2(180, 26));

    var rows = CreateUiObject("Player Rows", panel.transform);
    var rowsRect = rows.GetComponent<RectTransform>();
    rowsRect.anchorMin = rowsRect.anchorMax = new Vector2(0f, 1f);
    rowsRect.pivot = new Vector2(0f, 1f);
    rowsRect.anchoredPosition = new Vector2(18, -446);
    rowsRect.sizeDelta = new Vector2(890, 250);
    m_PlayerRowsRoot = rows.transform;

    var hint = CreateLabel(panel.transform,
        "F2 — hide/show panel. Autosave every 3 min. VIVE headsets auto-join over Wi-Fi.",
        12, new Vector2(18, -728), new Vector2(900, 22));
    hint.color = new Color(1f, 1f, 1f, 0.6f);
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
    var text = CreateLabel(go.transform, "", 17,
        new Vector2(8, -5), new Vector2(sizeDelta.x - 16, sizeDelta.y - 8));
    text.alignment = TextAnchor.MiddleLeft;
    input.textComponent = text;

    var ph = CreateLabel(go.transform, placeholder, 17,
        new Vector2(8, -5), new Vector2(sizeDelta.x - 16, sizeDelta.y - 8));
    ph.color = new Color(1f, 1f, 1f, 0.35f);
    ph.alignment = TextAnchor.MiddleLeft;
    input.placeholder = ph;
    return input;
  }

  private Button CreateButton(Transform parent, string text, Vector2 pos,
      Vector2 sizeDelta, UnityEngine.Events.UnityAction action) {
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

    var label = CreateLabel(go.transform, text, 14, Vector2.zero, sizeDelta);
    label.alignment = TextAnchor.MiddleCenter;
    return button;
  }
}

} // namespace TiltBrush
