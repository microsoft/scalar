using CommandLine;
using Scalar.PlatformLoader;

namespace Scalar.Upgrader
{
    public class Program
    {
        public static void Main(string[] args)
        {
            ScalarPlatformLoader.Initialize();

            Parser.Default.ParseArguments<UpgradeOptions>(args)
                .WithParsed(options =>  UpgradeOrchestratorFactory.Create(options).Execute());
        }
    }
}
