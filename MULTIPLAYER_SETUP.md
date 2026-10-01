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

Use two Quest headsets on the same Wi-Fi network, but keep internet access enabled
for Photon Cloud. Draw one simple stroke on each device, test undo/redo, then test
joining the room after several strokes already exist.

## Important

`com.unity.multiplayer.center` in Packages/manifest.json is not the networking
backend used by this implementation. The actual backend is the code under:

`Assets/Scripts/Multiplayer/Photon/`

and it is compiled only when `MP_PHOTON` (and Fusion's own generated symbols)
are active.
