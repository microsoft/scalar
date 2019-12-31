`scalar repos`
==============

Manage the set of repositories being maintained by Scalar.

Usage
-----

`scalar repos (add|list)`

Description
-----------

To add a repository to the list of registered repos, run `scalar repos add [<path>]`.
If `<path>` is not provided, then the "current repository" is discovered from
the working directory by scanning the parent paths for a path containing a `.git`
folder, possibly inside a `src` folder.

To see which repositories are currently tracked by the service, run
`scalar repos list`.
