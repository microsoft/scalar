Scalar: Enabling Git at Scale
=============================

Scalar is a tool that helps Git scale to some of the largest Git repositories.
It achieves this by enabling some advanced Git features, such as:

* *Sparse Checkout:* limits the size of your working directory.

* *File System Monitor:* reduces the number of files Git scans to the set
  that have been modified recently.

* *Commit-Graph:* accelerates commit walks and reachability calculations.

* *Multi-pack-index:* enables fast object lookups across many pack-files and
  combines pack-files in the background without blocking user-facing commands.

Scalar clones also use the [GVFS](https://github.com/microsoft/vfsforgit)
protocol to significantly reduce the amount of data required to get started
using a repository. By delaying all blob downloads until they are required,
Scalar allows you to work with very large repositories quickly. This protocol
allows a network of _cache servers_ to serve objects with lower latency and
higher throughput.

Scalar Commands
---------------

* [`scalar clone`](commands/scalar-clone.md): Create a local enlistment of
  a remote repository.

* [`scalar upgrade`](commands/scalar-upgrade.md): Upgrade your version of
  Scalar to the latest available release.

* [`scalar diagnose`](commands/scalar-diagnose.md): Collect diagnostic data
  to assist troubleshooting.

* [`scalar maintenance`](commands/scalar-maintenance.md): Manually process
  Git data for efficiency. Normally run in the background by Scalar Service.

