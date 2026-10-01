using UnityEngine;

namespace OpenBrush.Multiplayer {

public class LanPlayerRig : MonoBehaviour, ITransientData<PlayerRigData> {
  public int PlayerId { get; set; }
  public bool IsSpawned => gameObject != null && gameObject.activeInHierarchy;

  public Transform Head;
  public Transform LeftHand;
  public Transform RightHand;
  public Transform Tool;

  private PlayerRigData m_Data;

  public void TransmitData(PlayerRigData data) {
    m_Data = data;
  }

  public PlayerRigData ReceiveData() {
    Apply(m_Data);
    return m_Data;
  }

  public void Apply(PlayerRigData data) {
    m_Data = data;
    if (Head != null) {
      Head.localPosition = data.HeadPosition;
      Head.localRotation = data.HeadRotation;
    }
    if (LeftHand != null) {
      LeftHand.localPosition = data.LeftHandPosition;
      LeftHand.localRotation = data.LeftHandRotation;
    }
    if (RightHand != null) {
      RightHand.localPosition = data.RightHandPosition;
      RightHand.localRotation = data.RightHandRotation;
    }
    if (Tool != null) {
      Tool.localPosition = data.ToolPosition;
      Tool.localRotation = data.ToolRotation;
    }
  }

  public static LanPlayerRig Create(int playerId, string nickname) {
    var root = new GameObject("LAN Player " + playerId + " " + nickname);
    var rig = root.AddComponent<LanPlayerRig>();
    rig.PlayerId = playerId;

    rig.Head = CreatePart(root.transform, "HeadTransform", PrimitiveType.Sphere, 0.18f);
    rig.LeftHand = CreatePart(root.transform, "LeftHandTransform", PrimitiveType.Sphere, 0.08f);
    rig.RightHand = CreatePart(root.transform, "RightHandTransform", PrimitiveType.Sphere, 0.08f);
    rig.Tool = CreatePart(root.transform, "ToolTransform", PrimitiveType.Cube, 0.07f);
    return rig;
  }

  private static Transform CreatePart(Transform parent, string name, PrimitiveType type, float scale) {
    var go = GameObject.CreatePrimitive(type);
    go.name = name;
    go.transform.SetParent(parent, false);
    go.transform.localScale = Vector3.one * scale;
    var collider = go.GetComponent<Collider>();
    if (collider != null) Object.Destroy(collider);
    return go.transform;
  }
}

} // namespace OpenBrush.Multiplayer
