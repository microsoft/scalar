using Scalar.FunctionalTests.Tests;
using Scalar.FunctionalTests.Tools;
using NUnit.Framework;
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Scalar.FunctionalTests
{
    [SetUpFixture]
    public class GlobalSetup
    {
        [OneTimeSetUp]
        public void RunBeforeAnyTests()
        {
        }

        [OneTimeTearDown]
        public void RunAfterAllTests()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                string serviceLogFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "Scalar",
                    ScalarServiceProcess.TestServiceName,
                    "Logs");

                Console.WriteLine("Scalar.Service logs at '{0}' attached below.\n\n", serviceLogFolder);
                foreach (string filename in TestResultsHelper.GetAllFilesInDirectory(serviceLogFolder))
                {
                    TestResultsHelper.OutputFileContents(filename);
                }

                ScalarServiceProcess.UninstallService();
            }

            PrintTestCaseStats.PrintRunTimeStats();
        }
    }
}
