using Scalar.Tests.Should;
using System.Diagnostics;
using System.IO;

namespace Scalar.FunctionalTests.Tools
{
    public class ScalarProcess
    {
        private const int SuccessExitCode = 0;
        private const int ExitCodeShouldNotBeZero = -1;
        private const int DoNotCheckExitCode = -2;

        private readonly string pathToScalar;
        private readonly string enlistmentRoot;
        private readonly string localCacheRoot;

        public ScalarProcess(ScalarFunctionalTestEnlistment enlistment)
            : this(ScalarTestConfig.PathToScalar, enlistment.EnlistmentRoot, Path.Combine(enlistment.EnlistmentRoot, ".scalarCache"))
        {
        }

        public ScalarProcess(string pathToScalar, string enlistmentRoot, string localCacheRoot)
        {
            this.pathToScalar = pathToScalar;
            this.enlistmentRoot = enlistmentRoot;
            this.localCacheRoot = localCacheRoot;
        }

        public void Clone(string repositorySource, string branchToCheckout, bool skipFetchCommitsAndTrees, bool fullClone = true)
        {
            // TODO: consider sparse clone for functional tests
            string args = string.Format(
                "clone \"{0}\" \"{1}\" {2} --branch \"{3}\" --local-cache-path \"{4}\" {5}",
                repositorySource,
                this.enlistmentRoot,
                fullClone ? "--full-clone" : string.Empty,
                branchToCheckout,
                this.localCacheRoot,
                skipFetchCommitsAndTrees ? "--no-fetch-commits-and-trees" : string.Empty);
            this.CallScalar(args, expectedExitCode: SuccessExitCode);
        }

        public void Mount()
        {
            string output;
            this.TryMount(out output).ShouldEqual(true, "Scalar did not mount: " + output);

            // TODO: Re-add this warning after we work out the version detail information
            // output.ShouldNotContain(ignoreCase: true, unexpectedSubstrings: "warning");
        }

        public bool TryMount(out string output)
        {
            this.IsEnlistmentMounted().ShouldEqual(false, "Scalar is already mounted");
            output = this.CallScalar("mount \"" + this.enlistmentRoot + "\"");
            return this.IsEnlistmentMounted();
        }

        public string FetchCommitsAndTrees(bool failOnError, string standardInput = null)
        {
            return this.CallScalar(
                $"maintenance \"{this.enlistmentRoot}\" --task fetch-commits-and-trees",
                failOnError ? SuccessExitCode : DoNotCheckExitCode,
                standardInput: standardInput);
        }

        public void Repair(bool confirm)
        {
            string confirmArg = confirm ? "--confirm " : string.Empty;
            this.CallScalar(
                "repair " + confirmArg + "\"" + this.enlistmentRoot + "\"",
                expectedExitCode: SuccessExitCode);
        }

        public string CommitGraphStep()
        {
            return this.CallScalar(
                $"maintenance \"{this.enlistmentRoot}\" --task commit-graph",
                expectedExitCode: SuccessExitCode);
        }

        public string LooseObjectStep()
        {
            return this.CallScalar(
                $"maintenance \"{this.enlistmentRoot}\" --task loose-objects",
                expectedExitCode: SuccessExitCode);
        }

        public string PackfileMaintenanceStep(long? batchSize)
        {
            string sizeString = batchSize.HasValue ? $"--batch-size {batchSize.Value}" : string.Empty;
            return this.CallScalar(
                $"maintenance \"{this.enlistmentRoot}\" --task pack-files {sizeString}",
                expectedExitCode: SuccessExitCode);
        }

        public string ReposAdd(string enlistmentRoot)
        {
            return this.CallScalar($"repos add", expectedExitCode: SuccessExitCode, workingDirectory: enlistmentRoot);
        }

        public string ReposList()
        {
            return this.CallScalar($"repos list", expectedExitCode: SuccessExitCode, workingDirectory: this.enlistmentRoot);
        }

