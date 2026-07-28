namespace Aura.Runtime;

public sealed record RuntimeStoppedEvent(DateTimeOffset OccurredAtUtc) : RuntimeEvent(OccurredAtUtc);

