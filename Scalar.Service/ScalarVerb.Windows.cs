using Scalar.Common;
using Scalar.Common.Tracing;
using Scalar.Platform.Windows;

namespace Scalar.Service
{
    public class ScalarVerb : IScalarVerb
    {
        private readonly ITracer tracer;

        public ScalarVerb(ITracer tracer)
        {
            this.tracer = tracer;
        }

        public bool CallMaintenance(string task, string repoRoot, int sessionId)
        {
            using (CurrentUser currentUser = new CurrentUser(this.tracer, sessionId))
            {
                if (!this.CallScalarMaintenance(task, repoRoot, currentUser))
                {
                    this.tracer.RelatedError($"{nameof(this.CallMaintenance)}: Unable to start the Scalar.exe process.");
                    return false;
                }
            }

            return true;
        }

        private bool CallScalarMaintenance(string task, string repoRoot, CurrentUser currentUser)
        {
            InternalVerbParameters internalParams = new InternalVerbParameters(startedByService: true);
            return currentUser.RunAs(
                Configuration.Instance.ScalarLocation,
                $"maintenance \"{repoRoot}\" --{task} --{ScalarConstants.VerbParameters.InternalUseOnly} {internalParams.ToJson()}");
        }
    }
}
