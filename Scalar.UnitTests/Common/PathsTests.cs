using NUnit.Framework;
using Scalar.Common;
using Scalar.Tests.Should;
using System.Runtime.InteropServices;

namespace Scalar.UnitTests.Common
{
    [TestFixture]
    public class PathsTests
    {
        [TestCase]
        public void CanConvertOSPathToGitFormat()
        {
            string systemPath;
            string expectedGitPath;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                systemPath = @"C:\This\is\a\path";
                expectedGitPath = @"C:/This/is/a/path";
            }
            else
            {
                systemPath = @"/This/is/a/path";
                expectedGitPath = systemPath;
            }

            string actualTransformedPath = Paths.ConvertPathToGitFormat(systemPath);
            actualTransformedPath.ShouldEqual(expectedGitPath);

            string doubleTransformedPath = Paths.ConvertPathToGitFormat(actualTransformedPath);
            doubleTransformedPath.ShouldEqual(expectedGitPath);
        }
    }
}
