using NuGet.Commands;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Packaging.Core;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using Scalar.Common.Tracing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Scalar.Common.FileSystem;

namespace Scalar.Common.NuGetUpgrade
{
    /// <summary>
    /// Handles interactions with a NuGet Feed.
    /// </summary>
    public class NuGetFeed : IDisposable
    {
        // This is the SHA256 Certificate Thumbrint we expect packages from Microsoft to be signed with
        private const string TrustedMicrosoftCertFingerprint = "3F9001EA83C560D712C24CF213C3D312CB3BFF51EE89435D3430BD06B5D0EECE";

        // These are the expected signer and Authenticode certificate issuer for the NuGet CLI
        private const string NuGetToolSigner = "Microsoft Corporation";
        private const string NuGetToolCertIssuer = "Microsoft Code Signing PCA";

        private readonly ITracer tracer;
        private readonly string feedUrl;
        private readonly string feedName;
        private readonly string downloadFolder;
        private readonly bool platformSupportsEncryption;
        private readonly PhysicalFileSystem fileSystem;

        private SourceRepository sourceRepository;
        private string personalAccessToken;
        private SourceCacheContext sourceCacheContext;
        private ILogger nuGetLogger;

        public NuGetFeed(
            string feedUrl,
            string feedName,
            string downloadFolder,
            string personalAccessToken,
            bool platformSupportsEncryption,
            ITracer tracer,
            PhysicalFileSystem fileSystem)
        {
            this.feedUrl = feedUrl;
            this.feedName = feedName;
            this.downloadFolder = downloadFolder;
            this.personalAccessToken = personalAccessToken;
            this.tracer = tracer;
            this.fileSystem = fileSystem;

            // Configure the NuGet SourceCacheContext -
            // - Direct download packages - do not download to global
            //   NuGet cache. This is set in  NullSourceCacheContext.Instance
            // - NoCache - Do not cache package version lists
            this.sourceCacheContext = NullSourceCacheContext.Instance.Clone();
            this.sourceCacheContext.NoCache = true;
            this.platformSupportsEncryption = platformSupportsEncryption;

            this.nuGetLogger = new Logger(this.tracer);
            this.SetSourceRepository();
        }

        public void Dispose()
        {
            this.sourceRepository = null;
            this.sourceCacheContext?.Dispose();
            this.sourceCacheContext = null;
        }

        public virtual void SetCredentials(string credential)
        {
            this.personalAccessToken = credential;

            this.SetSourceRepository();
        }

        /// <summary>
        /// Query a NuGet feed for list of packages that match the packageId.
        /// </summary>
        /// <param name="packageId"></param>
        /// <returns>List of packages that match query parameters</returns>
        public virtual async Task<IList<IPackageSearchMetadata>> QueryFeedAsync(string packageId)
        {
            PackageMetadataResource packageMetadataResource = await this.sourceRepository.GetResourceAsync<PackageMetadataResource>();
            IEnumerable<IPackageSearchMetadata> queryResults = await packageMetadataResource.GetMetadataAsync(
                packageId,
                includePrerelease: false,
                includeUnlisted: false,
                sourceCacheContext: this.sourceCacheContext,
                log: this.nuGetLogger,
                token: CancellationToken.None);

            return queryResults.ToList();
        }

        /// <summary>
        /// Download the specified packageId from the NuGet feed.
        /// </summary>
        /// <param name="packageId">PackageIdentity to download.</param>
        /// <returns>Path to the downloaded package.</returns>
        public virtual async Task<string> DownloadPackageAsync(PackageIdentity packageId)
        {
            string downloadPath = Path.Combine(this.downloadFolder, $"{this.feedName}.zip");
            PackageDownloadContext packageDownloadContext = new PackageDownloadContext(
                this.sourceCacheContext,
                this.downloadFolder,
                true);

            DownloadResource downloadResource = await this.sourceRepository.GetResourceAsync<DownloadResource>();

            using (DownloadResourceResult downloadResourceResult = await downloadResource.GetDownloadResourceResultAsync(
                       packageId,
                       packageDownloadContext,
                       globalPackagesFolder: string.Empty,
                       logger: this.nuGetLogger,
                       token: CancellationToken.None))
            {
                if (downloadResourceResult.Status != DownloadResourceResultStatus.Available)
                {
                    throw new Exception("Download of NuGet package failed. DownloadResult Status: {downloadResourceResult.Status}");
                }

                using (FileStream fileStream = File.Create(downloadPath))
                {
                    downloadResourceResult.PackageStream.CopyTo(fileStream);
                }
            }

            return downloadPath;
        }

