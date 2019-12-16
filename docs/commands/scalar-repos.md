`scalar repos`
==============

The `repos` verb can register or list the repos being monitored by the
Scalar service.

Usage
-----

`scalar repos (add|list)`

Description
-----------

To add a repository to the list of registered repos, run `scalar repos add [<path>]`.
If `<path>` is not provided, then the "current repository" is discovered from
the working directory by scanning the parent paths for a path containing a `.git`
folder, possibly inside a `src` folder. This will assist discovering if the
repository is a Scalar repository or a plain Git repository.

The Scalar service will start to run maintenance on the repository using the
different tasks in the [`scalar maintenance`](scalar-maintenance.md) verb.

> Note: If the repository is a normal Git repository, then the
> `fetch-commits-and-trees` step will perform no actions, as there is
> no equivalent in Git.

To see the list of registered repos, run `scalar repos list`.
