using System.IO;

namespace Scalar.Common
{
    public static class Paths
    {
        public static string ConvertPathToGitFormat(string path)
        {
            return path.Replace(Path.DirectorySeparatorChar, ScalarConstants.GitPathSeparator);
        }
    }
}
