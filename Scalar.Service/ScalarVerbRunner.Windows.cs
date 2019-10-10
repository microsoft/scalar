using Scalar.Common;
using Scalar.Common.Tracing;
using Scalar.Platform.Windows;

namespace Scalar.Service
{
    public class ScalarVerbRunner : IScalarVerbRunner
    {
        private readonly ITracer tracer;
        private readonly string internalVerbJson;

        public ScalarVerbRunner(ITracer tracer)
        {
            this.tracer = tracer;

            InternalVerbParameters internalParams = new InternalVerbParameters(startedByService: true);
            this.internalVerbJson = internalParams.ToJson();
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
            return currentUser.RunAs(
                Configuration.Instance.ScalarLocation,
                $"maintenance \"{repoRoot}\" --{ScalarConstants.VerbParameters.Maintenance.Task} {task} --{ScalarConstants.VerbParameters.InternalUseOnly} {this.internalVerbJson}");
        }
    }
}
