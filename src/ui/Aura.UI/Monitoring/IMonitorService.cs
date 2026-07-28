using Aura.UI.Interop;
using Aura.UI.Windowing;

namespace Aura.UI.Monitoring;

public interface IMonitorService
{
    MonitorSnapshot? CurrentMonitor { get; }

    event EventHandler<MonitorSnapshot?>? CurrentMonitorChanged;

    void Attach(IAppWindow window);

    void Detach();

    void Refresh();
}

