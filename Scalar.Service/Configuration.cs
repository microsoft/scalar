using Scalar.Common;
using System.IO;

namespace Scalar.Service
{
    public class Configuration
    {
        private static Configuration instance = new Configuration();
        private static string assemblyPath = null;

        private Configuration()
        {
            this.ScalarLocation = Path.Combine(AssemblyPath, ScalarPlatform.Instance.Constants.ScalarExecutableName);
            this.ScalarServiceUILocation = Path.Combine(AssemblyPath, ScalarConstants.Service.UIName + ScalarPlatform.Instance.Constants.ExecutableExtension);
        }

        public static Configuration Instance
        {
            get
            {
                return instance;
            }
        }

        public static string AssemblyPath
        {
            get
            {
                if (assemblyPath == null)
                {
                    assemblyPath = ProcessHelper.GetCurrentProcessLocation();
                }

                return assemblyPath;
            }
        }

        public string ScalarLocation { get; private set; }
        public string ScalarServiceUILocation { get; private set; }
    }
}
