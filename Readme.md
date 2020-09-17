![Scalar](Scalar/Images/scalar-card.png)

## What is Scalar?

Scalar is an opinionated repository management tool. By creating new
repositories or registering existing repositories with Scalar, your Git
experience will speed up. Scalar sets advanced Git config settings,
maintains your repositories in the background, and helps reduce data sent
across the network.

You can learn more about Scalar in this video: [Git at Scale for Everyone](https://www.youtube.com/watch?v=USLB1gwl1vA).

## Table of Contents

* [Getting Started](#getting-started)
* [Example Workflow](#example-workflow)
* [License](#license)
* [Code of Conduct](#code-of-conduct)

## Getting Started

Full details can be found in [our documentation page](https://github.com/microsoft/scalar/blob/HEAD/docs/index.md).

### Installing on macOS

Scalar is installed and updated on macOS via [Homebrew](https://brew.sh/).

```sh
brew tap microsoft/git
brew cask install scalar
```

If you wish to use the [GVFS Protocol][gvfs-protocol], then you will
instead need the cask that uses [our custom fork of Git][microsoft-git]:

```sh
brew tap microsoft/git
brew cask install scalar-azrepos
```

When new versions of Scalar are available, you can upgrade in a few
different ways. First, you can use `brew`:

```sh
brew update
brew upgrade --cask scalar[-azrepos]
```

Alternatively, you can run `scalar upgrade` and it will run the necessary
`brew` commands on your behalf.

If your repository has many files in the working directory, then you might
want to install [Watchman](https://github.com/facebook/watchman), which
Scalar will detect and configure with Git's File System Monitor feature.

```sh
brew install watchman
```

### Installing on Windows

To install Scalar on Windows, download `Installers_Windows_Release.zip`
from the [latest release](https://github.com/microsoft/scalar/releases)
and extract all of the files.  Run the contained installers for Git and
Scalar, with names matching these patterns:

 * `Git\Git-2.XX.Y-vfs.*.*-64-bit.exe`
 * `Scalar\SetupScalar.YY.MM.<sprint>.<minor>.exe`

_Note: we are investigating package management tools to have the Windows
install process be as simple as the `brew` process on macOS. See
[#433](https://github.com/microsoft/scalar/issues/433) for progress on
this feature._

### Basic Scalar Use

To create a new local repository from a remote repository, run

```sh
scalar clone [--full-clone] <url> [<dir>]
```

If the given `<url>` is hosted by Azure Repos, then the clone will use
[the GVFS Protocol](gvfs-procool)
to reduce the amount of data sent across the network. Otherwise, this will
attempt to use [Git's partial clone feature](https://git-scm.com/docs/git-clone#Documentation/git-clone.txt---filterltfilter-specgt)
to achieve similar results.

If you already have a local Git repository and do not want to clone a new
one, you can get many of the benefits of Scalar by registering your repository
using the `scalar register` command.

```sh
scalar register
```

After either of these commands, your repositories will be initialized with
advanced Git performance features and will be maintained in the background
according to our recommended maintenance schedule.

## Example Workflow

If you want to get a feel for an initial workflow with a test project, here
are some commands that clone [our test repo](https://dev.azure.com/gvfs/ci/_git/ForTests)
and initialize the sparse-checkout definition to grow the working directory.

Run these commands in Terminal on macOS or in Git Bash on Windows.

First, clone the repository using the GVFS protocol.

```sh
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
```

Then, navigate into the repository's `src` directory.

```sh
$ cd ForTests/src
$ ls
AuthoringTests.md  GvFlt_EULA.md  GVFS.sln  License.md  nuget.config  Protocol.md  Readme.md  Settings.StyleCop
```

This directory is the _working directory_, which contains all files
tracked by Git. It also includes the `.git` directory and its contents.
This allows build outputs to be written to directories next to the `src`
directory instead of inside it, which speeds up some Git operations.

The working directory does not contain any subdirectories (other than the
`.git` directory). This is due to the initial sparse-checkout definition
which only cares about the files at root. We can expand the sparse-checkout
using the `git sparse-checkout set` and `git sparse-checkout add` commands:

```sh
$ git sparse-checkout set GVFS/GVFS.Common GVFS/GVFS.UnitTests GitHooksLoader
$ ls
AuthoringTests.md  GitHooksLoader/  GvFlt_EULA.md  GVFS/  GVFS.sln  License.md  nuget.config  Protocol.md  Readme.md  Settings.StyleCop

$ ls GVFS
GVFS.Common/  GVFS.UnitTests/  LibGit2Sharp.NativeBinaries.props  ProjectedFSLib.NativeBinaries.props

$ git sparse-checkout add GVFS/GVFS
$ ls GVFS
GVFS/  GVFS.Common/  GVFS.UnitTests/  LibGit2Sharp.NativeBinaries.props  ProjectedFSLib.NativeBinaries.props
```

[Learn more about sparse-checkout here](https://github.blog/2020-01-17-bring-your-monorepo-down-to-size-with-sparse-checkout/).

## License

The Scalar source code in this repo is available under the MIT license. See [License.md](License.md).

## Code of Conduct

This project has adopted the [Microsoft Open Source Code of Conduct][conduct-code].
For more information see the [Code of Conduct FAQ][conduct-FAQ] or contact [opencode@microsoft.com][conduct-email] with any additional questions or comments.


[gvfs-protocol]: https://github.com/microsoft/VFSForGit/blob/HEAD/Protocol.md
[microsoft-git]: https://github.com/microsoft/git
[conduct-code]: https://opensource.microsoft.com/codeofconduct/
[conduct-FAQ]: https://opensource.microsoft.com/codeofconduct/faq/
[conduct-email]: mailto:opencode@microsoft.com
