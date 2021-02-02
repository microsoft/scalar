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

        /// <summary>
        /// Supports the 'maintenance' builtin, including:
        ///
        /// * Tasks: gc, commit-graph, loose-objects, prefetch, incremental-repack
        /// * Subcommands: run, register, unregister, start, stop
        /// </summary>
        MaintenanceBuiltin = 1 << 1,

        /// <summary>
        /// Supports the builtin FS Monitor
        /// </summary>
        BuiltinFSMonitor = 1 << 2,
    }
}
