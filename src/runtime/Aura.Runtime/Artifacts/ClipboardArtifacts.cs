using System.Text.Json;
using Microsoft.Extensions.Hosting;
using System.Text.RegularExpressions;
using Aura.Runtime.Settings;

namespace Aura.Runtime.Artifacts;

public sealed record ClipboardArtifact(string Id, string Text, DateTimeOffset CapturedAtUtc, bool IsPinned, bool IsSensitive, string? SourceApp);
public sealed record ClipboardCaptureEvent(DateTimeOffset OccurredAtUtc, string Text, string? SourceApp, long ClipboardSequenceNumber) : RuntimeEvent(OccurredAtUtc);
public sealed record ClipboardHistorySnapshot(IReadOnlyList<ClipboardArtifact> Items, bool IsAvailable, string? Status);

public interface IClipboardArtifactManager
{
    ClipboardHistorySnapshot Current { get; }
    event EventHandler<ClipboardHistorySnapshot>? Changed;
    ValueTask StartAsync(CancellationToken cancellationToken = default);
    ValueTask StopAsync(CancellationToken cancellationToken = default);
    ValueTask<bool> CaptureAsync(ClipboardCaptureEvent capture, CancellationToken cancellationToken = default);
    IReadOnlyList<ClipboardArtifact> Query(string? filter = null);
    ValueTask<bool> RestoreAsync(string id, CancellationToken cancellationToken = default);
    ValueTask<bool> SetPinnedAsync(string id, bool isPinned, CancellationToken cancellationToken = default);
    ValueTask<bool> DeleteAsync(string id, CancellationToken cancellationToken = default);
    void SetAvailability(bool isAvailable, string? status = null);
}

public interface IClipboardArtifactPersistence
{
    ValueTask<IReadOnlyList<ClipboardArtifact>> LoadAsync(CancellationToken cancellationToken = default);
    ValueTask SaveAsync(IReadOnlyList<ClipboardArtifact> items, CancellationToken cancellationToken = default);
}

public interface IClipboardTextWriter
{
    bool TrySetText(string text, out long sequenceNumber);
}

public sealed class JsonClipboardArtifactPersistence : IClipboardArtifactPersistence
{
    private readonly string _path;
    private readonly SemaphoreSlim _gate = new(1, 1);
    public JsonClipboardArtifactPersistence() : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Aura", "clipboard-history.json")) { }
    public JsonClipboardArtifactPersistence(string path) => _path = path;

    public async ValueTask<IReadOnlyList<ClipboardArtifact>> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(_path)) return Array.Empty<ClipboardArtifact>();
            await using var stream = File.OpenRead(_path);
            return await JsonSerializer.DeserializeAsync<List<ClipboardArtifact>>(stream, cancellationToken: cancellationToken).ConfigureAwait(false) ?? [];
        }
        catch (JsonException) { return Array.Empty<ClipboardArtifact>(); }
        catch (IOException) { return Array.Empty<ClipboardArtifact>(); }
        catch (UnauthorizedAccessException) { return Array.Empty<ClipboardArtifact>(); }
        finally { _gate.Release(); }
    }

    public async ValueTask SaveAsync(IReadOnlyList<ClipboardArtifact> items, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(items);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        var temporaryPath = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            var directory = Path.GetDirectoryName(_path);
            if (string.IsNullOrWhiteSpace(directory)) throw new IOException("Clipboard storage path is invalid.");
            Directory.CreateDirectory(directory);
            await using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, items, cancellationToken: cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporaryPath, _path, overwrite: true);
        }
        finally
        {
            try { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); } catch (IOException) { }
            _gate.Release();
        }
    }
}

