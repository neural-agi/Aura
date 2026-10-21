using System.Runtime.InteropServices;
using Aura.Runtime;
using Aura.Runtime.Artifacts;

namespace Aura.UI.Interop;

public interface IClipboardWindowListener
{
    int RegistrationCount { get; }
    void Start();
    void Attach(nint windowHandle);
    void Stop();
}

public interface IClipboardNativeApi : IClipboardTextWriter
{
    bool AddListener(nint windowHandle);
    bool RemoveListener(nint windowHandle);
    bool TryReadText(nint windowHandle, out string? text, out long sequenceNumber);
    long GetClipboardSequenceNumber();
    nint ForwardMessage(nint windowHandle, uint message, nint wParam, nint lParam);
    bool InstallSubclass(nint windowHandle, ClipboardSubclassProcedure procedure, nuint subclassId);
    bool RemoveSubclass(nint windowHandle, ClipboardSubclassProcedure procedure, nuint subclassId);
}

[UnmanagedFunctionPointer(CallingConvention.Winapi)]
public delegate nint ClipboardSubclassProcedure(nint windowHandle, uint message, nint wParam, nint lParam, nuint subclassId, nuint referenceData);

public sealed class ClipboardWindowListener : IClipboardWindowListener, IDisposable
{
    private const uint ClipboardUpdateMessage = 0x031D;
    private const nuint SubclassId = 0x41555241;
    private readonly object _gate = new();
    private readonly IClipboardNativeApi _native;
    private readonly IEventDispatcher _events;
    private readonly IClipboardArtifactManager _artifacts;
    private readonly ClipboardSubclassProcedure _callback;
    private readonly SemaphoreSlim _captureSignal = new(0, 1);
    private CancellationTokenSource? _captureCancellation;
    private Task? _captureWorker;
    private long _latestRequestedSequence;
    private long _lastProcessedSequence;
    private Task? _stoppedWorker;
    private nint _windowHandle;
    private bool _started;
    private bool _registered;
    private int _registrationCount;

    public ClipboardWindowListener(IClipboardNativeApi native, IEventDispatcher events, IClipboardArtifactManager artifacts)
    {
        _native = native;
        _events = events;
        _artifacts = artifacts;
        _callback = OnWindowMessage;
    }

    public int RegistrationCount => Volatile.Read(ref _registrationCount);

    public void Start()
    {
        Task? stoppedWorker;
        lock (_gate) stoppedWorker = _stoppedWorker;
        stoppedWorker?.GetAwaiter().GetResult();
        lock (_gate)
        {
            if (_started) return;
            _stoppedWorker = null;
            _started = true;
            _captureCancellation = new CancellationTokenSource();
            _captureWorker = CaptureLoopAsync(_captureCancellation.Token);
            if (_windowHandle != 0) RegisterLocked();
        }
    }

