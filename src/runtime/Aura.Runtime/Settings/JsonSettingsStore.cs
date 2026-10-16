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
        _path = path;
        if (File.Exists(_path))
        {
            try
            {
                _current = Validate(JsonSerializer.Deserialize<AuraSettings>(File.ReadAllText(_path)) ?? new());
            }
            catch (JsonException) { }
            catch (IOException) { }
        }
    }

    public AuraSettings Current { get { lock (_gate) return _current; } }
    public event EventHandler<AuraSettings>? Changed;

    public async ValueTask<AuraSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(_path)) return Current;
            await using var stream = File.OpenRead(_path);
            var settings = await JsonSerializer.DeserializeAsync<AuraSettings>(stream, cancellationToken: cancellationToken) ?? new();
            settings = Validate(settings);
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
    }

    public async ValueTask SaveAsync(AuraSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings = Validate(settings);
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        await using var stream = File.Create(_path);
        await JsonSerializer.SerializeAsync(stream, settings, cancellationToken: cancellationToken);
        lock (_gate) _current = settings;
        Changed?.Invoke(this, settings);
    }

    private static AuraSettings Validate(AuraSettings settings)
        => settings with { RetentionDays = Math.Clamp(settings.RetentionDays, 0, 3650) };
}
