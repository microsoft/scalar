`scalar diagnose`
=================

The `diagnose` verb collects logs and config details for the current repository.
The resulting zip file helps root-cause issues.

Usage
-----

`scalar diagnose`

Description
-----------

When run inside your repository, creates a zip file containing several important
files for that repository. This includes:

* All log files from `scalar` commands run in the enlistment since `clone`.

* Log files from the Scalar service.

* Configuration files from your `.git` folder, such as the `config` file,
  `index`, `hooks`, and `refs`.

* A summary of your shared object cache, including the number of loose objects
  and the names and sizes of pack-files.

As the `diagnose` command completes, it provides the path of the resulting
zip file. This zip can be sent to the support team for investigation.
