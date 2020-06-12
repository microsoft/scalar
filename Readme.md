# Scalar

[![Build Status](https://dev.azure.com/mseng/Scalar/_apis/build/status/microsoft.scalar?branchName=main)](https://dev.azure.com/mseng/Scalar/_build/latest?definitionId=9297&branchName=main)

## What is Scalar?

Scalar is a C# application that manages large Git repositories.

Run `scalar register` in an existing Git repo to enable recommended config
settings and start background maintenance.

If your repo is hosted on a service that supports the
[GVFS Protocol](https://github.com/microsoft/VFSForGit/blob/HEAD/Protocol.md),
such as Azure Repos, then `scalar clone <url>` will create a local enlistment with
abilities like on-demand object retrieval, background maintenance tasks, and
automatically sets Git config values and hooks that enable performance enhancements.
Scalar also assists in setting up sparse enlistments.

See [the documentation](docs/index.md) for more details.

## Table of Contents

* [Installing on macOS](#installing-on-macos)
* [Installing on Windows](#installing-on-windows)
* [Quick start](#quick-start)
* [License](#license)

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

## Quick start

If you want to get a feel for an initial workflow with a test project, here
are some commands that clone [our test repo](https://dev.azure.com/gvfs/ci/_git/ForTests)
and initialize some of the sparse content.

(Run these commands in Mac OSX Terminal or in Git Bash on Windows.)

```
$ scalar clone https://dev.azure.com/gvfs/ci/_git/ForTests
Clone parameters:
  Repo URL:     https://dev.azure.com/gvfs/ci/_git/ForTests
  Branch:       Default
  Cache Server: Default
  Local Cache:  C:\.scalarCache
  Destination:  C:\_git\ForTests
  FullClone:     False
Authenticating...Succeeded
Querying remote for config...Succeeded
Using cache server: None (https://dev.azure.com/gvfs/ci/_git/ForTests)
Cloning...Succeeded
Fetching commits and trees from origin (no cache server)...Succeeded
Configuring FSMonitor...Succeeded.
Validating repo...Succeeded

$ cd ForTests/src
$ ls
AuthoringTests.md  GvFlt_EULA.md  GVFS.sln  License.md  nuget.config  Protocol.md  Readme.md  Settings.StyleCop

$ git sparse-checkout set GVFS/GVFS.Common GVFS/GVFS.UnitTests GitHooksLoader
$ ls
AuthoringTests.md  GitHooksLoader/  GvFlt_EULA.md  GVFS/  GVFS.sln  License.md  nuget.config  Protocol.md  Readme.md  Settings.StyleCop

$ ls GVFS
GVFS.Common/  GVFS.UnitTests/  LibGit2Sharp.NativeBinaries.props  ProjectedFSLib.NativeBinaries.props

$ git sparse-checkout set GVFS/GVFS GVFS/GVFS.Common GVFS/GVFS.UnitTests GitHooksLoader
$ ls GVFS
GVFS/  GVFS.Common/  GVFS.UnitTests/  LibGit2Sharp.NativeBinaries.props  ProjectedFSLib.NativeBinaries.props
```

## License

The Scalar source code in this repo is available under the MIT license. See [License.md](License.md).

---

## Code of Conduct

This project has adopted the [Microsoft Open Source Code of Conduct][conduct-code].
For more information see the [Code of Conduct FAQ][conduct-FAQ] or contact [opencode@microsoft.com][conduct-email] with any additional questions or comments.

[conduct-code]: https://opensource.microsoft.com/codeofconduct/
[conduct-FAQ]: https://opensource.microsoft.com/codeofconduct/faq/
[conduct-email]: mailto:opencode@microsoft.com
