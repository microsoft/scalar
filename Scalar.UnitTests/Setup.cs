using Scalar.Common;
using Scalar.UnitTests.Mock.Common;
using NUnit.Framework;

namespace Scalar.UnitTests
{
    [SetUpFixture]
    public class Setup
    {
        [OneTimeSetUp]
        public void SetUp()
        {
            ScalarPlatform.Register(new MockPlatform());
        }
    }
}
