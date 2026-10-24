using Aura.UI.Surface;
using FluentAssertions;

namespace Aura.Tests.Unit;

public sealed class LatestValueRenderQueueTests
{
    [Xunit.Fact]
    public void RapidUpdatesKeepOneQueuedRenderAndRenderNewestValue()
    {
        var dispatcher = new QueuedDispatcher();
        var rendered = new List<Value>();
        var queue = new LatestValueRenderQueue<Value>(rendered.Add, dispatcher.Enqueue, () => throw new InvalidOperationException("Unexpected enqueue failure."));

        queue.Submit(new Value(0.60));
        queue.Submit(new Value(0.59));
        queue.Submit(new Value(0.58));
        queue.Submit(new Value(0.57));
        queue.Submit(new Value(0.56));

        dispatcher.PendingCount.Should().Be(1);
        dispatcher.RunNext();
        rendered.Should().ContainSingle().Which.Level.Should().Be(0.56);
        dispatcher.PendingCount.Should().Be(0);
    }

    [Xunit.Fact]
    public void UpdateArrivingDuringRenderSchedulesOneFollowUpWithLatestValue()
    {
        var dispatcher = new QueuedDispatcher();
        var rendered = new List<Value>();
        LatestValueRenderQueue<Value>? queue = null;
        queue = new LatestValueRenderQueue<Value>(value =>
        {
            rendered.Add(value);
            if (value.Level == 0.60)
            {
                queue!.Submit(new Value(0.59));
                queue.Submit(new Value(0.58));
                queue.Submit(new Value(0.56));
            }
        }, dispatcher.Enqueue, () => throw new InvalidOperationException("Unexpected enqueue failure."));

        queue.Submit(new Value(0.60));
        dispatcher.RunNext();

        dispatcher.PendingCount.Should().Be(1);
        dispatcher.RunNext();
        rendered.Select(value => value.Level).Should().Equal(0.60, 0.56);
        dispatcher.PendingCount.Should().Be(0);
    }

    [Xunit.Fact]
    public void FailedDispatcherEnqueueResetsQueueForLaterUpdate()
    {
        var dispatcher = new QueuedDispatcher { RejectNext = true };
        var failures = 0;
        var rendered = new List<Value>();
        var queue = new LatestValueRenderQueue<Value>(rendered.Add, dispatcher.Enqueue, () => failures++);

        queue.Submit(new Value(0.60));
        failures.Should().Be(1);
        queue.Submit(new Value(0.56));
        dispatcher.PendingCount.Should().Be(1);
        dispatcher.RunNext();
        rendered.Should().ContainSingle().Which.Level.Should().Be(0.56);
    }

    private sealed record Value(double Level);

    private sealed class QueuedDispatcher
    {
        private readonly Queue<Action> _actions = new();
        public bool RejectNext { get; set; }
        public int PendingCount => _actions.Count;
        public void Enqueue(Action action)
        {
            if (RejectNext)
            {
                RejectNext = false;
                throw new InvalidOperationException("Dispatcher rejected work.");
            }
            _actions.Enqueue(action);
        }
        public void RunNext() => _actions.Dequeue()();
    }
}
