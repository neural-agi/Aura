using System.Runtime.InteropServices;
using Aura.Runtime.Media;

namespace Aura.Platform.Windows.Media;

public sealed class WindowsSystemVolumeAdapter : ISystemVolumeAdapter
{
    private readonly object _gate = new();
    private readonly SemaphoreSlim _ioGate = new(1, 1);
    private int _disposeStarted;
    private MMDeviceEnumerator? _enumerator;
    private MMDevice? _device;
    private AudioEndpointVolume? _endpoint;
    private bool _disposed;

    public event EventHandler<double>? VolumeChanged;

    public async ValueTask<double?> GetVolumeAsync(CancellationToken cancellationToken = default)
    {
        if (Volatile.Read(ref _disposeStarted) != 0) return null;
        try { await _ioGate.WaitAsync(cancellationToken).ConfigureAwait(false); }
        catch (ObjectDisposedException) when (Volatile.Read(ref _disposeStarted) != 0) { return null; }
        try { return await Task.Run(ReadVolume, cancellationToken).ConfigureAwait(false); }
        catch (OperationCanceledException) { throw; }
        catch { return null; }
        finally { _ioGate.Release(); }
    }

    public async ValueTask<bool> SetVolumeAsync(double level, CancellationToken cancellationToken = default)
    {
        if (!double.IsFinite(level) || Volatile.Read(ref _disposeStarted) != 0) return false;
        try { await _ioGate.WaitAsync(cancellationToken).ConfigureAwait(false); }
        catch (ObjectDisposedException) when (Volatile.Read(ref _disposeStarted) != 0) { return false; }
        try { return await Task.Run(() => WriteVolume((float)Math.Clamp(level, 0d, 1d)), cancellationToken).ConfigureAwait(false); }
        catch (OperationCanceledException) { throw; }
        catch { return false; }
        finally { _ioGate.Release(); }
    }

