`scalar clone`
==============

The `clone` verb creates a local enlistment of a remote repository.

Usage
-----

`scalar clone [options] <url> [<dir>]`

Description
-----------

Create a local copy of the repository at `<url>`. If specified, create the `<dir>`
directory and place the repository there. Otherwise, the last section of the `<url>`
will be used for `<dir>`.

At the end, the repo is located at `<dir>/src`. By default, the sparse-checkout
feature is enabled and the only files present are those in the root of your
Git repository. Use `git sparse-checkout set` to expand the set of directories
you want to see, or `git sparse-checkout disable` to expand to all files. You
can explore the subdirectories outside your sparse-checkout specification using
`git ls-tree HEAD`.

Options
-------

These options allow a user to customize their initial enlistment.

* `--full-clone`: If specified, do not initialize the sparse-checkout feature.
  All files will be present in your `src` directory. This behaves very similar
  to a Git partial clone in that blobs are downloaded on demand. However, it
  will use the GVFS protocol to download all Git objects.

* `--cache-server-url=<url>`: If specified, set the intended cache server to
  the specified `<url>`. All object queries will use the GVFS protocol to this
  `<url>` instead of the origin remote. If the remote supplies a list of
  cache servers via the `<url>/gvfs/config` endpoint, then the `clone` command
  will select a nearby cache server from that list.

* `--branch=<ref>`: Specify the branch to checkout after clone.

* `--local-cache-path=<path>`: Use this option to override the path for the
  local Scalar cache. If not specified, then Scalar will select a default
  path to share objects with your other enlistments. On Windows, this path
  is a subdirectory of `<Volume>:\.scalarCache\`. On Mac, this path is a
  subdirectory of `~/.scalarCache/`. The default cache path is recommended so
  multiple enlistments of the same remote repository share objects on the
  same device.

Advanced Options
----------------

The options below are not intended for use by a typical user. These are
usually used by build machines to create a temporary enlistment that
operates on a single commit.

* `--single-branch`: Use this option to only download metadata for the branch
  that will be checked out. This is helpful for build machines that target
  a remote with many branches. Any `git fetch` commands after the clone will
  still ask for all branches.

* `--no-prefetch`: Use this option to not prefetch commits after clone. This
  is not recommended for anyone planning to use their clone for history
  traversal. Use of this option will make commands like `git log` or
  `git pull` extremely slow and is therefore not recommended.

