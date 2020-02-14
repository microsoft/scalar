using System;
using NUnit.Framework;
using Scalar.Common.Git;
using Scalar.UnitTests.Mock.Common;

namespace Scalar.UnitTests.Git
{
    [TestFixture]
    public class GitCredentialManagerProcessTests
    {
        private const string GcmBinPath = @"C:\gcm\gcm.exe";
        private const string WorkingDirectoryRoot = @"C:\Windows";

        [TestCase]
        public void TryGetCredential_UrlHostOnly_InvokesGcmGetWithUrlHostOnly()
        {
            const string expectedUsername = "john.doe";
            const string expectedPassword = "letmein123";
            const string repoProtocol = "https";
            const string repoHost = "example.com";
            string repoUrl = $"{repoProtocol}://{repoHost}";

            string expectedStdin = $"protocol={repoProtocol}\n" +
                                   $"host={repoHost}\n" +
                                   "\n";

            var tracer = new MockTracer();

            var proc = new TestGcmProcess(GcmBinPath, WorkingDirectoryRoot)
            {
                InvokeFunc = (command, stdin) =>
                {
                    Assert.AreEqual("get", command);
                    Assert.AreEqual(expectedStdin, stdin);

                    string stdout = $"{stdin}username={expectedUsername}\npassword={expectedPassword}\n";
                    string stderr = null;
                    int exitCode = 0;

                    return new GitCredentialManagerProcess.Result(stdout, stderr, exitCode);
                }
            };

            bool result = proc.TryGetCredential(tracer, repoUrl, out string username, out string password, out string errorMessage);

            Assert.True(result);
            Assert.AreEqual(expectedUsername, username);
            Assert.AreEqual(expectedPassword, password);
            Assert.IsNull(errorMessage);
        }

        [TestCase]
        public void TryGetCredential_UrlWithPath_InvokesGcmGetWithUrlHostAndPath()
        {
            const string expectedUsername = "john.doe";
            const string expectedPassword = "letmein123";
            const string repoProtocol = "https";
            const string repoHost = "example.com";
            const string repoPath = "/path/to/some/repo";
            string repoUrl = $"{repoProtocol}://{repoHost}{repoPath}";

            string expectedStdin = $"protocol={repoProtocol}\n" +
                                   $"host={repoHost}\n" +
                                   $"path={repoPath}" +
                                   "\n\n";

            var tracer = new MockTracer();

            var proc = new TestGcmProcess(GcmBinPath, WorkingDirectoryRoot)
            {
                InvokeFunc = (command, stdin) =>
                {
                    Assert.AreEqual("get", command);
                    Assert.AreEqual(expectedStdin, stdin);

                    string stdout = $"{stdin}username={expectedUsername}\npassword={expectedPassword}\n";
                    string stderr = null;
                    int exitCode = 0;

                    return new GitCredentialManagerProcess.Result(stdout, stderr, exitCode);
                }
            };

            bool result = proc.TryGetCredential(tracer, repoUrl, out string username, out string password, out string errorMessage);

            Assert.True(result);
            Assert.AreEqual(expectedUsername, username);
            Assert.AreEqual(expectedPassword, password);
            Assert.IsNull(errorMessage);
        }

        [TestCase]
        public void TryGetCredential_BadUrl_ReturnsFalse()
        {
            const string repoUrl = "this is not a valid URL";

            var tracer = new MockTracer();

            var proc = new TestGcmProcess(GcmBinPath, WorkingDirectoryRoot)
            {
                InvokeFunc = (command, stdin) =>
                {
                    Assert.Fail("Should never invoke GCM with bad URL data");
                    return new GitCredentialManagerProcess.Result(null, null, 127);
                }
            };

            bool result = proc.TryGetCredential(tracer, repoUrl, out string username, out string password, out string errorMessage);

            Assert.False(result);
            Assert.IsNull(username);
            Assert.IsNull(password);
            Assert.IsNotNull(errorMessage);
        }

        [TestCase]
        public void TryGetCredential_NonZeroExitCode_ReturnsFalse()
        {
            const string repoProtocol = "https";
            const string repoHost = "example.com";
            string repoUrl = $"{repoProtocol}://{repoHost}";

            const string expectedErrorMessage = "This is an error!";

            var tracer = new MockTracer();

            var proc = new TestGcmProcess(GcmBinPath, WorkingDirectoryRoot)
            {
                InvokeFunc = (command, stdin) =>
                {
                    string stdout = null;
                    string stderr = expectedErrorMessage;
                    int exitCode = 127;

                    return new GitCredentialManagerProcess.Result(stdout, stderr, exitCode);
                }
            };

            bool result = proc.TryGetCredential(tracer, repoUrl, out string username, out string password, out string errorMessage);

            Assert.False(result);
            Assert.IsNull(username);
            Assert.IsNull(password);
            Assert.AreEqual(expectedErrorMessage, errorMessage);
        }

