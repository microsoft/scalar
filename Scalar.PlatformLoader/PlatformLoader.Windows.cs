using Scalar.Common;
using Scalar.Platform.Windows;

namespace Scalar.PlatformLoader
{
    public static class ScalarPlatformLoader
    {
        public static void Initialize()
        {
            ScalarPlatform.Register(new WindowsPlatform());
            return;
        }
    }
}
