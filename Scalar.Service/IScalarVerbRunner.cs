namespace Scalar.Service
{
    public interface IScalarVerbRunner
    {
        bool CallMaintenance(string task, string repoRoot, int sessionId);
    }
}
