using Scalar.Common;
using Scalar.Common.Git;
using Scalar.Platform.Mac;

namespace Scalar.PlatformLoader
{
    public static class ScalarPlatformLoader
    {
        public static void Initialize()
        {
            ScalarPlatform.Register(new MacPlatform());
            return;
        }
     }
}
