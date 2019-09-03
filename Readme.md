# Scalar

[![Build Status](https://dev.azure.com/gvfs/scalar/_apis/build/status/microsoft.scalar?branchName=master)](https://dev.azure.com/gvfs/scalar/_build/latest?definitionId=32&branchName=master)

## What is Scalar?

Scalar is a C# application that manages large Git repositories. It adds abilities
like on-demand object retrieval, background maintenance tasks, and automatically
sets Git config values and hooks that enable performance enhancements. Scalar
also assists in setting up sparse enlistments.

### Table of Contents

* [Building Scalar on Windows](#building-scalar-on-windows)
* [Building Scalar on Mac](#building-scalar-on-mac)
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

## Building Scalar on Windows

If you'd like to build your own Scalar Windows installer:
* Install Visual Studio 2017 Community Edition or higher (https://www.visualstudio.com/downloads/).
  * Include the following workloads:
    * .NET desktop development
    * Desktop development with C++
    * .NET Core cross-platform development
  * Include the following additional components:
    * .NET Core runtime
    * Windows 10 SDK (10.0.10586.0)
* Install the .NET Core 2.1 SDK (https://www.microsoft.com/net/download/dotnet-core/2.1)
* Create a folder to clone into, e.g. `C:\Repos\Scalar`
* Clone this repo into the `src` subfolder, e.g. `C:\Repos\Scalar\src`
* Run `\src\Scripts\BuildScalarForWindows.bat`
* You can also build in Visual Studio by opening `src\Scalar.sln` (do not upgrade any projects) and building. However, the very first
build will fail, and the second and subsequent builds will succeed. This is because the build requires a prebuild code generation step.
For details, see the build script in the previous step.

You can also use Visual Studio 2019. There are a couple of options for getting all the dependencies.
* You can install Visual Studio 2017 side by side with Visual Studio 2019, and make sure that you have all the dependencies from Visual Studio 2017 installed
* Alternatively, if you only want to have Visual Studio 2019 installed, install the following extra dependency:
  * MSVC v141 VS 2017 C++ build tools via the optional components in the Visual Studio 2019 installer. 

Visual Studio 2019 will [automatically prompt you to install these dependencies](https://devblogs.microsoft.com/setup/configure-visual-studio-across-your-organization-with-vsconfig/)
when you open the solution.

The installer can now be found at `<repo root>\Scalar\BuildOutput\Scalar.Installer.Windows\bin\x64\[Debug|Release]\SetupScalar.<version>.exe`

## Building Scalar on Mac

Note that Scalar on Mac is under active development.

* Ensure you have `Xcode` installed, have accepted the terms of use, and have launched `Xcode` at least once.

* Install [Visual Studio for Mac ](https://visualstudio.microsoft.com/vs/mac). (This will also install the `dotnet` CLI).

* If you still do not have the `dotnet` cli `>= v2.1.300` installed [manually install it]. You can check what version you have with `dotnet --version`.(https://www.microsoft.com/net/download/dotnet-core/2.1)

* Create a `Scalar` directory and Clone Scalar into a directory called `src` inside it:

  ```
  mkdir Scalar
  cd Scalar
  git clone https://github.com/microsoft/scalar.git src
  ```

* Using XCode, open `Scalar/ReadObjectHook/Scalar.ReadObjectHook.Mac.xcodeproj`.

  * Select the `Scalar.ReadObjectHook.Mac` project.
  * Under "Signing", select your developer certificate, which may be an organization
    certificate.

* Run the build and installation scripts:

  ```
  cd src/Scripts/Mac
  ./BuildScalarForMac.sh
  ./CreateScalarDistribution.sh
  ../../../BuildOutput/Scalar.Distribution/InstallScalar.sh
  ```

  _Troubleshooting if this fails_

  If you get
  ```
  xcodebuild: error: SDK "macosx10.13" cannot be located.
  ```
  You may have the "Xcode Command Line Tools" installed (helpfully by Mac OS) instead of full `Xcode`.
  Make sure
  ```
  xcode-select -p
  ```

  shows `/Applications/Xcode.app/Contents/Developer`. If it does not, install `Xcode` and then launch it (you can close it afterwards.)

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
