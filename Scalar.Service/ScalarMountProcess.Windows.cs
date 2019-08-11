using Scalar.Common;
using Scalar.Common.Tracing;
using Scalar.Platform.Windows;
using Scalar.Service.Handlers;

namespace Scalar.Service
{
    public class ScalarMountProcess : IRepoMounter
    {
        private readonly ITracer tracer;

        public ScalarMountProcess(ITracer tracer)
        {
            this.tracer = tracer;
        }

        public bool MountRepository(string repoRoot, int sessionId)
        {
            using (CurrentUser currentUser = new CurrentUser(this.tracer, sessionId))
            {
                if (!this.CallScalarMount(repoRoot, currentUser))
                {
                    this.tracer.RelatedError($"{nameof(this.MountRepository)}: Unable to start the Scalar.exe process.");
                    return false;
                }

                string errorMessage;
                if (!ScalarEnlistment.WaitUntilMounted(this.tracer, repoRoot, false, out errorMessage))
                {
                    this.tracer.RelatedError(errorMessage);
                    return false;
                }
            }

            return true;
        }

        private bool CallScalarMount(string repoRoot, CurrentUser currentUser)
        {
            InternalVerbParameters mountInternal = new InternalVerbParameters(startedByService: true);
            return currentUser.RunAs(
                Configuration.Instance.ScalarLocation,
                $"mount {repoRoot} --{ScalarConstants.VerbParameters.InternalUseOnly} {mountInternal.ToJson()}");
        }
    }
}
