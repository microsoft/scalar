using Scalar.Common.Tracing;
using System.Collections.Generic;

namespace Scalar.UnitTests.Mock.Common.Tracing
{
    public class MockListener : EventListener
    {
        public MockListener(EventLevel maxVerbosity, Keywords keywordFilter)
            : base(maxVerbosity, keywordFilter, null)
        {
        }

        public List<string> EventNamesRead { get; set; } = new List<string>();

        protected override void RecordMessageInternal(TraceEventMessage message)
        {
            this.EventNamesRead.Add(message.EventName);
        }
    }
}
