namespace Scalar.Upgrader
{
    public static class UpgradeOrchestratorFactory
    {
        public static UpgradeOrchestrator Create(UpgradeOptions options)
        {
#if MACOS_BUILD
            return new MacUpgradeOrchestrator(options);
#elif WINDOWS_BUILD
            return new WindowsUpgradeOrchestrator(options);
#else
            throw new System.NotImplementedException();
#endif
            }
    }
}
