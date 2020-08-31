using System.Runtime.InteropServices;

namespace Scalar.Upgrader
{
    public static class UpgradeOrchestratorFactory
    {
        public static UpgradeOrchestrator Create(UpgradeOptions options)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return new WindowsUpgradeOrchestrator(options);
            }

            throw new System.NotImplementedException();
        }
    }
}
