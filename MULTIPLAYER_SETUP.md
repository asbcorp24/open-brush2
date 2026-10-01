# Open Brush LAN Multiplayer — VIVE Focus Vision + Windows Teacher PC

This branch uses **local-network multiplayer only**.

There is no Photon dependency in the active multiplayer path.
No internet connection, Photon App ID, Fusion account, or Voice account is required.

## Architecture

- Windows PC runs `OpenBrushObserver.exe`.
- The Windows client is the LAN room host.
- HTC VIVE Focus Vision headsets run the Android standalone APK.
- All devices must be connected to the same local Wi-Fi/LAN.
- The router does **not** need internet access.
- Headsets find the host automatically by the six-digit room code using UDP broadcast.
- Reliable scene/command traffic uses direct TCP connections to the PC.

Default ports:

- UDP discovery: `45870`
- TCP multiplayer: `45871`

Allow these ports through Windows Firewall on the teacher PC.

## Build targets

### Windows Teacher / Observer

Unity menu:

`Open Brush > Multiplayer > Build Windows Observer`

Output:

`Builds/Multiplayer/WindowsObserver/OpenBrushObserver.exe`

The PC application runs in Monoscopic mode, hosts the LAN room, displays the live
shared 3D scene and provides teacher controls.

### HTC VIVE Focus Vision

Unity menu:

`Open Brush > Multiplayer > Build VIVE Focus Vision Multiplayer APK`

Output:

`Builds/Multiplayer/ViveFocusVision/OpenBrushViveFocusVisionMultiplayer.apk`

The headset build uses Android ARM64 + OpenXR + IL2CPP.

The project includes the VIVE OpenXR package through the configured VIVE scoped
registry. After package import, run Unity OpenXR Project Validation for Android.

## Classroom flow

1. Connect the teacher PC and all VIVE Focus Vision headsets to the same Wi-Fi.
2. Internet access may be completely disabled.
3. Start `OpenBrushObserver.exe` on the teacher PC.
4. Press **Create room**.
5. The PC generates a six-digit room code, for example `583921`.
6. Open Open Brush on every VIVE Focus Vision headset.
7. Open Multiplayer and enter the same six-digit code.
8. Each headset listens for LAN announcements for that room.
9. When the PC is found, the headset connects directly to the PC.
10. Drawing is synchronized between the PC and all connected headsets.

## Late join

When a new headset joins an existing room, the Windows host sends the current
stroke state through the existing `MultiplayerSceneSync` flow.

The joining headset clears its local sketch before applying the room state.

## Live synchronization

LAN transport currently handles:

- brush strokes;
- stroke deletion;
- undo/redo identity for synchronized commands;
- late-join stroke scene sync;
- head pose;
- left/right controller pose;
- tool pose;
- nickname;
- room owner state;
- view-only controls;
- kick;
- ownership transfer;
- manual co-location reference;
- participant join/leave.

Voice is intentionally disabled in LAN mode. It can be added later as a separate
local voice feature without introducing an internet dependency.

## Teacher controls

The Windows Observer provides:

- create room;
- join/leave;
- participant list;
- save shared sketch;
- clear synchronized strokes;
- all users view-only;
- allow everyone to draw;
- per-user view-only / allow draw;
- kick user;
- transfer ownership.

Mute buttons remain UI-compatible, but LAN mode currently has no voice channel.

Press **F2** to hide/show the teacher panel while keeping the live 3D scene visible.

## Network requirements

No WAN/internet route is required.

A normal Wi-Fi router or access point is enough:

```
VIVE Focus Vision 1 ─┐
VIVE Focus Vision 2 ─┤
VIVE Focus Vision 3 ─┼── local Wi-Fi/LAN ── Windows Teacher PC
VIVE Focus Vision 4 ─┘
```

Recommended:

- 5 GHz or 6 GHz Wi-Fi;
- all headsets on the same SSID/VLAN;
- disable AP/client isolation;
- Windows network profile set to Private;
- allow UDP 45870 and TCP 45871 through Windows Firewall.

## Offline verification test

1. Disconnect the router WAN/internet cable.
2. Keep Wi-Fi enabled.
3. Start Windows Observer.
4. Create a room.
5. Join from two VIVE Focus Vision headsets.
6. Draw on headset A.
7. Verify it appears on headset B and PC.
8. Draw on headset B.
9. Verify it appears on headset A and PC.
10. Join a third headset after strokes already exist.
11. Verify the current sketch is restored.
12. Test view-only, kick, save, clear and ownership controls.

If this test succeeds with the WAN cable disconnected, the installation is fully
offline.


## Classroom automation

The LAN classroom workflow now includes four reliability features:

### Start class / automatic headset join

On the Windows Teacher app press **START CLASS**.

The PC creates a six-digit room and starts advertising it on UDP 45870.
VIVE Focus Vision builds contain `LanAutoJoin`, which listens for the first
advertised Open Brush classroom on the local Wi-Fi and joins it automatically.

For a dedicated classroom network this means students do not need to type an IP
address or room code each lesson.

### Automatic reconnect

If a headset temporarily loses Wi-Fi, the LAN transport switches to
`RECONNECTING`, searches for the same room again and reconnects directly to the
teacher PC. The headset keeps a stable local user identity so it can reuse its
player id when possible.

TCP read/write timeouts are set to 5 seconds so a broken Wi-Fi link is detected
quickly instead of remaining stuck for a long OS TCP timeout.

### Heartbeat / ping

The teacher PC sends a heartbeat once per second. Each headset responds locally.
The Teacher Observer shows per-headset latency and a simple quality label:

- <= 30 ms: excellent
- <= 80 ms: good
- <= 160 ms: unstable
- > 160 ms: poor

A peer that stops responding for more than 6 seconds is closed and removed.

### Autosave

While the Windows Teacher PC owns an active classroom, the synchronized sketch is
automatically saved every 3 minutes when there are strokes and no other save is in
progress.

Autosave files are named like:

`Autosave_583921_2026-10-01_10-15-00.tilt`

Manual **Save sketch** remains available as well.
