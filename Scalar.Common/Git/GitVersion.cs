using System;
using System.Text;

namespace Scalar.Common.Git
{
    public class GitVersion
    {
        public GitVersion(int major, int minor, int build, string platform = null, int revision = 0, int minorRevision = 0)
        {
            this.Major = major;
            this.Minor = minor;
            this.Build = build;
            this.Platform = platform;
            this.Revision = revision;
            this.MinorRevision = minorRevision;
        }

        public int Major { get; private set; }
        public int Minor { get; private set; }
        public string Platform { get; private set; }
        public int Build { get; private set; }
        public int Revision { get; private set; }
        public int MinorRevision { get; private set; }

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

            int major, minor, build, revision, minorRevision;
            string platform = null;

            if (string.IsNullOrWhiteSpace(input))
            {
                return false;
            }

            string[] parsedComponents = input.Split('.');

            // We minimally accept the official Git version number format which
            // consists of three components: "major.minor.build".
            //
            // The other supported formats are the Git for Windows and Microsoft Git
            // formats which look like: "major.minor.build.platform.revision.minorRevision".
            //
            int numComponents = parsedComponents.Length;
            if (numComponents < 2)
            {
                return false;
            }

            if (!TryParseComponent(parsedComponents[0], out major))
            {
                return false;
            }

            if (!TryParseComponent(parsedComponents[1], out minor))
            {
                return false;
            }

            if (numComponents < 3 || !TryParseComponent(parsedComponents[2], out build))
            {
                return false;
            }

            if (numComponents >= 4)
            {
                platform = parsedComponents[3];
            }

            if (numComponents < 5 || !TryParseComponent(parsedComponents[4], out revision))
            {
                revision = 0;
            }

            if (numComponents < 6 || !TryParseComponent(parsedComponents[5], out minorRevision))
            {
                minorRevision = 0;
            }

            version = new GitVersion(major, minor, build, platform, revision, minorRevision);
            return true;
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

            if (!string.IsNullOrWhiteSpace(this.Platform))
            {
                sb.AppendFormat(".{0}.{1}.{2}", this.Platform, this.Revision, this.MinorRevision);
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
