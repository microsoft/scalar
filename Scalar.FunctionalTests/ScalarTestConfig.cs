using System.IO;

namespace Scalar.FunctionalTests
{
    public static class ScalarTestConfig
    {
        public static string RepoToClone { get; set; }

        public static bool NoSharedCache { get; set; }

        public static string LocalCacheRoot { get; set; }

        public static object[] FileSystemRunners { get; set; }

        public static object[] GitRepoTestsValidateWorkTree { get; set; }

        public static bool TestScalarOnPath { get; set; }

        public static string PathToScalar
        {
            get
            {
                return
                    TestScalarOnPath ?
                    Properties.Settings.Default.PathToScalar :
                    Path.Combine(Properties.Settings.Default.CurrentDirectory, Properties.Settings.Default.PathToScalar);
            }
        }
    }
}
