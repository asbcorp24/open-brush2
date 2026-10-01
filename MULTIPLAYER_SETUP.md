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


## Primitives panel

A dedicated runtime **PRIMITIVES** panel is now created from the native
GuideTools panel shell so it behaves like a normal Open Brush VR panel.

Available shapes:

- Cube
- Sphere
- Cylinder
- Cone
- Pyramid
- Plane
- Capsule
- Ellipsoid
- Dome

The buttons use the existing Open Brush `StencilWidget` /
`CreateWidgetCommand` system. The resulting shapes can therefore be grabbed,
rotated, scaled, undone/redone and saved with the sketch.

LAN classroom synchronization now also transports primitive state:

- creation;
- move/rotation/scale;
- visibility/hide;
- undo/redo resulting state;
- existing primitives are sent to late joiners.

Each primitive receives a stable `NetworkPrimitiveId` so all clients update the
same object.


## Primitive properties panel

A second native VR panel, **PRIMITIVE PROPERTIES**, is created next to the
PRIMITIVES panel. Grab or select a stencil/primitive and the property panel acts
on that object.

Controls now include:

- X / Y / Z size decrease and increase;
- precise size step cycling: **1 cm / 5 cm / 10 cm**;
- current X/Y/Z dimensions are reported after resizing;
- duplicate primitive;
- snap position to the current Open Brush grid;
- cycle primitive color palette;
- transparency levels: 100% / 65% / 35%;
- wireframe toggle using a generated edge mesh (not shader-dependent);
- material modes: **Solid / Metallic / Emissive**.

Primitive visual state and material mode are included in LAN primitive packets,
so all headsets and the Teacher PC see the same result.

Color, alpha, wireframe and material mode are also stored as optional guide
metadata in the .tilt file and restored when the sketch is loaded. Older .tilt
files remain compatible because the added metadata fields are optional.

The last primitive grabbed with a controller automatically becomes the active
primitive for this properties panel.


## VIVE Focus Vision Mixed Reality / Passthrough

The VIVE Focus Vision build now includes an optional **MIXED REALITY** panel.

Modes:

- VR
- AR 25%
- AR 50%
- AR 75%
- AR 100%

AR uses VIVE OpenXR planar passthrough as an **Underlay**. The headset camera
background is switched to a transparent solid-color clear so the real-world
passthrough is visible behind Open Brush strokes, primitives and other virtual
content.

The runtime adapter resolves:

`VIVE.OpenXR.Passthrough.PassthroughAPI`

at runtime and calls `CreatePlanarPassthrough(LayerType.Underlay)`. Reflection
is used intentionally so the Windows Observer/editor code is not hard-linked to
the HTC runtime types.

The selected MR mode and amount are stored in local PlayerPrefs on the headset.

### Teacher MR controls

Windows Teacher Observer includes:

- **VR ALL**
- **AR 25%**
- **AR 50%**
- **AR 75%**
- **AR 100%**

Each participant row also shows the reported state, for example:

`VIVE 03 • 12 ms • excellent • AR 75%`

and provides an individual **Switch AR / Switch VR** control.

Teacher MR commands travel only through the existing LAN TCP connection. No
internet service is involved.

After applying a remote MR command, the headset sends its current MR state back
to the teacher PC. It also reports its saved state after joining or reconnecting.

### OpenXR feature

HTC VIVE OpenXR Plugin 2.5.1 or newer is required.

For the Android target, **VIVE XR Passthrough** must be enabled under:

`Project Settings > XR Plug-in Management > OpenXR`

The VIVE Focus Vision multiplayer build command attempts to find and enable the
VIVE/HTC passthrough OpenXR feature automatically after the package has been
imported. If Unity has not yet generated the feature objects, the build log
prints a warning and the feature can be enabled manually.

### Offline behavior

Passthrough is produced locally by the VIVE Focus Vision cameras/OpenXR runtime.
It does not require WAN/internet connectivity. Teacher commands also remain on
the classroom LAN.