        [TestCase]
        public void TryStoreCredential_UrlHostOnly_InvokesGcmStoreWithUrlHostOnly()
        {
            const string username = "john.doe";
            const string password = "letmein123";
            const string repoProtocol = "https";
            const string repoHost = "example.com";
            string repoUrl = $"{repoProtocol}://{repoHost}";

            string expectedStdin = $"protocol={repoProtocol}\n" +
                                   $"host={repoHost}\n" +
                                   $"username={username}\n" +
                                   $"password={password}\n" +
                                   "\n";

            var tracer = new MockTracer();

            var proc = new TestGcmProcess(GcmBinPath, WorkingDirectoryRoot)
            {
                InvokeFunc = (command, stdin) =>
                {
                    Assert.AreEqual("store", command);
                    Assert.AreEqual(expectedStdin, stdin);

                    string stdout = null;
                    string stderr = null;
                    int exitCode = 0;

                    return new GitCredentialManagerProcess.Result(stdout, stderr, exitCode);
                }
            };

            bool result = proc.TryStoreCredential(tracer, repoUrl, username, password, out string errorMessage);

            Assert.True(result);
            Assert.IsNull(errorMessage);
        }

        [TestCase]
        public void TryStoreCredential_UrlWithPath_InvokesGcmStoreWithUrlHostAndPath()
        {
            const string username = "john.doe";
            const string password = "letmein123";
            const string repoProtocol = "https";
            const string repoHost = "example.com";
            const string repoPath = "/path/to/some/repo";
            string repoUrl = $"{repoProtocol}://{repoHost}{repoPath}";

            string expectedStdin = $"protocol={repoProtocol}\n" +
                                   $"host={repoHost}\n" +
                                   $"path={repoPath}\n" +
                                   $"username={username}\n" +
                                   $"password={password}\n" +
                                   "\n";

            var tracer = new MockTracer();

            var proc = new TestGcmProcess(GcmBinPath, WorkingDirectoryRoot)
            {
                InvokeFunc = (command, stdin) =>
                {
                    Assert.AreEqual("store", command);
                    Assert.AreEqual(expectedStdin, stdin);

                    string stdout = null;
                    string stderr = null;
                    int exitCode = 0;

                    return new GitCredentialManagerProcess.Result(stdout, stderr, exitCode);
                }
            };

            bool result = proc.TryStoreCredential(tracer, repoUrl, username, password, out string errorMessage);

            Assert.True(result);
            Assert.IsNull(errorMessage);
        }

        [TestCase]
        public void TryStoreCredential_BadUrl_ReturnsFalse()
        {
            const string username = "john.doe";
            const string password = "letmein123";
            const string repoUrl = "this is not a valid URL";

            var tracer = new MockTracer();

            var proc = new TestGcmProcess(GcmBinPath, WorkingDirectoryRoot)
            {
                InvokeFunc = (command, stdin) =>
                {
                    Assert.Fail("Should never invoke GCM with bad URL data");
                    return new GitCredentialManagerProcess.Result(null, null, 127);
                }
            };

            bool result = proc.TryStoreCredential(tracer, repoUrl, username, password, out string errorMessage);

            Assert.False(result);
            Assert.IsNotNull(errorMessage);
        }

        [TestCase]
        public void TryStoreCredential_NonZeroExitCode_ReturnsFalse()
        {
            const string username = "john.doe";
            const string password = "letmein123";
            const string repoProtocol = "https";
            const string repoHost = "example.com";
            string repoUrl = $"{repoProtocol}://{repoHost}";

            const string expectedErrorMessage = "This is an error!";

            var tracer = new MockTracer();

            var proc = new TestGcmProcess(GcmBinPath, WorkingDirectoryRoot)
            {
                InvokeFunc = (command, stdin) =>
                {
                    string stdout = null;
                    string stderr = expectedErrorMessage;
                    int exitCode = 127;

                    return new GitCredentialManagerProcess.Result(stdout, stderr, exitCode);
                }
            };

            bool result = proc.TryStoreCredential(tracer, repoUrl, username, password, out string errorMessage);

            Assert.False(result);
            Assert.AreEqual(expectedErrorMessage, errorMessage);
        }

