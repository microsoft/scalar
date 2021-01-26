using CommandLine;
using Scalar.Common;
using Scalar.Common.FileSystem;
using Scalar.Common.Maintenance;
using Scalar.Common.Tracing;
using System.IO;

namespace Scalar.CommandLine
{
    [Verb(RegisterVerb.RegisterVerbName, HelpText = "Register a repo with Scalar")]
    public class RegisterVerb : ScalarVerb
    {
        private const string RegisterVerbName = "register";

        protected override string VerbName => RegisterVerb.RegisterVerbName;

        [Value(
            1,
            Required = false,
            Default = null,
            MetaName = "Enlistment Root Path",
            HelpText = "Full or relative path to the enlistment root. Uses current directory if not provided.")]
        public override string EnlistmentRootPathParameter { get; set; }

        public override void Execute()
        {
            PhysicalFileSystem fileSystem = new PhysicalFileSystem();

            this.ValidatePathParameter(this.EnlistmentRootPathParameter);

            ScalarEnlistment enlistment = this.CreateEnlistment(this.EnlistmentRootPathParameter ?? Directory.GetCurrentDirectory(), null);

            using (JsonTracer tracer = new JsonTracer(ScalarConstants.ScalarEtwProviderName, RegisterVerb.RegisterVerbName))
            {
                if (this.TryRegisterRepo(tracer, enlistment, fileSystem, out string error))
                {
                    this.Output.WriteLine($"Successfully registered repo at '{enlistment.EnlistmentRoot}'");
                }
                else
                {
                    string message = $"Failed to register repo: {error}";
                    tracer.RelatedError(message);
                    this.ReportErrorAndExit(message);
                    return;
                }

                ScalarContext context = new ScalarContext(tracer, fileSystem, enlistment);
                new ConfigStep(context).Execute();
            }
        }
    }
}
