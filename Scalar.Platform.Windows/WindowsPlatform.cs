using Microsoft.Win32;
using Scalar.Common;
using Scalar.Common.FileSystem;
using Scalar.Common.Git;
using Scalar.Common.Tracing;
using Scalar.Platform.Windows.DiskLayoutUpgrades;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Management.Automation;
using System.Security.AccessControl;
using System.Security.Principal;
using System.ServiceProcess;
using System.Text;

namespace Scalar.Platform.Windows
{
    public partial class WindowsPlatform : ScalarPlatform
    {
        private const string WindowsVersionRegistryKey = "SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion";
        private const string BuildLabRegistryValue = "BuildLab";
        private const string BuildLabExRegistryValue = "BuildLabEx";

        public WindowsPlatform() : base(underConstruction: new UnderConstructionFlags())
        {
        }

        public override IGitInstallation GitInstallation { get; } = new WindowsGitInstallation();
        public override IDiskLayoutUpgradeData DiskLayoutUpgrade { get; } = new WindowsDiskLayoutUpgradeData();
        public override IPlatformFileSystem FileSystem { get; } = new WindowsFileSystem();
        public override string Name { get => "Windows"; }
        public override ScalarPlatformConstants Constants { get; } = new WindowsPlatformConstants();

        public override string ScalarConfigPath
        {
            get
            {
                string servicePath = ScalarPlatform.Instance.GetDataRootForScalarComponent(ScalarConstants.Service.ServiceName);
                string scalarDirectory = Path.GetDirectoryName(servicePath);

                return Path.Combine(scalarDirectory, LocalScalarConfig.FileName);
            }
        }

        public static string GetStringFromRegistry(string key, string valueName)
        {
            object value = GetValueFromRegistry(RegistryHive.LocalMachine, key, valueName);
            return value as string;
        }

        public static object GetValueFromRegistry(RegistryHive registryHive, string key, string valueName)
        {
            object value = GetValueFromRegistry(registryHive, key, valueName, RegistryView.Registry64);
            if (value == null)
            {
                value = GetValueFromRegistry(registryHive, key, valueName, RegistryView.Registry32);
            }

            return value;
        }

        public static bool TrySetDWordInRegistry(RegistryHive registryHive, string key, string valueName, uint value)
        {
            RegistryKey localKey = RegistryKey.OpenBaseKey(registryHive, RegistryView.Registry64);
            RegistryKey localKeySub = localKey.OpenSubKey(key, writable: true);

            if (localKeySub == null)
            {
                localKey = RegistryKey.OpenBaseKey(registryHive, RegistryView.Registry32);
                localKeySub = localKey.OpenSubKey(key, writable: true);
            }

            if (localKeySub == null)
            {
                return false;
            }

            localKeySub.SetValue(valueName, value, RegistryValueKind.DWord);
            return true;
        }

        public override void InitializeEnlistmentACLs(string enlistmentPath)
        {
            // The following permissions are typically present on deskop and missing on Server
            //
            //   ACCESS_ALLOWED_ACE_TYPE: NT AUTHORITY\Authenticated Users
            //          [OBJECT_INHERIT_ACE]
            //          [CONTAINER_INHERIT_ACE]
            //          [INHERIT_ONLY_ACE]
            //        DELETE
            //        GENERIC_EXECUTE
            //        GENERIC_WRITE
            //        GENERIC_READ
            DirectorySecurity rootSecurity = Directory.GetAccessControl(enlistmentPath);
            AccessRule authenticatedUsersAccessRule = rootSecurity.AccessRuleFactory(
                new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null),
                unchecked((int)(NativeMethods.FileAccess.DELETE | NativeMethods.FileAccess.GENERIC_EXECUTE | NativeMethods.FileAccess.GENERIC_WRITE | NativeMethods.FileAccess.GENERIC_READ)),
                true,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Allow);

            // The return type of the AccessRuleFactory method is the base class, AccessRule, but the return value can be cast safely to the derived class.
            // https://msdn.microsoft.com/en-us/library/system.security.accesscontrol.filesystemsecurity.accessrulefactory(v=vs.110).aspx
            rootSecurity.AddAccessRule((FileSystemAccessRule)authenticatedUsersAccessRule);
            Directory.SetAccessControl(enlistmentPath, rootSecurity);
        }

        public override string GetOSVersionInformation()
        {
            StringBuilder sb = new StringBuilder();
            try
            {
                string buildLabVersion = GetStringFromRegistry(WindowsVersionRegistryKey, BuildLabRegistryValue);
                sb.AppendFormat($"Windows BuildLab version {buildLabVersion}");
                sb.AppendLine();

                string buildLabExVersion = GetStringFromRegistry(WindowsVersionRegistryKey, BuildLabExRegistryValue);
                sb.AppendFormat($"Windows BuildLabEx version {buildLabExVersion}");
                sb.AppendLine();
            }
            catch (Exception e)
            {
                sb.AppendFormat($"Failed to record Windows version information. Exception: {e}");
            }

            return sb.ToString();
        }

