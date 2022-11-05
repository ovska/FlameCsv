using System.Diagnostics.Tracing;

namespace FlameCsv.Tests.Utilities;

public sealed class ArrayPoolEventListener : EventListener
{
    private uint _rentedCount;
    private uint _returnedCount;

    protected override void OnEventSourceCreated(EventSource eventSource)
    {
        base.OnEventSourceCreated(eventSource);

        if (eventSource.Name == "System.Buffers.ArrayPoolEventSource")
        {
            EnableEvents(eventSource, EventLevel.LogAlways, EventKeywords.All);
        }
    }

    protected override void OnEventWritten(EventWrittenEventArgs eventData)
    {
        switch (eventData.EventName)
        {
            case "BufferRented":
                Interlocked.Increment(ref _rentedCount);
                break;
            case "BufferReturned":
                Interlocked.Increment(ref _returnedCount);
                break;
        }
    }

    public override void Dispose()
    {
        base.Dispose();

        if (_rentedCount != _returnedCount)
        {
            throw new Xunit.Sdk.AssertActualExpectedException(
                _rentedCount,
                _returnedCount,
                "Not all rented buffers were returned to the pool.");
        }
    }
}
