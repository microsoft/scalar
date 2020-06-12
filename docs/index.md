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

By running `scalar register` in any Git repo, Scalar will automatically enable
these features for that repo and start running suggested maintenance in the
background.

Repos cloned with the `scalar clone` command use the
[GVFS protocol](https://github.com/microsoft/VFSForGit/blob/HEAD/Protocol.md)
to significantly reduce the amount of data required to get started
using a repository. By delaying all blob downloads until they are required,
Scalar allows you to work with very large repositories quickly. This protocol
allows a network of _cache servers_ to serve objects with lower latency and
higher throughput. The cache servers also reduce load on the central server.

Installing on macOS
------------------

To install Scalar on macOS,
[download the `Installers_macOS_Release.zip` from the releases page](https://github.com/microsoft/scalar/releases).
Extract the `Installers_macOS_Release` folder, `cd` into it, and run `./InstallScalar.sh` in a Terminal window.
The script may prompt for your password as it installs the following components:

* [Git](https://github.com/microsoft/git) (with custom patches)
* [Git Credential Manager Core](https://github.com/microsoft/Git-Credential-Manager-Core)
* Scalar

Installing on Windows
--------------------

To install Scalar on Windows,
[download the `Installers_Windows_Release.zip` from the releases page](https://github.com/microsoft/scalar/releases).
Extract the `Installers_Windows_Release` folder, open it in a command prompt, and
run `InstallScalar.bat`. This will install the following components:

* [Git for Windows](https://github.com/microsoft/git) (with custom patches)
* Scalar

Documentation
-------------

* [Getting Started](getting-started.md): Get started with Scalar.
  Includes `scalar register`, `scalar unregister`, `scalar clone`, and
  `scalar delete`.

* [Advanced Features](advanced.md): 
  For expert users who want full control of Scalar's activity. Includes
  `scalar run <task>`, `scalar pause`, `scalar resume`.

* [Troubleshooting](troubleshooting.md):
  Collect diagnostic information or update custom settings. Includes
  `scalar diagnose`, `scalar config`, `scalar upgrade`, and `scalar cache-server`.

* [Frequently Asked Questions](faq.md)