        [TestCase]
        public void TryDeleteCredential_UrlHostOnly_InvokesGcmEraseWithUrlHostOnly()
        {
            const string username = "john.doe";
            const string password = "letmein123";
            const string repoProtocol = "https";
            const string repoHost = "example.com";
            string repoUrl = $"{repoProtocol}://{repoHost}";

            string expectedStdin = $"protocol={repoProtocol}\n" +
                                   $"host={repoHost}\n" +
                                   "\n";

            var tracer = new MockTracer();

            var proc = new TestGcmProcess(GcmBinPath, WorkingDirectoryRoot)
            {
                InvokeFunc = (command, stdin) =>
                {
                    Assert.AreEqual("erase", command);
                    Assert.AreEqual(expectedStdin, stdin);

                    string stdout = null;
                    string stderr = null;
                    int exitCode = 0;

                    return new GitCredentialManagerProcess.Result(stdout, stderr, exitCode);
                }
            };

            bool result = proc.TryDeleteCredential(tracer, repoUrl, username, password, out string errorMessage);

            Assert.True(result);
            Assert.IsNull(errorMessage);
        }

        [TestCase]
        public void TryDeleteCredential_UrlWithPath_InvokesGcmEraseWithUrlHostAndPath()
        {
            const string username = "john.doe";
            const string password = "letmein123";
            const string repoProtocol = "https";
            const string repoHost = "example.com";
            const string repoPath = "/path/to/some/repo";
            string repoUrl = $"{repoProtocol}://{repoHost}{repoPath}";

            string expectedStdin = $"protocol={repoProtocol}\n" +
                                   $"host={repoHost}\n" +
                                   $"path={repoPath}" +
                                   "\n\n";

            var tracer = new MockTracer();

            var proc = new TestGcmProcess(GcmBinPath, WorkingDirectoryRoot)
            {
                InvokeFunc = (command, stdin) =>
                {
                    Assert.AreEqual("erase", command);
                    Assert.AreEqual(expectedStdin, stdin);

                    string stdout = null;
                    string stderr = null;
                    int exitCode = 0;

                    return new GitCredentialManagerProcess.Result(stdout, stderr, exitCode);
                }
            };

            bool result = proc.TryDeleteCredential(tracer, repoUrl, username, password, out string errorMessage);

            Assert.True(result);
            Assert.IsNull(errorMessage);
        }

        [TestCase]
        public void TryDeleteCredential_BadUrl_ReturnsFalse()
        {
            const string username = "john.doe";
            const string password = "letmein123";
            const string repoUrl = "this is not a valid URL";

            var tracer = new MockTracer();

            var proc = new TestGcmProcess(GcmBinPath, WorkingDirectoryRoot)
            {
                InvokeFunc = (command, stdin) =>
                {
                    Assert.Fail("Should never invoke GCM with bad URL data");
                    return new GitCredentialManagerProcess.Result(null, null, 127);
                }
            };

            bool result = proc.TryDeleteCredential(tracer, repoUrl, username, password, out string errorMessage);

            Assert.False(result);
            Assert.IsNotNull(errorMessage);
        }

        [TestCase]
        public void TryDeleteCredential_NonZeroExitCode_ReturnsFalse()
        {
            const string username = "john.doe";
            const string password = "letmein123";
            const string repoProtocol = "https";
            const string repoHost = "example.com";
            string repoUrl = $"{repoProtocol}://{repoHost}";

            const string expectedErrorMessage = "This is an error!";

            var tracer = new MockTracer();

            var proc = new TestGcmProcess(GcmBinPath, WorkingDirectoryRoot)
            {
                InvokeFunc = (command, stdin) =>
                {
                    string stdout = null;
                    string stderr = expectedErrorMessage;
                    int exitCode = 127;

                    return new GitCredentialManagerProcess.Result(stdout, stderr, exitCode);
                }
            };

            bool result = proc.TryDeleteCredential(tracer, repoUrl, username, password, out string errorMessage);

            Assert.False(result);
            Assert.AreEqual(expectedErrorMessage, errorMessage);
        }

        private class TestGcmProcess : GitCredentialManagerProcess
        {
            public Func<string, string, Result> InvokeFunc { get; set; }

            public TestGcmProcess(string gcmBinPath, string workingDirectoryRoot)
                : base(gcmBinPath, workingDirectoryRoot) { }

            protected override Result InvokeGcm(string command, string stdIn)
            {
                return InvokeFunc(command, stdIn);
            }
        }
    }
}