        public string Diagnose()
        {
            return this.CallScalar("diagnose \"" + this.enlistmentRoot + "\"");
        }

        public string Status(string trace = null)
        {
            return this.CallScalar("status " + this.enlistmentRoot, trace: trace);
        }

        public string CacheServer(string args)
        {
            return this.CallScalar("cache-server " + args + " \"" + this.enlistmentRoot + "\"");
        }

        public void Unmount()
        {
            if (this.IsEnlistmentMounted())
            {
                string result = this.CallScalar("unmount \"" + this.enlistmentRoot + "\"", expectedExitCode: SuccessExitCode);
                this.IsEnlistmentMounted().ShouldEqual(false, "Scalar did not unmount: " + result);
            }
        }

        public bool IsEnlistmentMounted()
        {
            string statusResult = this.CallScalar("status \"" + this.enlistmentRoot + "\"");
            return statusResult.Contains("Mount status: Ready");
        }

        public string RunServiceVerb(string argument)
        {
            return this.CallScalar("service " + argument, expectedExitCode: SuccessExitCode);
        }

        public string ReadConfig(string key, bool failOnError)
        {
            return this.CallScalar($"config {key}", failOnError ? SuccessExitCode : DoNotCheckExitCode).TrimEnd('\r', '\n');
        }

        public void WriteConfig(string key, string value)
        {
            this.CallScalar($"config {key} {value}", expectedExitCode: SuccessExitCode);
        }

        public void DeleteConfig(string key)
        {
            this.CallScalar($"config --delete {key}", expectedExitCode: SuccessExitCode);
        }

        /// <summary>
        /// Invokes a call to scalar using the arguments specified
        /// </summary>
        /// <param name="args">The arguments to use when invoking scalar</param>
        /// <param name="expectedExitCode">
        /// What the expected exit code should be.
        /// >= than 0 to check the exit code explicitly
        /// -1 = Fail if the exit code is 0
        /// -2 = Do not check the exit code (Default)
        /// </param>
        /// <param name="trace">What to set the GIT_TRACE environment variable to</param>
        /// <param name="standardInput">What to write to the standard input stream</param>
        /// <param name="internalParameter">The internal parameter to set in the arguments</param>
        /// <returns></returns>
        private string CallScalar(
                            string args,
                            int expectedExitCode = DoNotCheckExitCode,
                            string trace = null,
                            string standardInput = null,
                            string internalParameter = null,
                            string workingDirectory = null)
        {
            ProcessStartInfo processInfo = new ProcessStartInfo(this.pathToScalar);

            if (internalParameter == null)
            {
                internalParameter = ScalarHelpers.GetInternalParameter();
            }

            processInfo.Arguments = args + " " + TestConstants.InternalUseOnlyFlag + " " + internalParameter;

            if (!string.IsNullOrEmpty(workingDirectory))
            {
                processInfo.WorkingDirectory = workingDirectory;
            }

            processInfo.WindowStyle = ProcessWindowStyle.Hidden;
            processInfo.UseShellExecute = false;
            processInfo.RedirectStandardOutput = true;
            if (standardInput != null)
            {
                processInfo.RedirectStandardInput = true;
            }

            if (trace != null)
            {
                processInfo.EnvironmentVariables["GIT_TRACE"] = trace;
            }

            using (Process process = Process.Start(processInfo))
            {
                if (standardInput != null)
                {
                    process.StandardInput.Write(standardInput);
                    process.StandardInput.Close();
                }

                string result = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                if (expectedExitCode >= SuccessExitCode)
                {
                    process.ExitCode.ShouldEqual(expectedExitCode, result);
                }
                else if (expectedExitCode == ExitCodeShouldNotBeZero)
                {
                    process.ExitCode.ShouldNotEqual(SuccessExitCode, "Exit code should not be zero");
                }

                return result;
            }
        }
    }
}
