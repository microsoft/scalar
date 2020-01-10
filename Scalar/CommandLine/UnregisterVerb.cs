using CommandLine;
using Scalar.Common;

namespace Scalar.CommandLine
{
    [Verb(UnregisterVerb.UnregisterVerbName, HelpText = "Track Unregister Unregistered with the Scalar service")]
    public class UnregisterVerb : ScalarVerb
    {
        private const string UnregisterVerbName = "unregister";

        protected override string VerbName => UnregisterVerb.UnregisterVerbName;

        [Value(
            1,
            Required = false,
            Default = null,
            MetaName = "Enlistment Root Path",
            HelpText = "Full or relative path to the Scalar enlistment root")]
        public override string EnlistmentRootPathParameter { get; set; }

        public override void Execute()
        {
            this.ValidatePathParameter(this.EnlistmentRootPathParameter);

            if (!ScalarPlatform.Instance.FileSystem.TryGetNormalizedPath(this.EnlistmentRootPathParameter, out string enlistmentRoot, out string error))
            {
                this.ReportErrorAndExit($"Error while finding normalized path for '{this.EnlistmentRootPathParameter}': {error}");
            }

            this.UnregisterRepo(this.EnlistmentRootPathParameter);
            this.Output.WriteLine($"Successfully unregistered repo at '{this.EnlistmentRootPathParameter}'");
        }
    }
}
