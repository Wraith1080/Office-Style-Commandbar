using System.Windows.Forms.Design;

namespace CommandBars.Design;

/// <summary>
/// Legacy DockHost design behavior. Definition changes are coordinated by the
/// manager designer so every host does not independently rebuild for every
/// global surface notification.
/// </summary>
public sealed class DockHostDesigner : ControlDesigner
{
}
