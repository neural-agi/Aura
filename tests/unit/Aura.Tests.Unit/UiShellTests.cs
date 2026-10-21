using Aura.UI.Interop;
using Aura.UI.Monitoring;
using Aura.UI.Theming;
using Aura.UI.Windowing;
using Aura.UI.Surface;
using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aura.Tests.Unit;

public sealed class UiShellTests
{
    [Xunit.Fact]
    public async Task WindowManager_StartsAndRaisesStateEvents()
    {
        var window = new FakeAppWindow();
        var windowManager = CreateWindowManager(window, out var lifetime, out var interop);
        var observedStates = new List<AppWindowState>();

        windowManager.WindowStateChanged += (_, e) => observedStates.Add(e.State);

        await windowManager.StartAsync();

        window.ActivatedCount.Should().Be(1);
        lifetime.StopRequested.Should().BeFalse();
        interop.DpiAwarenessRequested.Should().BeTrue();
        observedStates.Should().Contain(AppWindowState.Activated);
    }

    [Xunit.Fact]
    public async Task WindowManager_ShutdownIsIdempotentAndClosesWindow()
    {
        var window = new FakeAppWindow();
        var windowManager = CreateWindowManager(window, out var lifetime, out _);
        var shutdownSignals = 0;

        windowManager.ShutdownRequested += (_, _) => shutdownSignals++;

        await windowManager.StartAsync();
        await windowManager.ShutdownAsync();
        await windowManager.ShutdownAsync();

        window.CloseCount.Should().Be(1);
        shutdownSignals.Should().Be(1);
        lifetime.StopRequested.Should().BeTrue();
    }

    [Xunit.Fact]
    public void ThemeService_AppliesThemeToAttachedWindow()
    {
        var themeService = new ThemeService(NullLogger<ThemeService>.Instance);
        var window = new FakeAppWindow();

        themeService.Attach(window);
        themeService.SetMode(AppThemeMode.Dark);

        window.LastAppliedTheme.Should().Be(AppThemeMode.Dark);
        themeService.RequestedMode.Should().Be(AppThemeMode.Dark);
    }

    [Xunit.Fact]
    public void MonitorService_RefreshesFromWindowHandle()
    {
        var interop = new FakeWin32Interop();
        var monitorService = new MonitorService(interop, NullLogger<MonitorService>.Instance);
        var window = new FakeAppWindow();
        var snapshots = new List<MonitorSnapshot?>();

        monitorService.CurrentMonitorChanged += (_, snapshot) => snapshots.Add(snapshot);
        monitorService.Attach(window);

        monitorService.CurrentMonitor.Should().NotBeNull();
        interop.RequestCount.Should().BeGreaterThan(0);
        snapshots.Should().NotBeEmpty();
    }

    [Xunit.Fact]
    public async Task WindowManager_RepeatStartDoesNotCreateSecondWindow()
    {
        var window = new FakeAppWindow();
        var windowManager = CreateWindowManager(window, out _, out _);

        await windowManager.StartAsync();
        await windowManager.StartAsync();

        window.ActivatedCount.Should().Be(1);
    }

    private static WindowManager CreateWindowManager(FakeAppWindow window, out FakeHostApplicationLifetime lifetime, out FakeWin32Interop interop)
    {
        lifetime = new FakeHostApplicationLifetime();
        interop = new FakeWin32Interop();
        var monitorService = new MonitorService(interop, NullLogger<MonitorService>.Instance);
        var themeService = new ThemeService(NullLogger<ThemeService>.Instance);
        var factory = new DelegateAppWindowFactory(() => window);

        return new WindowManager(factory, interop, monitorService, themeService, lifetime, NullLogger<WindowManager>.Instance, new FakeSurfaceManager(), new Aura.Runtime.StateMachine(), new FakeClipboardWindowListener(), new FakeNotificationAreaIcon());
    }

    private sealed class FakeAppWindow : IAppWindow
    {
        private event EventHandler? _activated;
        private event EventHandler? _closed;

        public int ActivatedCount { get; private set; }

        public int CloseCount { get; private set; }

        public AppThemeMode LastAppliedTheme { get; private set; } = AppThemeMode.System;

        public nint Handle => 0x1234;

        public event EventHandler<WindowStateChangedEventArgs>? StateChanged;

        event EventHandler IAppWindow.Activated
        {
            add => _activated += value;
            remove => _activated -= value;
        }

        event EventHandler IAppWindow.Closed
        {
            add => _closed += value;
            remove => _closed -= value;
        }

        public void PrepareForStartup() { }

        public void RevealAfterStartup()
        {
            ActivatedCount++;
            _activated?.Invoke(this, EventArgs.Empty);
            StateChanged?.Invoke(this, new WindowStateChangedEventArgs(AppWindowState.Activated, true, DateTimeOffset.UtcNow));
        }

        public void Close()
        {
            CloseCount++;
            _closed?.Invoke(this, EventArgs.Empty);
            StateChanged?.Invoke(this, new WindowStateChangedEventArgs(AppWindowState.Closed, false, DateTimeOffset.UtcNow));
        }

        public void ApplyTheme(AppThemeMode mode)
        {
            LastAppliedTheme = mode;
        }
    }

    private sealed class FakeWin32Interop : IWin32Interop
    {
        public bool DpiAwarenessRequested { get; private set; }

        public int RequestCount { get; private set; }

        public void EnsurePerMonitorDpiAwareness()
        {
            DpiAwarenessRequested = true;
        }

        public uint GetDpiForWindow(nint windowHandle)
        {
            RequestCount++;
            return 96;
        }

        public MonitorSnapshot GetMonitorSnapshot(nint windowHandle)
        {
            RequestCount++;
            return new MonitorSnapshot("DISPLAY1", 0, 0, 1920, 1080, 0, 0, 1920, 1040, 96, true);
        }

        public void SetWindowBounds(nint windowHandle, int left, int top, int width, int height) { }
        public void ConfigureFloatingWindow(nint windowHandle) { }
    }

    private sealed class FakeNotificationAreaIcon : INotificationAreaIcon
    {
        public event EventHandler? ExitRequested { add { } remove { } }
        public void Attach(nint windowHandle) { }
        public void Dispose() { }
    }

    private sealed class FakeClipboardWindowListener : IClipboardWindowListener
    {
        public int RegistrationCount { get; private set; }
        public void Start() { }
        public void Attach(nint windowHandle) => RegistrationCount = 1;
        public void Stop() => RegistrationCount = 0;
    }

    private sealed class FakeSurfaceManager : ISurfaceManager
    {
        public SurfacePresentationState Current { get; } = new(SurfacePresentationMode.Hidden, false, "", "");
        public void Attach(nint windowHandle) { }
    }

    private sealed class FakeHostApplicationLifetime : IHostApplicationLifetime
    {
        private readonly CancellationTokenSource _started = new();
        private readonly CancellationTokenSource _stopping = new();
        private readonly CancellationTokenSource _stopped = new();

        public bool StopRequested { get; private set; }

        public CancellationToken ApplicationStarted => _started.Token;

        public CancellationToken ApplicationStopping => _stopping.Token;

        public CancellationToken ApplicationStopped => _stopped.Token;

        public void StopApplication()
        {
            StopRequested = true;
            _stopping.Cancel();
            _stopped.Cancel();
        }
    }
}
