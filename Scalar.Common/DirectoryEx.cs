using System.IO;
using System.Security.AccessControl;

namespace Scalar.Common
{
    public static class DirectoryEx
    {
        public static DirectorySecurity GetAccessControl(string path)
        {
            return new DirectorySecurity(path, AccessControlSections.All);
        }

        public static void SetAccessControl(string path, DirectorySecurity security)
        {
            var di = new DirectoryInfo(path);
            di.SetAccessControl(security);
        }

        public static void CreateDirectory(string path, DirectorySecurity security)
        {
            var di = new DirectoryInfo(path);
            di.Create();
            di.SetAccessControl(security);
        }
    }
}
