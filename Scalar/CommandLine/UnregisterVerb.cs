using CommandLine;
using Scalar.Common;
using System.IO;

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

            ScalarEnlistment enlistment = this.CreateEnlistment(this.EnlistmentRootPathParameter ?? Directory.GetCurrentDirectory(), null);

            this.UnregisterRepo(enlistment.EnlistmentRoot);
            this.Output.WriteLine($"Successfully unregistered repo at '{enlistment.EnlistmentRoot}'");
        }
    }
}
