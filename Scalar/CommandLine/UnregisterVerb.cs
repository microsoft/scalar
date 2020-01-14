using CommandLine;
using Scalar.Common;
using System.IO;

namespace Scalar.CommandLine
{
    [Verb(UnregisterVerb.UnregisterVerbName, HelpText = "Unregister a repo with the Scalar service to stop background maintenance")]
    public class UnregisterVerb : ScalarVerb
    {
        private const string UnregisterVerbName = "unregister";

        protected override string VerbName => UnregisterVerb.UnregisterVerbName;

        [Value(
            1,
            Required = false,
            Default = null,
            MetaName = "Enlistment Root Path",
            HelpText = "Full or relative path to the enlistment root. Uses current directory if not provided.")]
        public override string EnlistmentRootPathParameter { get; set; }

        public override void Execute()
        {
            this.ValidatePathParameter(this.EnlistmentRootPathParameter);

            ScalarEnlistment enlistment = this.CreateEnlistment(this.EnlistmentRootPathParameter ?? Directory.GetCurrentDirectory(), null);

            this.UnregisterRepo(enlistment.EnlistmentRoot);
            this.Output.WriteLine($"Successfully unregistered repo at '{enlistment.EnlistmentRoot}'");
        }
    }
}
