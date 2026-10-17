namespace Aura.Runtime;

using Aura.Runtime.Decision;

public interface IRuntimeCore
{
    RuntimeState State { get; }

    RuntimeHealth Health { get; }
    RuntimeEvaluationResult? LastEvaluation { get; }

    RuntimeEvaluationResult Evaluate(RuntimeEvaluationInput input);

    ValueTask StartAsync(CancellationToken cancellationToken = default);

    ValueTask StopAsync(CancellationToken cancellationToken = default);
}

