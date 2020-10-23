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

# Use only one of the following, depending on which you have installed:
brew upgrade --cask scalar
brew upgrade --cask scalar-azrepos
```

Alternatively, you can run `scalar upgrade` and it will run the necessary
`brew` commands on your behalf.

If your repository has many files in the working directory, then you might
want to install [Watchman](https://github.com/facebook/watchman), which
Scalar will detect and configure with Git's File System Monitor feature.

```sh
brew install watchman
```

Installing on Windows
--------------------

To install Scalar on Windows,
[download the `Installers_Windows_Release.zip` from the releases page](https://github.com/microsoft/scalar/releases).
Extract the `Installers_Windows_Release` folder, open it in a command prompt, and
run `InstallScalar.bat`. This will install the following components:

* [Git for Windows](https://github.com/microsoft/git) (with custom patches)
* Scalar
* [Watchman](https://github.com/facebook/watchman), if you use the `--watchman` argument.

Installing on Linux
-------------------

Currently, we package a custom version of Git and Scalar as `.deb` packages
that can be installed after downloading from the GitHub releases pages. The
latest releases can be downloaded and installed as follows:

```sh
# Install git-vfs, a custom fork of Git
wget https://github.com/microsoft/git/releases/download/v2.29.0.vfs.0.0/git-vfs_2.29.0.vfs.0.0.deb
sudo dpkg -i git-vfs_2.29.0.vfs.0.0.deb

# Install GCM Core
wget https://github.com/microsoft/Git-Credential-Manager-Core/releases/download/v2.0.252-beta/gcmcore-linux_amd64.2.0.252.766.deb
sudo dpkg -i gcmcore-linux_amd64.2.0.252.766.deb
git-credential-manager-core configure

# Install Scalar
wget https://github.com/microsoft/scalar/releases/download/v20.10.178.6/scalar-azrepos-linux_amd64.20.10.178.0.deb
sudo dpkg -i scalar-azrepos-linux_amd64.20.10.178.0.deb

# Test installation
git version
git-credential-manager-core version
scalar version
```

> Note: If you don't have `wget`, then try `sudo apt-get install wget` first.

At this point, you must install our custom version of Git in order to get
background maintenance as part of `scalar clone` or `scalar register`. As
that feature is accepted and merged into the core Git client, then you can
take advantage of the feature without the custom fork.

We are working to provide a package via `apt-get` to make this installation
easier and better for automatically upgrading.

The current installation via `.deb` package only works on Debian-based
Linux distributions. The software has only been compiled and tested with x86_64/amd64
architectures. Motivated users can install [`microsoft/git`](https://github.com/microsoft/git)
and Scalar from source. See [the `InstallFromSource.sh` script](https://github.com/microsoft/scalar/blob/2dc48243c50763024b048c5f36d5f50835943dda/Scripts/Linux/InstallFromSource.sh#L62-L76)
for assistance installing Scalar from source.

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

* [The Philosophy of Scalar](philosophy.md): Why does Scalar work the way
  it does, and how do we make decisions about its future?

* [Frequently Asked Questions](faq.md)
