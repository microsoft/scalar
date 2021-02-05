using System;
using System.Collections.Generic;
using System.Text;
using Scalar.Common.Tracing;

namespace Scalar.Common.Git
{
    public class GitVersion
    {
        public GitVersion(int major, int minor, int build, string platform = null, int revision = 0, int minorRevision = 0, int? rc = null, string extra = null)
        {
            this.Major = major;
            this.Minor = minor;
            this.Build = build;
            this.ReleaseCandidate = rc;
            this.Platform = platform;
            this.Revision = revision;
            this.MinorRevision = minorRevision;
            this.Extra = extra;
            this.Features = new HashSet<string>();
        }

        public int Major { get; private set; }
        public int Minor { get; private set; }
        public int Build { get; private set; }
        public int? ReleaseCandidate { get; private set; }
        public string Platform { get; private set; }
        public int Revision { get; private set; }
        public int MinorRevision { get; private set; }
        public string Extra { get; private set; }
        public HashSet<string> Features { get; private set; }

        /// <summary>
        /// Determine the set of Git features that are supported in this version of Git.
        /// </summary>
        /// <returns>Set of Git features.</returns>
        public GitFeatureFlags GetFeatures()
        {
            var flags = GitFeatureFlags.None;

            if (StringComparer.OrdinalIgnoreCase.Equals(Platform, "vfs"))
            {
                flags |= GitFeatureFlags.GvfsProtocol;
            }

            if ((flags & GitFeatureFlags.GvfsProtocol) != 0 &&
                this.Minor > 28 || (this.Minor == 28 && this.Revision > 0))
            {
                flags |= GitFeatureFlags.MaintenanceBuiltin;
            }

            if (this.Features.Contains("fsmonitor--daemon"))
            {
                flags |= GitFeatureFlags.BuiltinFSMonitor;
            }

            return flags;
        }

        public static bool TryParseGitVersionCommandResult(string input, out GitVersion version)
        {
            // git version output is of the form
            // git version 2.17.0.scalar.1.preview.3

            const string GitVersionExpectedPrefix = "git version ";

            if (input.StartsWith(GitVersionExpectedPrefix))
            {
                input = input.Substring(GitVersionExpectedPrefix.Length);
            }

            return TryParseVersion(input, out version);
        }

        public static bool TryParseInstallerName(string input, string installerExtension, out GitVersion version)
        {
            // Installer name is of the form
            // Git-2.14.1.scalar.1.1.gb16030b-64-bit.exe

            version = null;

            if (!input.StartsWith("Git-", StringComparison.InvariantCultureIgnoreCase))
            {
                return false;
            }

            if (!input.EndsWith("-64-bit" + installerExtension, StringComparison.InvariantCultureIgnoreCase))
            {
                return false;
            }

            return TryParseVersion(input.Substring(4, input.Length - 15), out version);
        }

        public static bool TryParseVersion(string input, out GitVersion version)
        {
            version = null;

            int major, minor, build, revision = 0, minorRevision = 0;
            int? rc = null;
            string platform = null;
            string extra = null;

            if (string.IsNullOrWhiteSpace(input))
            {
                return false;
            }

            string[] parsedComponents = input.Split('.');
            int numComponents = parsedComponents.Length;

            // We minimally accept the official Git version number format which
            // consists of three components: "major.minor.build" or "major.minor.build-rc<N>".
            //
            // The other supported formats are the Git for Windows and Microsoft Git
            // formats which look like: "major.minor.build.platform.revision.minorRevision"
            // or "major.minor.build-rc<N>.platform.revision.minorRevision".
            //      0     1     2            3        4        5
            // len  1     2     3            4        5        6
            //
            if (numComponents < 3)
            {
                return false;
            }

            // Major version
            if (!TryParseComponent(parsedComponents[0], out major))
            {
                return false;
            }

            // Minor version
            if (!TryParseComponent(parsedComponents[1], out minor))
            {
                return false;
            }

            // Check if this is a release candidate version and if so split
            // it from the build number.
            string[] buildParts = parsedComponents[2].Split("-rc", StringSplitOptions.RemoveEmptyEntries);
            if (buildParts.Length > 1 && TryParseComponent(buildParts[1], out int rcInt))
            {
                rc = rcInt;
            }

            // Build number
            if (!TryParseComponent(buildParts[0], out build))
            {
                return false;
            }

            // Take the platform component verbatim
            if (numComponents >= 4)
            {
                platform = parsedComponents[3];
            }

            // Platform revision
            if (numComponents < 5 || !TryParseComponent(parsedComponents[4], out revision))
            {
                revision = 0;
            }

            // Minor platform revision
            if (numComponents < 6 || !TryParseComponent(parsedComponents[5], out minorRevision))
            {
                minorRevision = 0;
            }

            if (numComponents > 6) {
                extra = parsedComponents[6].Trim();
            }

            version = new GitVersion(major, minor, build, platform, revision, minorRevision, rc, extra);
            return true;
        }

