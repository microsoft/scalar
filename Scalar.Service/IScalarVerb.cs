namespace Scalar.Service
{
    public interface IScalarVerb
    {
        bool CallMaintenance(string task, string repoRoot, int sessionId);
    }
}