        public virtual bool VerifyPackage(string packagePath)
        {
            // We cannot use VerifyCommandRunner::ExecuteCommandAsync(VerifyArgs) because this is not implement for .NET Core.
            // Instead we will shell out to the nuget.exe (Windows only) that we bundle with Scalar to do the checks for us.
            // We first locate the bundled nuget.exe and check that it is signed before delegating our trust checks to it.
            if (!ScalarPlatform.Instance.UnderConstruction.SupportsNuGetVerification)
            {
                this.tracer.RelatedError("Platform does not support NuGet package verification.");
                return false;
            }

            // Locate bundled nuget CLI tool
            string externalBinDir = ProcessHelper.GetBundledBinariesLocation();
            string nugetToolFileName = ScalarConstants.BundledBinaries.NuGetFileName + ScalarPlatform.Instance.Constants.ExecutableExtension;
            string nugetToolFilePath = Path.Combine(externalBinDir, nugetToolFileName);
            if (!this.fileSystem.FileExists(nugetToolFilePath))
            {
                this.tracer.RelatedError($"Cannot find NuGet CLI tool {nugetToolFilePath}. Scalar installation is broken; please reinstall Scalar.");
                return false;
            }

            // Open a file handle so that no one can replace the executable between the 'is signed' check and actually running it
            using (Stream stream = this.fileSystem.OpenFileStream(nugetToolFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, false))
            {
                // Check CLI tool is signed
                if (!ScalarPlatform.Instance.TryVerifyAuthenticodeSignature(nugetToolFilePath, out string subject, out string issuer, out string error))
                {
                    this.tracer.RelatedError($"NuGet CLI tool {nugetToolFilePath} is not signed. Error={error}");
                    return false;
                }

                if (!subject.StartsWith($"CN={NuGetToolSigner}, ") || !issuer.StartsWith($"CN={NuGetToolCertIssuer}"))
                {
                    this.tracer.RelatedError($"NuGet CLI tool {nugetToolFilePath} is signed by unknown signer. Signed by {subject}, issued by {issuer} expected signer is {NuGetToolSigner}, issuer {NuGetToolCertIssuer}.");
                    return false;
                }

                // Use the NuGet CLI to verify the package
                string verifyCommandArgs = $"verify -All \"{packagePath}\" -CertificateFingerprint {TrustedMicrosoftCertFingerprint}";

                ProcessResult result = ProcessHelper.Run(nugetToolFilePath, verifyCommandArgs, redirectOutput: true);
                if (result.ExitCode != 0)
                {
                    this.tracer.RelatedError($"NuGet package verification failed. ExitCode={result.ExitCode}.{Environment.NewLine}StdOut: {result.Output}{Environment.NewLine}StdError: {result.Errors}");
                    return false;
                }
            }

            return true;
        }

        protected static EventMetadata CreateEventMetadata(Exception e = null)
        {
            EventMetadata metadata = new EventMetadata();
            metadata.Add("Area", nameof(NuGetFeed));
            if (e != null)
            {
                metadata.Add("Exception", e.ToString());
            }

            return metadata;
        }

        private static PackageSourceCredential BuildCredentialsFromPAT(string personalAccessToken, bool storePasswordInClearText)
        {
            // The storePasswordInClearText property is used to control whether the password
            // is written to NuGet config files in clear text or not. It also controls whether the
            // password is stored encrypted in memory or not. The ability to encrypt / decrypt the password
            // is not supported in non-windows platforms at this point.
            // We do not actually write out config files or store the password (except in memory). As in our
            // usage of NuGet functionality we do not write out config files, it is OK to not set this property
            // (with the tradeoff being the password is not encrypted in memory, and we need to make sure that new code
            // does not start to write out config files).
            return PackageSourceCredential.FromUserInput(
                "ScalarNugetUpgrader",
                "PersonalAccessToken",
                personalAccessToken,
                storePasswordInClearText: storePasswordInClearText,
                validAuthenticationTypesText: null); // null means "all"
        }

        private void SetSourceRepository()
        {
            this.sourceRepository = Repository.Factory.GetCoreV3(this.feedUrl);
            if (!string.IsNullOrEmpty(this.personalAccessToken))
            {
                this.sourceRepository.PackageSource.Credentials = BuildCredentialsFromPAT(this.personalAccessToken, !this.platformSupportsEncryption);
            }
        }

        /// <summary>
        /// Implementation of logger used by NuGet library. It takes all output
        /// and redirects it to the Scalar logger.
        /// </summary>
        private class Logger : ILogger
        {
            private ITracer tracer;

            public Logger(ITracer tracer)
            {
                this.tracer = tracer;
            }

            public void Log(LogLevel level, string data)
            {
                string message = $"NuGet Logger: ({level}): {data}";
                switch (level)
                {
                    case LogLevel.Debug:
                    case LogLevel.Verbose:
                    case LogLevel.Minimal:
                    case LogLevel.Information:
                        this.tracer.RelatedInfo(message);
                        break;
                    case LogLevel.Warning:
                        this.tracer.RelatedWarning(message);
                        break;
                    case LogLevel.Error:
                        this.tracer.RelatedWarning(message);
                        break;
                    default:
                        this.tracer.RelatedWarning(message);
                        break;
                }
            }

            public void Log(ILogMessage message)
            {
                this.Log(message.Level, message.Message);
            }

            public Task LogAsync(LogLevel level, string data)
            {
                this.Log(level, data);
                return Task.CompletedTask;
            }

            public Task LogAsync(ILogMessage message)
            {
                this.Log(message);
                return Task.CompletedTask;
            }

            public void LogDebug(string data)
            {
                this.Log(LogLevel.Debug, data);
            }

            public void LogError(string data)
            {
                this.Log(LogLevel.Error, data);
            }

            public void LogInformation(string data)
            {
                this.Log(LogLevel.Information, data);
            }

            public void LogInformationSummary(string data)
            {
                this.Log(LogLevel.Information, data);
            }

            public void LogMinimal(string data)
            {
                this.Log(LogLevel.Minimal, data);
            }

            public void LogVerbose(string data)
            {
                this.Log(LogLevel.Verbose, data);
            }

            public void LogWarning(string data)
            {
                this.Log(LogLevel.Warning, data);
            }
        }
    }
}
