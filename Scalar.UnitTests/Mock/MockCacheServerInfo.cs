using Scalar.Common.Http;

namespace Scalar.UnitTests.Mock
{
    public class MockCacheServerInfo : CacheServerInfo
    {
        public MockCacheServerInfo() : base("https://mock", "mock")
        {
        }
    }
}