    public void Attach(nint windowHandle)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(windowHandle);
        lock (_gate)
        {
            if (_windowHandle == windowHandle) return;
            UnregisterLocked();
            _windowHandle = windowHandle;
            if (_started) RegisterLocked();
        }
    }

    public void Stop()
    {
        CancellationTokenSource? cancellation;
        lock (_gate)
        {
            if (!_started && !_registered) return;
            _started = false;
            UnregisterLocked();
            cancellation = _captureCancellation;
            _captureCancellation = null;
            _stoppedWorker = _captureWorker;
            _captureWorker = null;
            cancellation?.Cancel();
        }
        cancellation?.Dispose();
        Task? stoppedWorker;
        lock (_gate) stoppedWorker = _stoppedWorker;
        stoppedWorker?.GetAwaiter().GetResult();
        lock (_gate) _stoppedWorker = null;
    }

    private void RegisterLocked()
    {
        if (_registered || _windowHandle == 0) return;
        if (!_native.InstallSubclass(_windowHandle, _callback, SubclassId) || !_native.AddListener(_windowHandle))
        {
            _ = _native.RemoveSubclass(_windowHandle, _callback, SubclassId);
            _artifacts.SetAvailability(false, "Clipboard capture is unavailable.");
            return;
        }
        _registered = true;
        Interlocked.Increment(ref _registrationCount);
        _artifacts.SetAvailability(true);
    }

    private void SignalCaptureWorker()
    {
        if (_captureSignal.CurrentCount == 0)
        {
            try { _captureSignal.Release(); } catch (SemaphoreFullException) { }
        }
    }

    private void UnregisterLocked()
    {
        if (!_registered) return;
        _ = _native.RemoveListener(_windowHandle);
        _ = _native.RemoveSubclass(_windowHandle, _callback, SubclassId);
        _registered = false;
        Interlocked.Decrement(ref _registrationCount);
    }

    private nint OnWindowMessage(nint hwnd, uint message, nint wParam, nint lParam, nuint subclassId, nuint referenceData)
    {
        if (message == ClipboardUpdateMessage)
        {
            try
            {
                var sequence = _native.GetClipboardSequenceNumber();
                var processed = Interlocked.Read(ref _lastProcessedSequence);
                var pending = Interlocked.Read(ref _latestRequestedSequence);
                if (sequence != 0 && sequence != processed && sequence != pending)
                {
                    Interlocked.Exchange(ref _latestRequestedSequence, sequence);
                    SignalCaptureWorker();
                }
            }
            catch (Exception)
            {
                try { _artifacts.SetAvailability(false, "Clipboard is temporarily unavailable."); } catch (Exception) { }
            }
        }
        try { return _native.ForwardMessage(hwnd, message, wParam, lParam); }
        catch (Exception) { return 0; }
    }

    private async Task CaptureLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (true)
            {
                await _captureSignal.WaitAsync(cancellationToken).ConfigureAwait(false);
                while (Interlocked.Exchange(ref _latestRequestedSequence, 0) is var requestedSequence && requestedSequence != 0)
                {
                    if (requestedSequence == Interlocked.Read(ref _lastProcessedSequence)) continue;
                    await ReadAndPublishAsync(requestedSequence, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception)
        {
            try { _artifacts.SetAvailability(false, "Clipboard capture is unavailable."); } catch (Exception) { }
        }
    }

    private async Task ReadAndPublishAsync(long requestedSequence, CancellationToken cancellationToken)
    {
        nint hwnd;
        lock (_gate)
        {
            if (!_started || !_registered) return;
            hwnd = _windowHandle;
        }

        try
        {
            for (var attempt = 0; attempt < 3; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (_native.TryReadText(hwnd, out var text, out var currentSequence))
                {
                    if (currentSequence != 0 && currentSequence != requestedSequence)
                    {
                        Interlocked.Exchange(ref _latestRequestedSequence, currentSequence);
                        SignalCaptureWorker();
                        return;
                    }
                    if (currentSequence != 0) Interlocked.Exchange(ref _lastProcessedSequence, currentSequence);
                    if (text is not null)
                    {
                        await _events.PublishAsync(new ClipboardCaptureEvent(DateTimeOffset.UtcNow, text, null, currentSequence)).ConfigureAwait(false);
                    }
                    return;
                }
                if (attempt < 2) await Task.Delay(50, cancellationToken).ConfigureAwait(false);
            }
            Interlocked.Exchange(ref _lastProcessedSequence, requestedSequence);
            try { _artifacts.SetAvailability(false, "Clipboard is temporarily unavailable."); } catch (Exception) { }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception)
        {
            Interlocked.Exchange(ref _lastProcessedSequence, requestedSequence);
            try { _artifacts.SetAvailability(false, "Clipboard capture is temporarily unavailable."); } catch (Exception) { }
        }
    }

    public void Dispose()
    {
        Stop();
        _captureSignal.Dispose();
    }
}

public sealed class Win32ClipboardNativeApi : IClipboardNativeApi
{
    private const uint CfUnicodeText = 13;
    private const uint GmemMoveable = 0x0002;
    private const uint GmemZeroInit = 0x0040;

