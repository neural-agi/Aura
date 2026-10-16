namespace Aura.Runtime.Settings;

// PROVISIONAL scope note: ReducedMotion and RetentionDays remain persisted/validated preferences but have no M3 runtime consumer.
public sealed record AuraSettings(
    bool SurfaceEnabled = true,
    bool ReducedMotion = false,
    bool AllowSensitiveData = false,
    int RetentionDays = 30,
    IReadOnlySet<string>? PrioritySources = null)
{
    public IReadOnlySet<string> EffectivePrioritySources => PrioritySources ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
}

public interface ISettingsStore
{
    AuraSettings Current { get; }
    event EventHandler<AuraSettings>? Changed;
    ValueTask<AuraSettings> LoadAsync(CancellationToken cancellationToken = default);
    ValueTask SaveAsync(AuraSettings settings, CancellationToken cancellationToken = default);
}
