using System.Text.Json;

namespace Aura.Runtime.Settings;

public sealed class JsonSettingsStore : ISettingsStore
{
    private readonly string _path;
    private readonly object _gate = new();
    private AuraSettings _current = new();

    public JsonSettingsStore() : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Aura", "settings.json"))
    {
    }

    public JsonSettingsStore(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = path;
        if (File.Exists(_path))
        {
            try
            {
                _current = Validate(FromJson(JsonSerializer.Deserialize<SettingsDocument>(File.ReadAllText(_path))));
            }
            catch (JsonException) { }
            catch (IOException) { }
        }
    }

    public AuraSettings Current { get { lock (_gate) return _current; } }
    public event EventHandler<AuraSettings>? Changed;

    public async ValueTask<AuraSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            if (!File.Exists(_path)) return Current;
            await using var stream = File.OpenRead(_path);
            var document = await JsonSerializer.DeserializeAsync<SettingsDocument>(stream, cancellationToken: cancellationToken);
            var settings = Validate(FromJson(document));
            lock (_gate) _current = settings;
            return settings;
        }
        catch (JsonException)
        {
            return Current;
        }
        catch (IOException)
        {
            return Current;
        }
        catch (UnauthorizedAccessException)
        {
            return Current;
        }
    }

    public async ValueTask SaveAsync(AuraSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings = Validate(settings);
        var directory = Path.GetDirectoryName(_path);
        if (string.IsNullOrWhiteSpace(directory)) throw new InvalidOperationException("Settings path must include a directory.");
        Directory.CreateDirectory(directory);
        await using var stream = File.Create(_path);
        await JsonSerializer.SerializeAsync(stream, ToJson(settings), cancellationToken: cancellationToken);
        lock (_gate) _current = settings;
        Changed?.Invoke(this, settings);
    }

    private static AuraSettings FromJson(SettingsDocument? document)
        => document is null
            ? new AuraSettings()
            : new AuraSettings(document.SurfaceEnabled, document.ReducedMotion, document.AllowSensitiveData,
                document.RetentionDays, document.PrioritySources?.ToHashSet(StringComparer.OrdinalIgnoreCase));

    private static SettingsDocument ToJson(AuraSettings settings)
        => new(settings.SurfaceEnabled, settings.ReducedMotion, settings.AllowSensitiveData,
            settings.RetentionDays, settings.EffectivePrioritySources.ToArray());

    private static AuraSettings Validate(AuraSettings settings)
        => settings with
        {
            RetentionDays = Math.Clamp(settings.RetentionDays, 0, 3650),
            PrioritySources = new HashSet<string>(settings.EffectivePrioritySources
                .Where(source => !string.IsNullOrWhiteSpace(source))
                .Select(source => source.Trim()), StringComparer.OrdinalIgnoreCase)
        };

    private sealed record SettingsDocument(
        bool SurfaceEnabled = true,
        bool ReducedMotion = false,
        bool AllowSensitiveData = false,
        int RetentionDays = 30,
        string[]? PrioritySources = null);
}
