using NUnit.Framework;
using Scalar.FunctionalTests.Tests;
using Scalar.FunctionalTests.Tools;
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
            PrintTestCaseStats.PrintRunTimeStats();
        }
    }
}
