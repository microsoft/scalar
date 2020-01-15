# Scalar

[![Build Status](https://dev.azure.com/mseng/Scalar/_apis/build/status/microsoft.scalar?branchName=master)](https://dev.azure.com/mseng/Scalar/_build/latest?definitionId=9297&branchName=master)

## What is Scalar?

Scalar is a C# application that manages large Git repositories. It adds abilities
like on-demand object retrieval, background maintenance tasks, and automatically
sets Git config values and hooks that enable performance enhancements. Scalar
also assists in setting up sparse enlistments.

### Table of Contents

* [Trying Scalar](#trying-scalar)
* [License](#license)

### Installing on macOS

To install Scalar on macOS, [download the .zip from the releases page](https://github.com/microsoft/scalar/releases).
Extract the `Scalar.Distribution` folder, `cd` into it, and run `./InstallScalar.sh` in a Terminal window.
The script may prompt for your password as it installs the following components:

* [Git](https://github.com/microsoft/git) (with custom patches)
* [Git Credential Manager Core](https://github.com/microsoft/Git-Credential-Manager-Core)
* Scalar

### Quick Start

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
Configuring Watchman...Succeeded.
Validating repo...Succeeded
Mounting...Succeeded
Registering for automount...Succeeded

$ cd ForTests/src
$ ls
AuthoringTests.md  GvFlt_EULA.md  GVFS.sln  License.md  nuget.config  Protocol.md  Readme.md  Settings.StyleCop

$ scalar sparse --add="GVFS/GVFS.Common;GVFS/GVFS.UnitTests;GitHooksLoader"
Authenticating...Succeeded
Querying remote for config...Succeeded
Configured cache server: None (https://dev.azure.com/gvfs/ci/_git/ForTests)
Fetching blobs from origin (no cache server)...Succeeded

Stats:
  Matched blobs:    168
  Already cached:   168
  Downloaded:       0

$ ls
AuthoringTests.md  GitHooksLoader/  GvFlt_EULA.md  GVFS/  GVFS.sln  License.md  nuget.config  Protocol.md  Readme.md  Settings.StyleCop

$ ls GVFS
GVFS.Common/  GVFS.UnitTests/  LibGit2Sharp.NativeBinaries.props  ProjectedFSLib.NativeBinaries.props

$ scalar sparse --add="GVFS/GVFS"
Authenticating...Succeeded
Querying remote for config...Succeeded
Configured cache server: None (https://dev.azure.com/gvfs/ci/_git/ForTests)
Fetching blobs from origin (no cache server)...Succeeded

Stats:
  Matched blobs:    48
  Already cached:   48
  Downloaded:       0

$ ls GVFS
GVFS/  GVFS.Common/  GVFS.UnitTests/  LibGit2Sharp.NativeBinaries.props  ProjectedFSLib.NativeBinaries.props

$ scalar status
Enlistment root: C:\_git\ForTests
Repo URL: https://dev.azure.com/gvfs/ci/_git/ForTests
Cache Server: None (https://dev.azure.com/gvfs/ci/_git/ForTests)
Local Cache: C:\.scalarCache
Mount status: Ready
Background operations: 0
Disk layout version: 0.0

$ scalar unmount
Unmounting...Succeeded
Unregistering automount...Succeeded

$ scalar status
Unable to connect to Scalar.  Try running 'scalar mount'
```

## Trying Scalar

Scalar will work with any Git service that supports
[the GVFS protocol](https://github.com/microsoft/vfsforgit/blob/master/Protocol.md).
For example, you can clone a repo that is hosted in
[Azure DevOps](https://azure.microsoft.com/services/devops/).

### Sparse Repo Mode

By default, Scalar reduces your working directory to the bare minimum. You
need to add the folders you care about to build up to your working set.

* `scalar clone <URL of repo you just created>`
  * Please choose the **Clone with HTTPS** option in the `Clone Repository` dialog in Azure Repos, not **Clone with SSH**.
* `cd <root>\src`
* At this point, your `src` directory only contains files that appear in your root
  tree. No folders are populated.
* Add folders to your sparse-checkout using:
	1. `scalar sparse --add="folder1;folder2;..."`
	2. `scalar sparse --add-stdin <folder-list.txt`
* After running `scalar sparse`, you will have the requested folders populated.
* Run git commands as you normally would.
* `scalar unmount` when done.

### Full Repo Mode

You can also clone a repo and checkout the full working directory.

* `scalar clone --full-clone <URL of repo you just created>`
  * Please choose the **Clone with HTTPS** option in the `Clone Repository` dialog in Azure Repos, not **Clone with SSH**.
* `cd <root>\src`
* Run git commands as you normally would
* `scalar unmount` when done

# License

The Scalar source code in this repo is available under the MIT license. See [License.md](License.md).

---

# Code of Conduct

This project has adopted the [Microsoft Open Source Code of Conduct][conduct-code].
For more information see the [Code of Conduct FAQ][conduct-FAQ] or contact [opencode@microsoft.com][conduct-email] with any additional questions or comments.

[conduct-code]: https://opensource.microsoft.com/codeofconduct/
[conduct-FAQ]: https://opensource.microsoft.com/codeofconduct/faq/
[conduct-email]: mailto:opencode@microsoft.com
