using UnityEngine;

namespace TiltBrush.Multiplayer {

/// <summary>
/// Simple remote avatar rig: head + two hands. The rig is intentionally independent
/// from a particular XR SDK and receives world-space poses from MultiplayerManager.
/// </summary>
public class MultiplayerAvatar : MonoBehaviour {
  [SerializeField] private Transform m_Head;
  [SerializeField] private Transform m_LeftHand;
  [SerializeField] private Transform m_RightHand;
  [SerializeField, Range(1f, 30f)] private float m_Smoothing = 15f;

  private MultiplayerPose m_TargetPose;
  private bool m_HasPose;

  public string PlayerId { get; private set; }

  public void Initialize(string playerId, MultiplayerPose initialPose) {
    PlayerId = playerId;
    m_TargetPose = initialPose;
    m_HasPose = true;
    ApplyPose(initialPose, true);
  }

  public void SetTargetPose(MultiplayerPose pose) {
    m_TargetPose = pose;
    m_HasPose = true;
  }

  private void Update() {
    if (!m_HasPose) {
      return;
    }
    ApplyPose(m_TargetPose, false);
  }

  private void ApplyPose(MultiplayerPose pose, bool immediate) {
    var t = immediate ? 1f : 1f - Mathf.Exp(-m_Smoothing * Time.unscaledDeltaTime);
    LerpTransform(m_Head, pose.headPosition, pose.headRotation, t);
    LerpTransform(m_LeftHand, pose.leftHandPosition, pose.leftHandRotation, t);
    LerpTransform(m_RightHand, pose.rightHandPosition, pose.rightHandRotation, t);
  }

  private static void LerpTransform(Transform target, Vector3 position,
      Quaternion rotation, float t) {
    if (target == null) {
      return;
    }

    target.position = Vector3.Lerp(target.position, position, t);
    target.rotation = Quaternion.Slerp(target.rotation, rotation, t);
  }
}

} // namespace TiltBrush.Multiplayer
