namespace Aura.UI.Windowing;

public sealed class DelegateAppWindowFactory : IAppWindowFactory
{
    private readonly Func<IAppWindow> _factory;

    public DelegateAppWindowFactory(Func<IAppWindow> factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    public IAppWindow Create() => _factory();
}

