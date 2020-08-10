using System;
using System.Runtime.InteropServices;
using Scalar.Common;
using Scalar.Platform.Linux;
using Scalar.Platform.Mac;
using Scalar.Platform.Windows;

namespace Scalar.PlatformLoader
{
    public static class ScalarPlatformLoader
    {
        public static void Initialize()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                ScalarPlatform.Register(new WindowsPlatform());
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                ScalarPlatform.Register(new MacPlatform());
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                ScalarPlatform.Register(new LinuxPlatform());
            }
            else
            {
                throw new NotImplementedException();
            }
        }
    }
}