        public override string GetDataRootForScalar()
        {
            return WindowsPlatform.GetDataRootForScalarImplementation();
        }

        public override string GetDataRootForScalarComponent(string componentName)
        {
            return WindowsPlatform.GetDataRootForScalarComponentImplementation(componentName);
        }

        public override void StartBackgroundScalarProcess(ITracer tracer, string programName, string[] args)
        {
            string programArguments = string.Empty;
            try
            {
                programArguments = string.Join(" ", args.Select(arg => arg.Contains(' ') ? "\"" + arg + "\"" : arg));
                ProcessStartInfo processInfo = new ProcessStartInfo(programName, programArguments);
                processInfo.WindowStyle = ProcessWindowStyle.Hidden;

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
            // No additional work required
        }

        public override NamedPipeServerStream CreatePipeByName(string pipeName)
        {
            PipeSecurity security = new PipeSecurity();
            security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null), PipeAccessRights.ReadWrite | PipeAccessRights.CreateNewInstance, AccessControlType.Allow));
            security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.CreatorOwnerSid, null), PipeAccessRights.FullControl, AccessControlType.Allow));
            security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null), PipeAccessRights.FullControl, AccessControlType.Allow));

            NamedPipeServerStream pipe = new NamedPipeServerStream(
                pipeName,
                PipeDirection.InOut,
                NamedPipeServerStream.MaxAllowedServerInstances,
                PipeTransmissionMode.Byte,
                PipeOptions.WriteThrough | PipeOptions.Asynchronous,
                0, // default inBufferSize
                0, // default outBufferSize
                security,
                HandleInheritability.None);

            return pipe;
        }

        public override bool IsElevated()
        {
            return WindowsPlatform.IsElevatedImplementation();
        }

        public override bool IsProcessActive(int processId)
        {
            return WindowsPlatform.IsProcessActiveImplementation(processId, tryGetProcessById: true);
        }

        public override void IsServiceInstalledAndRunning(string name, out bool installed, out bool running)
        {
            ServiceController service = ServiceController.GetServices().FirstOrDefault(s => s.ServiceName.Equals(name, StringComparison.Ordinal));

            installed = service != null;
            running = service != null ? service.Status == ServiceControllerStatus.Running : false;
        }

        public override string GetNamedPipeName(string enlistmentRoot)
        {
            return WindowsPlatform.GetNamedPipeNameImplementation(enlistmentRoot);
        }

        public override string GetScalarServiceNamedPipeName(string serviceName)
        {
            return serviceName + ".pipe";
        }

        public override void ConfigureVisualStudio(string gitBinPath, ITracer tracer)
        {
            try
            {
                const string GitBinPathEnd = "\\cmd\\git.exe";
                string[] gitVSRegistryKeyNames =
                {
                    "HKEY_CURRENT_USER\\Software\\Microsoft\\VSCommon\\15.0\\TeamFoundation\\GitSourceControl",
                    "HKEY_CURRENT_USER\\Software\\Microsoft\\VSCommon\\16.0\\TeamFoundation\\GitSourceControl"
                };
                const string GitVSRegistryValueName = "GitPath";

                if (!gitBinPath.EndsWith(GitBinPathEnd))
                {
                    tracer.RelatedWarning(
                        "Unable to configure Visual Studio’s GitSourceControl regkey because invalid git.exe path found: " + gitBinPath,
                        Keywords.Telemetry);

                    return;
                }

                string regKeyValue = gitBinPath.Substring(0, gitBinPath.Length - GitBinPathEnd.Length);
                foreach (string registryKeyName in gitVSRegistryKeyNames)
                {
                    Registry.SetValue(registryKeyName, GitVSRegistryValueName, regKeyValue);
                }
            }
            catch (Exception ex)
            {
                EventMetadata metadata = new EventMetadata();
                metadata.Add("Operation", nameof(this.ConfigureVisualStudio));
                metadata.Add("Exception", ex.ToString());
                tracer.RelatedWarning(metadata, "Error while trying to set Visual Studio’s GitSourceControl regkey");
            }
        }

        public override bool TryVerifyAuthenticodeSignature(string path, out string subject, out string issuer, out string error)
        {
            using (PowerShell powershell = PowerShell.Create())
            {
                powershell.AddScript($"Get-AuthenticodeSignature -FilePath {path}");

                Collection<PSObject> results = powershell.Invoke();
                if (powershell.HadErrors || results.Count <= 0)
                {
                    subject = null;
                    issuer = null;
                    error = $"Powershell Get-AuthenticodeSignature failed, could not verify authenticode for {path}.";
                    return false;
                }

                Signature signature = results[0].BaseObject as Signature;
                bool isValid = signature.Status == SignatureStatus.Valid;
                subject = signature.SignerCertificate.SubjectName.Name;
                issuer = signature.SignerCertificate.IssuerName.Name;
                error = isValid == false ? signature.StatusMessage : null;
                return isValid;
            }
        }

        public override string GetCurrentUser()
        {
            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(identity);
            return identity.User.Value;
        }

        public override string GetUserIdFromLoginSessionId(int sessionId, ITracer tracer)
        {
            using (CurrentUser currentUser = new CurrentUser(tracer, sessionId))
            {
                return currentUser.Identity.User.Value;
            }
        }

        public override string GetUpgradeLogDirectoryParentDirectory()
        {
            return this.GetUpgradeProtectedDataDirectory();
        }

        public override string GetUpgradeHighestAvailableVersionDirectory()
        {
            return this.GetUpgradeProtectedDataDirectory();
        }

        public override string GetUpgradeProtectedDataDirectory()
        {
            return GetUpgradeProtectedDataDirectoryImplementation();
        }

        public override Dictionary<string, string> GetPhysicalDiskInfo(string path, bool sizeStatsOnly) => WindowsPhysicalDiskInfo.GetPhysicalDiskInfo(path, sizeStatsOnly);

        public override bool IsConsoleOutputRedirectedToFile()
        {
            return WindowsPlatform.IsConsoleOutputRedirectedToFileImplementation();
        }

        public override FileBasedLock CreateFileBasedLock(
            PhysicalFileSystem fileSystem,
            ITracer tracer,
            string lockPath)
        {
            return new WindowsFileBasedLock(fileSystem, tracer, lockPath);
        }

        public override ProductUpgraderPlatformStrategy CreateProductUpgraderPlatformInteractions(
            PhysicalFileSystem fileSystem,
            ITracer tracer)
        {
            return new WindowsProductUpgraderPlatformStrategy(fileSystem, tracer);
        }

        public override bool TryGetScalarEnlistmentRoot(string directory, out string enlistmentRoot, out string errorMessage)
        {
            return WindowsPlatform.TryGetScalarEnlistmentRootImplementation(directory, out enlistmentRoot, out errorMessage);
        }

        public override bool TryGetDefaultLocalCacheRoot(string enlistmentRoot, out string localCacheRoot, out string localCacheRootError)
        {
            string pathRoot;

            try
            {
                pathRoot = Path.GetPathRoot(enlistmentRoot);
            }
            catch (ArgumentException e)
            {
                localCacheRoot = null;
                localCacheRootError = $"Failed to determine the root of '{enlistmentRoot}'): {e.Message}";
                return false;
            }

            if (string.IsNullOrEmpty(pathRoot))
            {
                localCacheRoot = null;
                localCacheRootError = $"Failed to determine the root of '{enlistmentRoot}', path does not contain root directory information";
                return false;
            }

            try
            {
                localCacheRoot = Path.Combine(pathRoot, ScalarConstants.DefaultScalarCacheFolderName);
                localCacheRootError = null;
                return true;
            }
            catch (ArgumentException e)
            {
                localCacheRoot = null;
                localCacheRootError = $"Failed to build local cache path using root directory '{pathRoot}'): {e.Message}";
                return false;
            }
        }

        public override bool TryKillProcessTree(int processId, out int exitCode, out string error)
        {
            ProcessResult result = ProcessHelper.Run("taskkill", $"/pid {processId} /f /t");
            error = result.Errors;
            exitCode = result.ExitCode;
            return result.ExitCode == 0;
        }

        private static object GetValueFromRegistry(RegistryHive registryHive, string key, string valueName, RegistryView view)
        {
            RegistryKey localKey = RegistryKey.OpenBaseKey(registryHive, view);
            RegistryKey localKeySub = localKey.OpenSubKey(key);

            object value = localKeySub == null ? null : localKeySub.GetValue(valueName);
            return value;
        }

        public class WindowsPlatformConstants : ScalarPlatformConstants
        {
            public override string ExecutableExtension
            {
                get { return ".exe"; }
            }

            public override string InstallerExtension
            {
                get { return ".exe"; }
            }

            public override bool SupportsUpgradeWhileRunning => false;

            public override string WorkingDirectoryBackingRootPath
            {
                get { return ScalarConstants.WorkingDirectoryRootName; }
            }

            public override string DotScalarRoot
            {
                get { return WindowsPlatform.DotScalarRoot; }
            }

            public override string ScalarBinDirectoryPath
            {
                get
                {
                    return Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                        this.ScalarBinDirectoryName);
                }
            }

            public override string ScalarBinDirectoryName
            {
                get { return "Scalar"; }
            }

            public override string ScalarExecutableName
            {
                get { return "Scalar" + this.ExecutableExtension; }
            }

            public override string ProgramLocaterCommand
            {
                get { return "where"; }
            }

            public override HashSet<string> UpgradeBlockingProcesses
            {
                get { return new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Scalar", "Scalar.Mount", "git", "ssh-agent", "wish", "bash" }; }
            }

            // Tests show that 250 is the max supported pipe name length
            public override int MaxPipePathLength => 250;
        }
    }
}