/// <summary>Best-effort heuristic; it cannot identify every sensitive value.</summary>
public sealed class ClipboardSensitivityClassifier
{
    private static readonly Regex Password = new(@"(?im)\b(password|passwd|pwd)\s*[:=]\s*\S+", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex PrivateKey = new(@"-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex Token = new(@"(?i)\b(?:Bearer\s+[A-Za-z0-9._~+/-]+=*|(?:api[_-]?key|access[_-]?token|client[_-]?secret)\s*[:=]\s*[A-Za-z0-9._~+/-]{16,})\b", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    public bool IsSensitive(string text) => Password.IsMatch(text) || PrivateKey.IsMatch(text) || Token.IsMatch(text);
}

public sealed class ClipboardArtifactManager : IClipboardArtifactManager, IHostedService, IDisposable
{
    public const int MaximumHistoryItems = 500;
    private readonly IClipboardArtifactPersistence _persistence;
    private readonly IClipboardTextWriter _clipboard;
    private readonly ISettingsStore _settings;
    private readonly IEventDispatcher _events;
    private readonly ClipboardSensitivityClassifier _classifier;
    private readonly TimeProvider _clock;
    private readonly object _gate = new();
    private readonly IDisposable _subscription;
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private List<ClipboardArtifact> _items = [];
    private readonly HashSet<long> _restoreSequences = [];
    private readonly HashSet<RestoreSequenceReservation> _pendingRestores = [];
    private bool _started;
    private bool _available = true;
    private string? _status;

    public ClipboardArtifactManager(IClipboardArtifactPersistence persistence, IClipboardTextWriter clipboard, ISettingsStore settings,
        IEventDispatcher events, ClipboardSensitivityClassifier classifier, TimeProvider clock)
    {
        _persistence = persistence;
        _clipboard = clipboard;
        _settings = settings;
        _events = events;
        _classifier = classifier;
        _clock = clock;
        _subscription = events.Subscribe(HandleEventAsync);
        _settings.Changed += OnSettingsChanged;
    }

    public event EventHandler<ClipboardHistorySnapshot>? Changed;
    public ClipboardHistorySnapshot Current { get { lock (_gate) return new(_items.ToArray(), _available, _status); } }

    public async ValueTask StartAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate) { if (_started) return; _started = true; }
        try
        {
            var loaded = await _persistence.LoadAsync(cancellationToken).ConfigureAwait(false);
            lock (_gate) _items = loaded.Where(item => !item.IsSensitive || _settings.Current.AllowSensitiveData).ToList();
            await EnforceRetentionAsync(cancellationToken).ConfigureAwait(false);
            SetAvailability(true);
        }
        catch (IOException) { SetAvailability(false, "Clipboard history is unavailable."); }
        catch (UnauthorizedAccessException) { SetAvailability(false, "Clipboard history is unavailable."); }
        PublishChanged();
    }