    public bool AddListener(nint windowHandle) => AddClipboardFormatListener(windowHandle);
    public bool RemoveListener(nint windowHandle) => RemoveClipboardFormatListener(windowHandle);
    public bool InstallSubclass(nint windowHandle, ClipboardSubclassProcedure procedure, nuint subclassId) => SetWindowSubclass(windowHandle, procedure, subclassId, 0);
    public bool RemoveSubclass(nint windowHandle, ClipboardSubclassProcedure procedure, nuint subclassId) => RemoveWindowSubclass(windowHandle, procedure, subclassId);
    public nint ForwardMessage(nint windowHandle, uint message, nint wParam, nint lParam) => DefSubclassProc(windowHandle, message, wParam, lParam);

    public long GetClipboardSequenceNumber() => GetClipboardSequenceNumberInternal();

    public bool TryReadText(nint windowHandle, out string? text, out long sequenceNumber)
    {
        text = null;
        sequenceNumber = GetClipboardSequenceNumberInternal();
        if (!OpenClipboard(windowHandle)) return false;
        try
        {
            if (!IsClipboardFormatAvailable(CfUnicodeText)) return true;
            var data = GetClipboardData(CfUnicodeText);
            if (data == 0) return false;
            var pointer = GlobalLock(data);
            if (pointer == 0) return false;
            try { text = Marshal.PtrToStringUni(pointer); }
            finally { _ = GlobalUnlock(data); }
            return true;
        }
        finally { _ = CloseClipboard(); }
    }

    public bool TrySetText(string text, out long sequenceNumber)
    {
        sequenceNumber = 0;
        var bytes = System.Text.Encoding.Unicode.GetBytes(text + '\0');
        if (!OpenClipboard(0)) return false;
        nint memory = 0;
        try
        {
            if (!EmptyClipboard()) return false;
            memory = GlobalAlloc(GmemMoveable | GmemZeroInit, (nuint)bytes.Length);
            if (memory == 0) return false;
            var pointer = GlobalLock(memory);
            if (pointer == 0) return false;
            try { Marshal.Copy(bytes, 0, pointer, bytes.Length); }
            finally { _ = GlobalUnlock(memory); }
            if (SetClipboardData(CfUnicodeText, memory) == 0) return false;
            memory = 0;
            sequenceNumber = GetClipboardSequenceNumberInternal();
            return true;
        }
        finally
        {
            _ = CloseClipboard();
            if (memory != 0) _ = GlobalFree(memory);
        }
    }

    [DllImport("user32.dll", SetLastError = true)] private static extern bool AddClipboardFormatListener(nint hwnd);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool RemoveClipboardFormatListener(nint hwnd);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool OpenClipboard(nint hwnd);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool CloseClipboard();
    [DllImport("user32.dll", SetLastError = true)] private static extern bool IsClipboardFormatAvailable(uint format);
    [DllImport("user32.dll", SetLastError = true)] private static extern nint GetClipboardData(uint format);
    [DllImport("user32.dll", SetLastError = true)] private static extern nint SetClipboardData(uint format, nint memory);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool EmptyClipboard();
    [DllImport("user32.dll", EntryPoint = "GetClipboardSequenceNumber")] private static extern uint GetClipboardSequenceNumberInternal();
    [DllImport("kernel32.dll", SetLastError = true)] private static extern nint GlobalLock(nint memory);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool GlobalUnlock(nint memory);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern nint GlobalAlloc(uint flags, nuint bytes);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern nint GlobalFree(nint memory);
    [DllImport("comctl32.dll", SetLastError = true)] private static extern bool SetWindowSubclass(nint hwnd, ClipboardSubclassProcedure procedure, nuint subclassId, nuint referenceData);
    [DllImport("comctl32.dll", SetLastError = true)] private static extern bool RemoveWindowSubclass(nint hwnd, ClipboardSubclassProcedure procedure, nuint subclassId);
    [DllImport("comctl32.dll")] private static extern nint DefSubclassProc(nint hwnd, uint message, nint wParam, nint lParam);
}
