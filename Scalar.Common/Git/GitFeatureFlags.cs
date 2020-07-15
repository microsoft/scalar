using System;

namespace Scalar.Common.Git
{
    /// <summary>
    /// Identifies a set of features that Git may support that we are interested in.
    /// </summary>
    [Flags]
    public enum GitFeatureFlags
    {
        None = 0,

        /// <summary>
        /// Support for the GVFS protocol.
        /// </summary>
        GvfsProtocol = 1 << 0,
    }
}