    private double? ReadVolume()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            try { return Endpoint.MasterVolumeLevelScalar; }
            catch { return null; }
        }
    }

    private bool WriteVolume(float level)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            try { Endpoint.MasterVolumeLevelScalar = level; return true; }
            catch { return false; }
        }
    }

    private AudioEndpointVolume Endpoint
    {
        get
        {
            lock (_gate)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                if (_endpoint is not null) return _endpoint;
                _enumerator ??= new MMDeviceEnumerator();
                _device ??= _enumerator.GetDefaultAudioEndpoint(EDataFlow.Render, ERole.Multimedia);
                try
                {
                    _endpoint = _device.AudioEndpointVolume;
                    _endpoint.OnVolumeNotification += OnVolumeNotification;
                    return _endpoint;
                }
                catch
                {
                    _endpoint?.Dispose();
                    _endpoint = null;
                    throw;
                }
            }
        }
    }

    private void OnVolumeNotification(object? sender, AudioVolumeNotificationData notification)
    {
        var bounded = Math.Clamp((double)notification.MasterVolume, 0d, 1d);
        ThreadPool.QueueUserWorkItem(_ => VolumeChanged?.Invoke(this, bounded));
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposeStarted, 1) != 0) return;
        await _ioGate.WaitAsync().ConfigureAwait(false);
        try
        {
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true;
                if (_endpoint is not null)
                {
                    _endpoint.OnVolumeNotification -= OnVolumeNotification;
                    try { _endpoint.Dispose(); } catch { }
                    _endpoint = null;
                }
                _device?.Dispose();
                _device = null;
                _enumerator?.Dispose();
                _enumerator = null;
            }
        }
        finally
        {
            _ioGate.Release();

        }
    }

    private enum EDataFlow { Render, Capture, All }
    private enum ERole { Console, Multimedia, Communications }

    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private class MMDeviceEnumeratorComObject;

    private sealed class MMDeviceEnumerator : IDisposable
    {
        private readonly IMMDeviceEnumerator _native = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
        public MMDevice GetDefaultAudioEndpoint(EDataFlow flow, ERole role)
        {
            Marshal.ThrowExceptionForHR(_native.GetDefaultAudioEndpoint(flow, role, out var device));
            return new MMDevice(device);
        }
        public void Dispose() => Marshal.ReleaseComObject(_native);
    }

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(EDataFlow dataFlow, uint stateMask, out nint devices);
        [PreserveSig] int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice device);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid iid, uint clsCtx, nint activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object result);
    }

    private sealed class MMDevice(IMMDevice native) : IDisposable
    {
        private static readonly Guid AudioEndpointVolumeId = new("5CDF2C82-841E-4546-9722-0CF74078229A");
        public AudioEndpointVolume AudioEndpointVolume
        {
            get
            {
                var iid = AudioEndpointVolumeId;
                Marshal.ThrowExceptionForHR(native.Activate(ref iid, 23, 0, out var result));
                return new AudioEndpointVolume((IAudioEndpointVolume)result);
            }
        }
        public void Dispose() => Marshal.ReleaseComObject(native);
    }

    [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioEndpointVolume
    {
        [PreserveSig] int RegisterControlChangeNotify(IAudioEndpointVolumeCallback callback);
        [PreserveSig] int UnregisterControlChangeNotify(IAudioEndpointVolumeCallback callback);
        [PreserveSig] int GetChannelCount(out uint channelCount);
        [PreserveSig] int SetMasterVolumeLevel(float levelDb, ref Guid eventContext);
        [PreserveSig] int SetMasterVolumeLevelScalar(float level, ref Guid eventContext);
        [PreserveSig] int GetMasterVolumeLevel(out float levelDb);
        [PreserveSig] int GetMasterVolumeLevelScalar(out float level);
        [PreserveSig] int SetChannelVolumeLevel(uint channel, float levelDb, ref Guid eventContext);
        [PreserveSig] int SetChannelVolumeLevelScalar(uint channel, float level, ref Guid eventContext);
        [PreserveSig] int GetChannelVolumeLevel(uint channel, out float levelDb);
        [PreserveSig] int GetChannelVolumeLevelScalar(uint channel, out float level);
        [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool muted, ref Guid eventContext);
        [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool muted);
        [PreserveSig] int GetVolumeStepInfo(out uint step, out uint stepCount);
        [PreserveSig] int VolumeStepUp(ref Guid eventContext);
        [PreserveSig] int VolumeStepDown(ref Guid eventContext);
        [PreserveSig] int QueryHardwareSupport(out uint hardwareSupportMask);
        [PreserveSig] int GetVolumeRange(out float minDb, out float maxDb, out float incrementDb);
    }

    [ComVisible(true), Guid("657804FA-D6AD-4496-8A60-352752AF4F89"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioEndpointVolumeCallback
    {
        [PreserveSig] int OnNotify(IntPtr notificationData);
    }

    private sealed class AudioVolumeNotificationData
    {
        public float MasterVolume { get; init; }
    }

    private sealed class AudioEndpointVolume : IDisposable
    {
        private readonly IAudioEndpointVolume _native;
        private readonly VolumeCallback _callback;
        private EventHandler<AudioVolumeNotificationData>? _changed;
        private static readonly Guid EventContext = Guid.Empty;

        public AudioEndpointVolume(IAudioEndpointVolume native)
        {
            _native = native;
            _callback = new VolumeCallback(data => _changed?.Invoke(this, data));
            Marshal.ThrowExceptionForHR(_native.RegisterControlChangeNotify(_callback));
        }

        public float MasterVolumeLevelScalar
        {
            get { Marshal.ThrowExceptionForHR(_native.GetMasterVolumeLevelScalar(out var value)); return value; }
            set { var context = EventContext; Marshal.ThrowExceptionForHR(_native.SetMasterVolumeLevelScalar(value, ref context)); }
        }

        public event EventHandler<AudioVolumeNotificationData> OnVolumeNotification
        {
            add => _changed += value;
            remove => _changed -= value;
        }

        public void Dispose()
        {
            _ = _native.UnregisterControlChangeNotify(_callback);
            Marshal.ReleaseComObject(_native);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeNotification
        {
            public Guid EventContext;
            public float MasterVolume;
            [MarshalAs(UnmanagedType.Bool)] public bool Muted;
            public uint ChannelCount;
        }

        private sealed class VolumeCallback(Action<AudioVolumeNotificationData> notify) : IAudioEndpointVolumeCallback
        {
            public int OnNotify(IntPtr notificationData)
            {
                var native = Marshal.PtrToStructure<NativeNotification>(notificationData);
                notify(new AudioVolumeNotificationData { MasterVolume = native.MasterVolume });
                return 0;
            }
        }
    }
}
