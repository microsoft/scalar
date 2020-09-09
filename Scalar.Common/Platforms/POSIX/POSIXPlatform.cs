using Scalar.Common;
using Scalar.Common.Git;
using Scalar.Common.Tracing;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;

namespace Scalar.Platform.POSIX
{
    public abstract partial class POSIXPlatform : ScalarPlatform
    {
        private const int StdInFileNo = 0;  // STDIN_FILENO  -> standard input file descriptor
        private const int StdOutFileNo = 1; // STDOUT_FILENO -> standard output file descriptor
        private const int StdErrFileNo = 2; // STDERR_FILENO -> standard error file descriptor

        protected POSIXPlatform() : this(
            underConstruction: new UnderConstructionFlags(
                usesCustomUpgrader: false,
                supportsScalarConfig: false,
                supportsNuGetEncryption: false,
                supportsNuGetVerification: false))
        {
        }

        protected POSIXPlatform(UnderConstructionFlags underConstruction)
            : base(underConstruction)
        {
        }

        public override IGitInstallation GitInstallation { get; } = new POSIXGitInstallation();

        public override void ConfigureVisualStudio(string gitBinPath, ITracer tracer)
        {
        }

        public override bool TryVerifyAuthenticodeSignature(string path, out string subject, out string issuer, out string error)
        {
            throw new NotImplementedException();
        }

        public override bool IsProcessActive(int processId)
        {
            return POSIXPlatform.IsProcessActiveImplementation(processId);
        }

        public override void IsServiceInstalledAndRunning(string name, out bool installed, out bool running)
        {
            throw new NotImplementedException();
        }

        public override void StartBackgroundScalarProcess(ITracer tracer, string programName, string[] args)
        {
            string programArguments = string.Empty;
            try
            {
                programArguments = string.Join(" ", args.Select(arg => arg.Contains(' ') ? "\"" + arg + "\"" : arg));
                ProcessStartInfo processInfo = new ProcessStartInfo(programName, programArguments);

                // Redirecting stdin/out/err ensures that all standard input/output file descriptors are properly closed
                // by dup2 before execve is called for the child process
                // (see https://github.com/dotnet/corefx/blob/b10e8d67b260e26f2e47750cf96669e6f48e774d/src/Native/Unix/System.Native/pal_process.c#L381)
                //
                // Testing has shown that without redirecting stdin/err/out code like this:
                //
                //      string result = process.StandardOutput.ReadToEnd();
                //      process.WaitForExit();
                //
                // That waits on a `scalar` verb to exit can hang in the WaitForExit() call because the chuild process has inheritied
                // standard input/output handle(s), and redirecting those streams before spawing the process appears to be the only
                // way to ensure they're properly closed.
                //
                // Note that this approach requires that the child process know that it needs to redirect its standard input/output to /dev/null and
                // so this method can only be used with Scalar processes that are aware they're being launched in the background
                processInfo.RedirectStandardError = true;
                processInfo.RedirectStandardInput = true;
                processInfo.RedirectStandardOutput = true;

                Process executingProcess = new Process();
                executingProcess.StartInfo = processInfo;
                executingProcess.Start();
            }
            catch (Exception ex)
            {
                EventMetadata metadata = new EventMetadata();
                metadata.Add(nameof(programName), programName);
                metadata.Add(nameof(programArguments), programArguments);
                metadata.Add("Exception", ex.ToString());
                tracer.RelatedError(metadata, "Failed to start background process.");
                throw;
            }
        }

        public override void PrepareProcessToRunInBackground()
        {
            int devNullIn = Open("/dev/null", (int)POSIXFileSystem.OpenFlags.O_RDONLY);
            if (devNullIn == -1)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to open /dev/null for reading");
            }

            int devNullOut = Open("/dev/null", (int)POSIXFileSystem.OpenFlags.O_WRONLY);
            if (devNullOut == -1)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to open /dev/null for writing");
            }

