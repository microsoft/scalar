Scalar: Enabling Git at Scale
=============================

Scalar is a tool that helps Git scale to some of the largest Git repositories.
It achieves this by enabling some advanced Git features, such as:

* *Sparse-checkout:* limits the size of your working directory.

* *File system monitor:* tracks the recently modified files and eliminates
  the need for Git to scan the entire worktree.

* *Commit-graph:* accelerates commit walks and reachability calculations,
   speeding up commands like `git log`.

* *Multi-pack-index:* enables fast object lookups across many pack-files.

* *Incremental repack:* Repacks the packed Git data into fewer pack-file
  without disrupting concurrent commands by using the multi-pack-index.

Scalar clones also use the
[GVFS protocol](https://github.com/microsoft//VFSForGit/blob/master/Protocol.md)
to significantly reduce the amount of data required to get started
using a repository. By delaying all blob downloads until they are required,
Scalar allows you to work with very large repositories quickly. This protocol
allows a network of _cache servers_ to serve objects with lower latency and
higher throughput. The cache servers also reduce load on the central server.

Documentation
-------------

* [Frequently Asked Questions](faq.md)

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

