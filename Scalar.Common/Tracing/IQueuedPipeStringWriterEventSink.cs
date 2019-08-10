using System;

namespace Scalar.Common.Tracing
{
    public interface IQueuedPipeStringWriterEventSink
    {
        void OnStateChanged(QueuedPipeStringWriter writer, QueuedPipeStringWriterState state, Exception exception);
    }
}
