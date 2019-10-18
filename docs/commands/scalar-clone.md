`scalar clone`
==============

The `clone` verb creates a local enlistment of a remote repository.

Usage
-----

`scalar clone [options] <url> [<dir>]`

Description
-----------

Create a local copy of the repository at `url`. If specified, create the `dir`
folder and place the repository there. Otherwise, the last section of the `url`
will be used for `dir`.

At the end, the repo is located at `dir/src`. By default, the sparse-checkout
feature is enabled and the only files present are those in the root of your
Git repository. Use `git sparse-checkout set` to expand the set of folders
you want to see, or `git sparse-checkout disable` to expand to all files.

Options
-------

These options allow a user to customize their initial enlistment.

* `--full-clone`: If specified, do not initialize the sparse-checkout feature.
  All files will be present in your `src` directory.

* `--cache-server-url=<url>`: If specified, set the intended cache server to
  the specified `url`. All object queries will use the GVFS protocol to this
  `url` instead of the origin remote.

* `--branch=<ref>`: Specify the branch to checkout after clone.

* `--local-cache-path=<path>`: Use this option to override the path for the
  local Scalar cache. If not specified, then Scalar will select a default
  path to share objects with your other enlistments. On Windows, this path
  is a subfolder of `<Volume>:\.scalarCache\`. On Mac, this path is a subfolder
  of `~/.scalarCache/`.

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
  traversal.

