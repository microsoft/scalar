using GVFS.Common;
using GVFS.Common.Git;
using GVFS.Platform.Mac;

namespace GVFS.PlatformLoader
{
    public static class GVFSPlatformLoader
    {
        public static void Initialize()
        {
            GVFSPlatform.Register(new MacPlatform());
            return;
        }
     }
}