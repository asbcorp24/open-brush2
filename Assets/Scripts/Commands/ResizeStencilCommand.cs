namespace TiltBrush {

public class ResizeStencilCommand : BaseCommand {
  private readonly StencilWidget m_Widget;
  private readonly UnityEngine.Vector3 m_StartExtents;
  private readonly UnityEngine.Vector3 m_EndExtents;

  public StencilWidget Widget => m_Widget;

  public ResizeStencilCommand(StencilWidget widget, UnityEngine.Vector3 endExtents,
      BaseCommand parent = null) : base(parent) {
    m_Widget = widget;
    m_StartExtents = widget.Extents;
    m_EndExtents = endExtents;
  }

  public override bool NeedsSave => true;

  protected override void OnRedo() {
    if (m_Widget != null) {
      m_Widget.Extents = m_EndExtents;
    }
  }

  protected override void OnUndo() {
    if (m_Widget != null) {
      m_Widget.Extents = m_StartExtents;
    }
  }
}

} // namespace TiltBrush
