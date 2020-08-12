using NUnit.Framework;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Scalar.FunctionalTests.Tools
{
    public static class GitProcess
    {
        public static string Invoke(string executionWorkingDirectory, string command)
        {
            return InvokeProcess(executionWorkingDirectory, command).Output;
        }

        public static ProcessResult InvokeProcess(string executionWorkingDirectory, string command, string inputData, Dictionary<string, string> environmentVariables = null)
        {
            if (inputData == null)
            {
                return InvokeProcess(executionWorkingDirectory, command, environmentVariables);
            }

            using (MemoryStream stream = new MemoryStream())
            {
                using (StreamWriter writer = new StreamWriter(stream, Encoding.Default, bufferSize: 4096, leaveOpen: true))
                {
                    writer.Write(inputData);
                }

                stream.Position = 0;

                return InvokeProcess(executionWorkingDirectory, command, environmentVariables, stream);
            }
        }

        public static ProcessResult InvokeProcess(string executionWorkingDirectory, string command, Dictionary<string, string> environmentVariables = null, Stream inputStream = null)
        {
            ProcessStartInfo processInfo = new ProcessStartInfo(Properties.Settings.Default.PathToGit);
            processInfo.WorkingDirectory = executionWorkingDirectory;
            processInfo.UseShellExecute = false;
            processInfo.RedirectStandardOutput = true;
            processInfo.RedirectStandardError = true;
            processInfo.Arguments = command;

            if (inputStream != null)
            {
                processInfo.RedirectStandardInput = true;
            }

            processInfo.EnvironmentVariables["GIT_TERMINAL_PROMPT"] = "0";

            // Enforce that Git never calls 'crontab' during tests.
            processInfo.EnvironmentVariables["GIT_TEST_CRONTAB"] = "echo";

            // Add some test-specific info to the Trace2 stream to help us
            // identify which TestCase is running.  GIT_TRACE2_ENV_VARS
            // takes a comma-separated list of all of the environment
            // variables we are interested in.
            //
            // [1] "XXX_TEST_FULLNAME" -- The full name of the current
            //     TestCase (along with the test parameters).
            // [2] "XXX_SEQUENCE_ID"   -- The sequence-id for
            //     control-vs-enlistment command pairs.  (This env var
            //     is set in Scalar.FunctionalTests/Tools/GitHelpers.cs
            //     when applicable.)
            //
            // Notes:
            // [a] This only catches Git commands from the functional test
            //     harness and NOT Git commands from GitAPI.  See
            //     `Scalar.FunctionalTests/Tools/GitProcess.cs` and
            //     `Scalar.Common/Git/GitProcess.cs`.
            //
            // [b] This Trace2 decoration may introduce a little confusion
            //     for fsmonitor--daemon.exe instances that are implicitly
            //     spawned by fsmonitor_query_daemon() in a client process
            //     and that persist for the duration of a multi-test-case
            //     fixture.  (The daemon process will (correctly) inherit
            //     the env vars from the spawning client test, but since
            //     it is long-running it may later talk to other test case
            //     clients (which is correct, but confusing.))
            //
            processInfo.EnvironmentVariables["XXX_TEST_FULLNAME"] = TestContext.CurrentContext.Test.FullName;
            processInfo.EnvironmentVariables["GIT_TRACE2_ENV_VARS"] = "XXX_TEST_FULLNAME,XXX_SEQUENCE_ID";

            if (environmentVariables != null)
            {
                foreach (string key in environmentVariables.Keys)
                {
                    processInfo.EnvironmentVariables[key] = environmentVariables[key];
                }
            }

            return ProcessHelper.Run(processInfo, inputStream: inputStream);
        }
    }
}