    public ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate) _started = false;
        return ValueTask.CompletedTask;
    }

    Task IHostedService.StartAsync(CancellationToken cancellationToken) => StartAsync(cancellationToken).AsTask();
    Task IHostedService.StopAsync(CancellationToken cancellationToken) => StopAsync(cancellationToken).AsTask();

    public IReadOnlyList<ClipboardArtifact> Query(string? filter = null)
    {
        lock (_gate)
        {
            var now = _clock.GetUtcNow();
            return _items.Where(item => (item.IsPinned || !IsExpired(item, now))
                    && (!item.IsSensitive || _settings.Current.AllowSensitiveData))
                .Where(item => string.IsNullOrWhiteSpace(filter) || item.Text.Contains(filter, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(item => item.CapturedAtUtc).ToArray();
        }
    }

    public async ValueTask<bool> RestoreAsync(string id, CancellationToken cancellationToken = default)
    {
        ClipboardArtifact? item;
        lock (_gate) item = _items.FirstOrDefault(candidate => candidate.Id == id);
        if (item is null || (item.IsSensitive && !_settings.Current.AllowSensitiveData)) return false;
        cancellationToken.ThrowIfCancellationRequested();
        var reservation = new RestoreSequenceReservation();
        lock (_gate) _pendingRestores.Add(reservation);
        bool writeSucceeded;
        long sequence;
        try
        {
            (writeSucceeded, sequence) = await Task.Run(() =>
            {
                var succeeded = _clipboard.TrySetText(item.Text, out var clipboardSequence);
                return (succeeded, clipboardSequence);
            }, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            lock (_gate) _pendingRestores.Remove(reservation);
            throw;
        }
        if (!writeSucceeded)
        {
            lock (_gate) _pendingRestores.Remove(reservation);
            SetAvailability(false, "Clipboard is temporarily unavailable.");
            return false;
        }
        lock (_gate)
        {
            reservation.Sequence = sequence;
            reservation.Completed = true;
            if (reservation.CaptureArrived && sequence != 0) _restoreSequences.Add(sequence);
            if (sequence == 0) reservation.SuppressionUnavailable = true;
            _pendingRestores.Remove(reservation);
        }
        SetAvailability(true);
        return true;
    }

    public async ValueTask<bool> SetPinnedAsync(string id, bool isPinned, CancellationToken cancellationToken = default)
    {
        List<ClipboardArtifact> snapshot;
        List<ClipboardArtifact> original;
        lock (_gate)
        {
            var index = _items.FindIndex(item => item.Id == id);
            if (index < 0) return false;
            if (isPinned && !_items[index].IsPinned && _items.Count >= MaximumHistoryItems && _items.All(item => item.IsPinned)) return false;
            original = _items.ToList();
            _items[index] = _items[index] with { IsPinned = isPinned };
            snapshot = _items.ToList();
        }
        try { await PersistAndPublishAsync(snapshot, cancellationToken).ConfigureAwait(false); }
        catch (Exception ex) when (IsPersistenceFailure(ex))
        {
            Rollback(original);
            throw;
        }
        return true;
    }

    public async ValueTask<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        List<ClipboardArtifact> snapshot;
        List<ClipboardArtifact> original;
        lock (_gate)
        {
            if (!_items.Any(item => item.Id == id)) return false;
            original = _items.ToList();
            _items.RemoveAll(item => item.Id == id);
            snapshot = _items.ToList();
        }
        try { await PersistAndPublishAsync(snapshot, cancellationToken).ConfigureAwait(false); }
        catch (Exception ex) when (IsPersistenceFailure(ex))
        {
            Rollback(original);
            throw;
        }
        return true;
    }

    public void SetAvailability(bool isAvailable, string? status = null)
    {
        lock (_gate) { _available = isAvailable; _status = status; }
        PublishChanged();
    }

    private ValueTask HandleEventAsync(RuntimeEvent runtimeEvent, CancellationToken cancellationToken)
    {
        if (runtimeEvent is not ClipboardCaptureEvent capture) return ValueTask.CompletedTask;
        return new ValueTask(CaptureAsync(capture, cancellationToken).AsTask());
    }

    public async ValueTask<bool> CaptureAsync(ClipboardCaptureEvent capture, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(capture);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (capture.ClipboardSequenceNumber != 0 && _restoreSequences.Remove(capture.ClipboardSequenceNumber)) return false;
            if (_pendingRestores.Count > 0)
            {
                foreach (var reservation in _pendingRestores)
                {
                    if (!reservation.Completed && capture.ClipboardSequenceNumber != 0)
                    {
                        reservation.Sequence = capture.ClipboardSequenceNumber;
                        reservation.Completed = true;
                        reservation.CaptureArrived = true;
                        return false;
                    }
                    if (reservation.Sequence == capture.ClipboardSequenceNumber)
                    {
                        reservation.CaptureArrived = true;
                        return false;
                    }
                    if (reservation.SuppressionUnavailable) return false;
                }
            }
        }
        if (string.IsNullOrEmpty(capture.Text) || _settings.Current.RetentionDays == 0) return false;
        var sensitive = _classifier.IsSensitive(capture.Text);
        if (sensitive && !_settings.Current.AllowSensitiveData) return false;
        List<ClipboardArtifact> snapshot;
        List<ClipboardArtifact> original;
        lock (_gate)
        {
            var now = _clock.GetUtcNow();
            original = _items.ToList();
            _items.RemoveAll(item => !item.IsPinned && IsExpired(item, now));
            if (_items.Count >= MaximumHistoryItems && _items.All(item => item.IsPinned)) return false;
            var mostRecent = _items.Count == 0 ? null : _items[0];
            if (mostRecent is not null && string.Equals(mostRecent.Text, capture.Text, StringComparison.Ordinal))
            {
                var refreshed = mostRecent with
                {
                    CapturedAtUtc = capture.OccurredAtUtc,
                    SourceApp = capture.SourceApp ?? mostRecent.SourceApp,
                    IsSensitive = sensitive
                };
                _items.RemoveAll(item => item.Id == mostRecent.Id);
                _items.Insert(0, refreshed);
            }
            else
            {
                _items.Insert(0, new ClipboardArtifact(Guid.NewGuid().ToString("N"), capture.Text, capture.OccurredAtUtc, false, sensitive, capture.SourceApp));
            }
            while (_items.Count > MaximumHistoryItems)
            {
                var oldestUnpinned = _items.FindLastIndex(item => !item.IsPinned);
                if (oldestUnpinned < 0) break;
                _items.RemoveAt(oldestUnpinned);
            }
            snapshot = _items.ToList();
        }
        try { await PersistAndPublishAsync(snapshot, cancellationToken).ConfigureAwait(false); }
        catch (Exception ex) when (IsPersistenceFailure(ex))
        {
            Rollback(original);
            throw;
        }
        catch
        {
            Rollback(original);
            throw;
        }
        return true;
    }

    private async ValueTask EnforceRetentionAsync(CancellationToken cancellationToken)
    {
        List<ClipboardArtifact> snapshot;
        lock (_gate)
        {
            var now = _clock.GetUtcNow();
            _items.RemoveAll(item => !item.IsPinned && IsExpired(item, now));
            if (_items.Count > MaximumHistoryItems) _items = _items.Take(MaximumHistoryItems).ToList();
            snapshot = _items.ToList();
        }
        await _persistence.SaveAsync(snapshot, cancellationToken).ConfigureAwait(false);
    }

    private bool IsExpired(ClipboardArtifact item, DateTimeOffset now)
        => _settings.Current.RetentionDays == 0 || now - item.CapturedAtUtc >= TimeSpan.FromDays(_settings.Current.RetentionDays);

    private async ValueTask PersistAndPublishAsync(List<ClipboardArtifact> snapshot, CancellationToken cancellationToken)
    {
        await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _persistence.SaveAsync(snapshot, cancellationToken).ConfigureAwait(false);
            lock (_gate) { _available = true; _status = null; }
        }
        catch (IOException)
        {
            SetAvailability(false, "Clipboard history could not be saved.");
            throw;
        }
        catch (UnauthorizedAccessException)
        {
            SetAvailability(false, "Clipboard history could not be saved.");
            throw;
        }
        finally { _writeGate.Release(); }
        PublishChanged();
    }

    private void Rollback(List<ClipboardArtifact> original)
    {
        lock (_gate) _items = original;
        PublishChanged();
    }

    private static bool IsPersistenceFailure(Exception exception)
        => exception is IOException or UnauthorizedAccessException;

    private void OnSettingsChanged(object? sender, AuraSettings settings)
    {
        if (!settings.AllowSensitiveData)
        {
            List<ClipboardArtifact> snapshot;
            lock (_gate) { _items.RemoveAll(item => item.IsSensitive); snapshot = _items.ToList(); }
            PublishChanged();
            _ = PersistSensitivePurgeAsync(snapshot);
            return;
        }
        _ = EnforceRetentionAsync(CancellationToken.None);
    }

    private async Task PersistSensitivePurgeAsync(List<ClipboardArtifact> snapshot)
    {
        try { await PersistAndPublishAsync(snapshot, CancellationToken.None).ConfigureAwait(false); }
        catch (Exception ex) when (IsPersistenceFailure(ex)) { }
    }

    private sealed class RestoreSequenceReservation
    {
        public long Sequence { get; set; }
        public bool Completed { get; set; }
        public bool CaptureArrived { get; set; }
        public bool SuppressionUnavailable { get; set; }
    }

    private void PublishChanged() => Changed?.Invoke(this, Current);

    public void Dispose()
    {
        _subscription.Dispose();
        _settings.Changed -= OnSettingsChanged;
        _writeGate.Dispose();
    }
}
