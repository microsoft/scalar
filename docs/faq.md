Frequently Asked Questions
==========================

Using Scalar
------------

### I don't want a sparse clone, I want every file after I clone!

Run `scalar clone --full-clone <url>` to initialize your repo to include
every file. You can switch to a sparse-checkout later by running
`git sparse-checkout init --cone`.

### I already cloned without `--full-clone`. How do I get everything?

Run `git sparse-checkout disable`.

Scalar Design Decisions
-----------------------

There may be many design decisions within Scalar that are confusing at first
glance. Some of them may cause friction when you use Scalar with your existing
repos and existing habits.

> Scalar has the most benefit when users design repositories
> with efficient patterns.

For example: Scalar uses the sparse-checkout feature to limit the size of the
working directory within a large monorepo. It is designed to work efficiently
with monorepos that are highly componentized, allowing most developers to
need many fewer files in their daily work.

### Why does `scalar clone` create a `<repo>/src` folder?

Scalar uses a file system watcher to keep track of changes under this `src` folder.
Any activity in this folder is assumed to be important to Git operations. By
creating the `src` folder, we are making it easy for your build system to
create output folders outside the `src` directory. We commonly see systems
create folders for build outputs and package downloads. Scalar itself creates
these folders during its builds.

Your build system may create build artifacts such as `.obj` or `.lib` files
next to your source code. These are commonly "hidden" from Git using
`.gitignore` files. Having such artifacts into your source tree creates
additional work for Git because it needs to look at these files and match them
against the `.gitignore` patterns.

By following the pattern Scalar tries to establish and placing your build
intermediates and outputs parallel with the `src` folder and not inside it,
you can help optimize Git command performance for developers in the repository
by limiting the number of files Git needs to consider for many common
operations.

### Why is Scalar a C# tool instead of built directly into Git?

Currently, Scalar does a few things that Git does not do and a few
things that Git will never do.

Git does not have a concept of running background jobs, which is critical to
keeping our repositories running smoothly. Git does not have a concept of
cache servers, so we need to use the GVFS protocol during `scalar clone` to
set up the cache server URL. Scalar also creates the `.scalarCache` directory
so multiple enlistments share objects from the remote, allowing a second
`scalar clone` operation to be much faster than the first. All of these concepts
are on our backlog for contributing to Git. Once we do, we will transition
from the Scalar implementation to the Git implementation.

Scalar uses the GVFS protocol for some repositories. This protocol was created
by Azure Repos to support large repositories before the Git protocol supported
partial clone and the Git client supported missing objects. In a way, the GVFS
protocol proved that there was value in creating the partial clone feature.
This shows precedent for how our C# tools show value that leads to Git client
features. However, Git will never adopt the GVFS protocol, so we need to use
an tool other than the Git client to support teams that use it.

For now, Scalar requires a
[custom version of Git](https://github.com/microsoft/git), but we hope to relax
that restriction eventually.
We try to keep our fork of Git as close to [upstream](https://github.com/git-for-windows/git)
as we can, but some patches are necessary _for now_. Still, maintaining those
patches as we take updates from upstream has a significant cost. It is better
to create features in C# that use Git as a black box, then remove those C#
features as we contribute replacements to Git upstream.

Finally, as Git gains the features that we need in Scalar, Scalar will need to
support backwards compatibility with repositories cloned with older versions of
Scalar. Using the C# layer, we can update the Git version and upgrade old
repositories to whatever new features are added to Git.
