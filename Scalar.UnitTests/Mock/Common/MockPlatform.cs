using Scalar.Common;
using Scalar.Common.FileSystem;
using Scalar.Common.Git;
using Scalar.Common.Tracing;
using Scalar.UnitTests.Mock.FileSystem;
using Scalar.UnitTests.Mock.Git;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;

namespace Scalar.UnitTests.Mock.Common
{
    public class MockPlatform : ScalarPlatform
    {
        public MockPlatform() : base(underConstruction: new UnderConstructionFlags())
        {
        }

        public string MockCurrentUser { get; set; }

        public override IGitInstallation GitInstallation { get; } = new MockGitInstallation();

        public override IPlatformFileSystem FileSystem { get; } = new MockPlatformFileSystem();

        public override string Name { get => "Mock"; }

        public override string ScalarConfigPath { get => Path.Combine("mock:", LocalScalarConfig.FileName); }

        public override ScalarPlatformConstants Constants { get; } = new MockPlatformConstants();

        public HashSet<int> ActiveProcesses { get; } = new HashSet<int>();

        public override void ConfigureVisualStudio(string gitBinPath, ITracer tracer)
        {
            throw new NotSupportedException();
        }

        public override bool TryVerifyAuthenticodeSignature(string path, out string subject, out string issuer, out string error)
        {
            throw new NotImplementedException();
        }

        public override string GetScalarServiceNamedPipeName(string serviceName)
        {
            return Path.Combine("Scalar_Mock_ServicePipeName", serviceName);
        }

        public override NamedPipeServerStream CreatePipeByName(string pipeName)
        {
            throw new NotSupportedException();
        }

        public override string GetCurrentUser()
        {
            return this.MockCurrentUser;
        }

        public override string GetUserIdFromLoginSessionId(int sessionId, ITracer tracer)
        {
            return sessionId.ToString();
        }

        public override string GetOSVersionInformation()
        {
            throw new NotSupportedException();
        }

        public override string GetCommonAppDataRootForScalar()
        {
            // TODO: Update this method to return non existant file path.
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "Scalar");
        }

        public override string GetSecureDataRootForScalar()
        {
            return this.GetCommonAppDataRootForScalar();
        }

        public override Dictionary<string, string> GetPhysicalDiskInfo(string path, bool sizeStatsOnly)
        {
            return new Dictionary<string, string>();
        }

        public override string GetUpgradeProtectedDataDirectory()
        {
            return this.GetCommonAppDataRootForScalarComponent(ProductUpgraderInfo.UpgradeDirectoryName);
        }

        public override string GetUpgradeLogDirectoryParentDirectory()
        {
            return this.GetUpgradeProtectedDataDirectory();
        }

        public override string GetUpgradeHighestAvailableVersionDirectory()
        {
            return this.GetUpgradeProtectedDataDirectory();
        }

        public override void InitializeEnlistmentACLs(string enlistmentPath)
        {
            throw new NotSupportedException();
        }

        public override bool IsElevated()
        {
            throw new NotSupportedException();
        }

        public override bool IsProcessActive(int processId)
        {
            return this.ActiveProcesses.Contains(processId);
        }

        public override void IsServiceInstalledAndRunning(string name, out bool installed, out bool running)
        {
            throw new NotSupportedException();
        }

        public override bool TryGetDefaultLocalCacheRoot(string enlistmentRoot, out string localCacheRoot, out string localCacheRootError)
        {
            throw new NotImplementedException();
        }

        public override void StartBackgroundScalarProcess(ITracer tracer, string programName, string[] args)
        {
            throw new NotSupportedException();
        }

        public override void PrepareProcessToRunInBackground()
        {
            throw new NotSupportedException();
        }

        public override FileBasedLock CreateFileBasedLock(PhysicalFileSystem fileSystem, ITracer tracer, string lockPath)
        {
            return new MockFileBasedLock(fileSystem, tracer, lockPath);
        }

        public override ProductUpgraderPlatformStrategy CreateProductUpgraderPlatformInteractions(
            PhysicalFileSystem fileSystem,
            ITracer tracer)
        {
            return new MockProductUpgraderPlatformStrategy(fileSystem, tracer);
        }

        public override bool TryKillProcessTree(int processId, out int exitCode, out string error)
        {
            error = null;
            exitCode = 0;
            return true;
        }

        public override string GetTemplateHooksDirectory()
        {
            throw new NotSupportedException();
        }

        public class MockPlatformConstants : ScalarPlatformConstants
        {
            public override string ExecutableExtension
            {
                get { return ".mockexe"; }
            }

            public override string InstallerExtension
            {
                get { return ".mockexe"; }
            }

            public override string ScalarBinDirectoryPath
            {
                get { return Path.Combine("MockProgramFiles", this.ScalarBinDirectoryName); }
            }

            public override string ScalarBinDirectoryName
            {
                get { return "MockScalar"; }
            }

            public override string ScalarExecutableName
            {
                get { return "MockScalar" + this.ExecutableExtension; }
            }

            public override string ProgramLocaterCommand
            {
                get { return "MockWhere"; }
            }

            public override HashSet<string> UpgradeBlockingProcesses
            {
                get { return new HashSet<string>(this.PathComparer) { "Scalar", "git", "wish", "bash" }; }
            }

            public override bool SupportsUpgradeWhileRunning => false;

            public override int MaxPipePathLength => 250;

            public override bool CaseSensitiveFileSystem => RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
        }
    }
}