        public static GitFeatureFlags GetAvailableGitFeatures(ITracer tracer)
        {
            // Determine what features of Git we have available to guide how we init/clone the repository
            var gitFeatures = GitFeatureFlags.None;
            string gitBinPath = ScalarPlatform.Instance.GitInstallation.GetInstalledGitBinPath();
            tracer?.RelatedInfo("Attempting to determine Git version for installation '{0}'", gitBinPath);
            if (GitProcess.TryGetVersion(gitBinPath, out var gitVersion, out string gitVersionError))
            {
                tracer?.RelatedInfo("Git installation '{0}' has version '{1}", gitBinPath, gitVersion);
                gitFeatures = gitVersion.GetFeatures();
            }
            else
            {
                tracer?.RelatedWarning("Unable to detect Git features for installation '{0}'. Failed to get Git version: '{1}", gitBinPath, gitVersionError);
            }

            return gitFeatures;
        }

        public bool IsEqualTo(GitVersion other)
        {
            if (this.Platform != other.Platform)
            {
                return false;
            }

            return this.CompareVersionNumbers(other) == 0;
        }

        public bool IsLessThan(GitVersion other)
        {
            return this.CompareVersionNumbers(other) < 0;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();

            sb.AppendFormat("{0}.{1}.{2}", this.Major, this.Minor, this.Build);

            if (this.ReleaseCandidate.HasValue)
            {
                sb.AppendFormat("-rc{0}", this.ReleaseCandidate.Value);
            }

            if (!string.IsNullOrWhiteSpace(this.Platform))
            {
                sb.AppendFormat(".{0}.{1}.{2}", this.Platform, this.Revision, this.MinorRevision);
            }

            if (this.Extra != null) {
                sb.Append($".{this.Extra}");
            }

            if (this.Features.Count > 0)
            {
                sb.Append(" (");
                foreach (string feature in this.Features)
                {
                    sb.Append($" {feature} ");
                }
                sb.Append(")");
            }

            return sb.ToString();
        }

        private static bool TryParseComponent(string component, out int parsedComponent)
        {
            if (!int.TryParse(component, out parsedComponent))
            {
                return false;
            }

            if (parsedComponent < 0)
            {
                return false;
            }

            return true;
        }

        private int CompareVersionNumbers(GitVersion other)
        {
            if (other == null)
            {
                return -1;
            }

            if (this.Major != other.Major)
            {
                return this.Major.CompareTo(other.Major);
            }

            if (this.Minor != other.Minor)
            {
                return this.Minor.CompareTo(other.Minor);
            }

            if (this.Build != other.Build)
            {
                return this.Build.CompareTo(other.Build);
            }

            if (this.Revision != other.Revision)
            {
                return this.Revision.CompareTo(other.Revision);
            }

            if (this.MinorRevision != other.MinorRevision)
            {
                return this.MinorRevision.CompareTo(other.MinorRevision);
            }

            return 0;
        }
    }
}
