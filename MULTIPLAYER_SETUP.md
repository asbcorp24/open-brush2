# Multiplayer setup for this fork

This fork already contains Open Brush's multiplayer implementation:

- Photon Fusion room/lobby connection
- remote head and controller pose sync
- brush/color/size sync
- command and stroke replication
- late-join scene synchronization
- undo/redo synchronization
- room ownership, kick, mute and view-only modes
- Photon Voice
- manual co-location support

The Main scene already has **MultiplayerManager** enabled with
`m_MultiplayerType: Photon`.

## 1. Install the Photon SDKs

Use the versions supported by upstream Open Brush:

- Photon Fusion 2.0.3
- Photon Voice 2

Upstream also provides a combined mirror package:

https://github.com/icosa-mirror/photon-fusion/releases/tag/Fusion_v2_Voice_2

Copy/import the SDK contents into the Unity project's `Assets` folder and restart Unity.

Do not enable `MP_PHOTON` before the SDKs are present.

## 2. Enable multiplayer code

In Unity use:

`Open Brush > Multiplayer > Enable Photon Multiplayer`

The helper added in this branch checks that both SDKs are loaded and then adds
`MP_PHOTON` to both Standalone and Android scripting define symbols.

You can verify at any time with:

`Open Brush > Multiplayer > Validate Photon Setup`

## 3. Create Photon applications

Create two Photon applications in the Photon dashboard:

1. **Fusion** app
2. **Voice** app

Keep the App IDs private.

## 4. Configure Open Brush Secrets.asset

Create the normal Open Brush `Secrets.asset` if one does not already exist.

Add:

- `Photon Fusion` -> put the Fusion App ID in **Client ID**
- `Photon Voice` -> put the Voice App ID in **Client ID**

The runtime reads the Fusion ID through
`App.Config.PhotonFusionSecrets.ClientId` and the Voice configuration through
the existing Photon Voice integration.

Never commit your real `Secrets.asset`.

## 5. Test two VR devices

Build the same branch for two devices.

On device A:

1. Open Multiplayer.
2. Connect.
3. Create/join a room, for example `SCI-101`.

On device B:

1. Open Multiplayer.
2. Connect.
3. Join the same `SCI-101` room.

Both users should see each other's head/controllers and brush state. Commands and
strokes are distributed through the existing Open Brush command synchronization,
and a late joiner receives the current scene through `MultiplayerSceneSync`.

## Recommended first test

Use two VIVE Focus Vision headsets on the same Wi-Fi network, but keep internet access enabled
for Photon Cloud. Draw one simple stroke on each device, test undo/redo, then test
joining the room after several strokes already exist.

## Important

`com.unity.multiplayer.center` in Packages/manifest.json is not the networking
backend used by this implementation. The actual backend is the code under:

`Assets/Scripts/Multiplayer/Photon/`

and it is compiled only when `MP_PHOTON` (and Fusion's own generated symbols)
are active.


# Dedicated Windows Observer / Admin client

This branch also adds a dedicated desktop observer workflow.

The Windows observer uses the same Open Brush Main scene and the same Photon room
as VIVE Focus Vision clients, but is built in **Monoscopic** mode. It does not require a VR
headset.

When launched without VR it automatically creates the
`DesktopMultiplayerObserver` overlay.

The overlay provides:

- six-digit room code
- **New code**
- **Create room**
- **Join room**
- **Leave**
- connection status
- participant count/list
- indication of the current room owner

If **Create room** is pressed, a fresh six-digit code is generated. Photon Fusion's
Join/Create behavior then creates that room. VIVE Focus Vision users enter the same code.

The desktop client receives the same Open Brush command stream and scene snapshot,
so strokes made by VR participants appear on the PC display.

## Build the PC application

After Photon is configured:

`Open Brush > Multiplayer > Build Windows Observer`

Output:

`Builds/Multiplayer/WindowsObserver/OpenBrushObserver.exe`

## Build the VIVE Focus Vision application

After Photon is configured:

`Open Brush > Multiplayer > Build VIVE Focus Vision Multiplayer APK`

Output:

`Builds/Multiplayer/VIVE Focus Vision/OpenBrushMultiplayer.apk`

The VIVE Focus Vision build uses Android + OpenXR + IL2CPP.

# End-to-end test

1. Start `OpenBrushObserver.exe` on the PC.
2. Press **Create room** and note the six-digit code.
3. Install `OpenBrushMultiplayer.apk` on VIVE Focus Vision A and VIVE Focus Vision B.
4. Open Multiplayer on both headsets.
5. Enter the same room code and join.
6. Draw on VIVE Focus Vision A.
7. Verify the stroke appears on VIVE Focus Vision B and on the PC.
8. Draw on VIVE Focus Vision B.
9. Verify the stroke appears on VIVE Focus Vision A and on the PC.
10. Undo on a headset and verify the command is reflected on the other clients.
11. Join a third client after strokes already exist and verify
    `MultiplayerSceneSync` restores the current scene.

# Current external requirements

The repository code path is prepared, but a real Photon deployment still requires
credentials that must belong to your Photon account and therefore are not committed:

- Photon Fusion App ID
- Photon Voice App ID

The Photon SDK binaries/packages also remain an external dependency. The editor
validator deliberately refuses to enable/build multiplayer when those SDK types are
missing.


# Teacher controls in Windows Observer

When the Windows client owns the room, the observer panel now provides classroom
administration controls:

- **All view-only** — prevent all connected VR participants from drawing.
- **Allow all draw** — re-enable drawing for everyone.
- **Mute all / Unmute all** — control room voice for remote participants.
- **Clear strokes** — deletes every active stroke by issuing normal
  `DeleteStrokeCommand` commands. Because these are recorded through
  `SketchMemoryScript`, the existing multiplayer command hook broadcasts the
  deletions to all connected clients.
- **Save sketch** — saves the synchronized room state to a local `.tilt` file on
  the teacher PC using a timestamped `Multiplayer_<room>_...` filename.

Each participant also receives an admin row with:

- **View only / Allow draw**
- **Mute / Unmute**
- **Kick**
- **Make owner**

These participant controls are enabled only when the Windows client is the current
room owner. Ownership can be transferred to another participant.

The teacher window can be hidden/shown with **F2** while the live 3D scene remains
visible behind it.


# VIVE Focus Vision target

The Android multiplayer build in this branch now targets **HTC VIVE Focus Vision**
as a standalone headset.

Project changes:

- added `com.htc.upm.vive.openxr` version 2.5.1 from the already configured
  VIVE scoped registry;
- keeps Android ARM64 + OpenXR + IL2CPP;
- build validation now requires the VIVE OpenXR package in addition to Photon;
- Android output is now:
  `Builds/Multiplayer/ViveFocusVision/OpenBrushViveFocusVisionMultiplayer.apk`;
- build menu:
  `Open Brush > Multiplayer > Build VIVE Focus Vision Multiplayer APK`.

After Unity imports the VIVE package, open:

`Edit > Project Settings > XR Plug-in Management > OpenXR`

and run **Project Validation / Fix All**. Ensure the VIVE XR support/controller
interaction profile for Focus devices is enabled for Android.

On the headset enable USB debugging:

`Settings > Developer options > USB debugging`

Then the generated APK can be installed directly on VIVE Focus Vision.

The Windows Observer remains unchanged and joins the same Photon room as all
Focus Vision headsets.
