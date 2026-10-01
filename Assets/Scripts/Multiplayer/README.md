# Open Brush Multiplayer Foundation

This directory contains the transport-independent first stage of multiplayer support.

## Included

- `IMultiplayerTransport` — backend abstraction.
- `LocalLoopbackTransport` — zero-dependency development transport for testing multiple clients in one Unity process.
- `MultiplayerManager` — room lifecycle, player pose replication and stroke events.
- `MultiplayerProtocol` — compact serializable message model.
- `MultiplayerAvatar` — smoothed head/hand representation for remote users.

## Unity setup for the local smoke test

1. Create an empty GameObject named `Multiplayer`.
2. Add `LocalLoopbackTransport`.
3. Add `MultiplayerManager`.
4. Drag the `LocalLoopbackTransport` component into the manager's **Transport Component** field.
5. Duplicate the object to simulate a second client in a dedicated test scene, or use two independent app instances once a real transport is added.
6. Call `Host("SCI-101")` on one client and `Join("SCI-101")` on another.

## Open Brush integration points

The drawing code should call:

- `BeginStroke(brushGuid, color, size)` when a local stroke begins.
- `AddStrokePoint(...)` as control points are accepted by the brush.
- `EndStroke(strokeId)` when drawing finishes.
- `DeleteStroke(strokeId)` for multiplayer-aware undo/delete.

Remote rendering should subscribe to:

- `StrokeBeginReceived`
- `StrokePointReceived`
- `StrokeEndReceived`
- `StrokeDeleteReceived`

Do **not** send generated meshes over the network. Each client should reconstruct the brush geometry locally from the same brush id and control points.

## Next transport

The intended production backend is Photon Fusion. Add a component implementing
`IMultiplayerTransport`, then assign it to `MultiplayerManager`. No Open Brush
drawing integration code needs to know whether packets are carried by Photon, LAN
UDP, Unity Transport, or another backend.

## Still required before production

- Hook local stroke creation into the existing Open Brush brush pipeline.
- Rebuild remote strokes through the native Open Brush stroke/brush APIs.
- Add room snapshot/history for late joiners.
- Add remote avatar prefab lifecycle.
- Add host/teacher permissions and room UI.
- Add the production network transport.
