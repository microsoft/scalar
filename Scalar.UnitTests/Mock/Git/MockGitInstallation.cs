using Scalar.Common.Git;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Scalar.UnitTests.Mock.Git
{
    public class MockGitInstallation : IGitInstallation
    {
        public bool GitExists(string gitBinPath)
        {
            return false;
        }

        public string GetInstalledGitBinPath()
        {
            return null;
        }
    }
}
