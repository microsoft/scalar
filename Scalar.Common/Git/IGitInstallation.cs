namespace Scalar.Common.Git
{
    public interface IGitInstallation
    {
        bool GitExists(string gitBinPath);
        string GetInstalledGitBinPath();
    }
}
