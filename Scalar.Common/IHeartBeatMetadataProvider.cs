using Scalar.Common.Tracing;

namespace Scalar.Common
{
    public interface IHeartBeatMetadataProvider
    {
        EventMetadata GetAndResetHeartBeatMetadata(out bool logToFile);
    }
}