            // Redirect stdout/stdin/stderr to "/dev/null"
            if (Dup2(devNullIn, StdInFileNo) == -1 ||
                Dup2(devNullOut, StdOutFileNo) == -1 ||
                Dup2(devNullOut, StdErrFileNo) == -1)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Error redirecting stdout/stdin/stderr to /dev/null");
            }

            Close(devNullIn);
            Close(devNullOut);

            // Become session leader of a new session
            if (SetSid() == -1)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to become session leader");
            }
        }

        public override NamedPipeServerStream CreatePipeByName(string pipeName)
        {
            NamedPipeServerStream pipe = new NamedPipeServerStream(
                pipeName,
                PipeDirection.InOut,
                NamedPipeServerStream.MaxAllowedServerInstances,
                PipeTransmissionMode.Byte,
                PipeOptions.WriteThrough | PipeOptions.Asynchronous,
                0,  // default inBufferSize
                0); // default outBufferSize)

            return pipe;
        }

        public override string GetCurrentUser()
        {
            return Getuid().ToString();
        }

        public override string GetUserIdFromLoginSessionId(int sessionId, ITracer tracer)
        {
            // There are no separate User and Session Ids on POSIX platforms.
            return sessionId.ToString();
        }

        public override Dictionary<string, string> GetPhysicalDiskInfo(string path, bool sizeStatsOnly)
        {
            // TODO(#1356): Collect disk information
            Dictionary<string, string> result = new Dictionary<string, string>();
            result.Add("GetPhysicalDiskInfo", "Not yet implemented on POSIX");
            return result;
        }

        public override void InitializeEnlistmentACLs(string enlistmentPath)
        {
        }

        public override string GetScalarServiceNamedPipeName(string serviceName)
        {
            // Pipes are stored as files on POSIX, use a rooted pipe name
            // in the same location as the service to keep full control of the location of the file
            return this.GetCommonAppDataRootForScalarComponent(serviceName) + ".pipe";
        }

        public override bool IsConsoleOutputRedirectedToFile()
        {
            return POSIXPlatform.IsConsoleOutputRedirectedToFileImplementation();
        }

        public override bool IsElevated()
        {
            return POSIXPlatform.IsElevatedImplementation();
        }

        public override bool TryKillProcessTree(int processId, out int exitCode, out string error)
        {
            ProcessResult result = ProcessHelper.Run("pkill", $"-P {processId}");
            error = result.Errors;
            exitCode = result.ExitCode;
            return result.ExitCode == 0;
        }

        [DllImport("libc", EntryPoint = "getuid", SetLastError = true)]
        private static extern uint Getuid();

        [DllImport("libc", EntryPoint = "setsid", SetLastError = true)]
        private static extern int SetSid();

        [DllImport("libc", EntryPoint = "open", SetLastError = true)]
        private static extern int Open(string path, int flag);

        [DllImport("libc", EntryPoint = "close", SetLastError = true)]
        private static extern int Close(int filedes);

        [DllImport("libc", EntryPoint = "dup2", SetLastError = true)]
        private static extern int Dup2(int oldfd, int newfd);

        public abstract class POSIXPlatformConstants : ScalarPlatformConstants
        {
            public override string ExecutableExtension
            {
                get { return string.Empty; }
            }

            public override string ScalarExecutableName
            {
                get { return "scalar"; }
            }

            public override string ProgramLocaterCommand
            {
                get { return "which"; }
            }

            public override HashSet<string> UpgradeBlockingProcesses
            {
                get { return new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "git", "wish" }; }
            }

            public override bool SupportsUpgradeWhileRunning => true;
        }

        protected class EnvironmentVariableBasePath
        {
            private string environmentVariable;
            private string subFolder;

            public EnvironmentVariableBasePath (string environmentVariable, string subFolder)
            {
                this.environmentVariable = environmentVariable;
                this.subFolder = subFolder;
            }

            public string EnvironmentVariable
            {
                get { return environmentVariable; }
            }

            public string SubFolder
            {
                get { return subFolder; }
            }
        }

        protected static bool TryGetEnvironmentVariableBasePath(EnvironmentVariableBasePath[] environmentVariableBasePaths, out string path, out string error)
        {
            if (environmentVariableBasePaths == null || environmentVariableBasePaths.Length == 0)
            {
                path = null;
                error = "Null or empty list of base path environment variables to read";
                return false;
            }

            error = null;
            foreach (EnvironmentVariableBasePath environmentVariableBasePath in environmentVariableBasePaths)
            {
                if (TryGetEnvironmentVariable(environmentVariableBasePath.EnvironmentVariable, out path, out error))
                {
                    try
                    {
                        path = Path.Combine(path, environmentVariableBasePath.SubFolder);
                        return true;
                    }
                    catch (ArgumentException e)
                    {
                        error = $"Failed to build base path using ${environmentVariableBasePath.EnvironmentVariable}('{path}'), '{environmentVariableBasePath.SubFolder}': {e.Message}";
                    }
                }
            }

            path = null;
            return false;
        }

        private static bool TryGetEnvironmentVariable(string name, out string val, out string error)
        {
            try
            {
                val = Environment.GetEnvironmentVariable(name);
            }
            catch (SecurityException e)
            {
                val = null;
                error = $"Failed to read ${name}, insufficient permission: {e.Message}";
                return false;
            }

            if (string.IsNullOrEmpty(val))
            {
                val = null;
                error = $"${name} empty or not found";
                return false;
            }

            error = null;
            return true;
        }
    }
}
