using Scalar.Common.Tracing;
using System;
using System.Security;

namespace Scalar.Common
{
    public partial class ScalarEnlistment
    {
        public static bool IsUnattended(ITracer tracer)
        {
            try
            {
                return Environment.GetEnvironmentVariable(ScalarConstants.UnattendedEnvironmentVariable) == "1";
            }
            catch (SecurityException e)
            {
                if (tracer != null)
                {
                    EventMetadata metadata = new EventMetadata();
                    metadata.Add("Area", nameof(ScalarEnlistment));
                    metadata.Add("Exception", e.ToString());
                    tracer.RelatedError(metadata, "Unable to read environment variable " + ScalarConstants.UnattendedEnvironmentVariable);
                }

                return false;
            }
        }
    }
}
