using Aura.UI.Windowing;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;

namespace Aura.App.Bootstrap;

public static class SingleInstanceActivation
{
    private const string InstanceKey = "Aura";
    private static AppInstance? _instance;
    private static DispatcherQueue? _dispatcherQueue;

    public static bool RegisterOrActivateCurrentInstance(AppActivationArguments activationArguments)
    {
        var current = AppInstance.FindOrRegisterForKey(InstanceKey);
        if (!current.IsCurrent)
        {
            current.RedirectActivationToAsync(activationArguments).AsTask().GetAwaiter().GetResult();
            return false;
        }

        _instance = current;
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        _instance.Activated += OnActivated;
        return true;
    }

    private static void OnActivated(object? sender, AppActivationArguments args)
    {
        if (Application.Current is not App app)
        {
            return;
        }

        _dispatcherQueue?.TryEnqueue(() =>
        {
            if (BootstrapRuntime.GetHost().Services.GetService(typeof(IWindowManager)) is IWindowManager windowManager
                && windowManager.ActiveWindow is MainWindow window)
            {
                window.ActivateFromRedirectedLaunch();
            }
        });
    }
}
