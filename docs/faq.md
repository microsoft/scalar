Frequently Asked Questions
==========================

Using Scalar
------------

### I don't want a sparse clone, I want every file after I clone!

Run `scalar clone --full-clone <url>` to initialize your repo to include
every file. You can switch to a sparse-checkout later by running
`git sparse-checkout init --cone`.

### I already cloned withou `--full-clone`. How do I get everything?

Run `git sparse-checkout disable`.

Scalar Design Decisions
-----------------------

There may be many design decisions within Scalar that are confusing at first
glance. Some of them may cause friction when you use Scalar with your existing
repos and existing habits.

> Scalar has the most benefit when users design repositories
> with efficient patterns.

For example: Scalar uses the sparse-checkout feature to limit the size of the
working directory within a large monorepo. This encourages code architects to
build componetized units within their monorepo, allowing most developers to
need many fewer files in their daily work.

### Why does `scalar clone` create a `<repo>/src` folder?

Scalar uses a file-system watcher to keep track of changes in this `src` folder.
Any activity in this folder is assumed to be important to Git operations. By
creating the `src` folder, we are making it easy for your build system to
create output folders outside the `src` directory. We commonly see systems
create folders for build outputs and package downloads. Scalar itself creates
these folders during its builds.

Your build system may create build artifacts next to your source code in the
`src` folder, but "hide" them from Git using `.gitignore` files. While you do
not see these changes, Git still needs to do work to look at these files and
match them against the `.gitignore` patterns.
