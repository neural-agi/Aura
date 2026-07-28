namespace Aura.Runtime;

public sealed record RuntimeStartedEvent(DateTimeOffset OccurredAtUtc) : RuntimeEvent(OccurredAtUtc);

