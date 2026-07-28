namespace Aura.Runtime;

public sealed record RuntimeStoppingEvent(DateTimeOffset OccurredAtUtc) : RuntimeEvent(OccurredAtUtc);

