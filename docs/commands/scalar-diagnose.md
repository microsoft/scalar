`scalar diagnose`
=================

The `diagnose` verb collects logs and config details. The resulting zip file
helps root-cause issues.

Usage
-----

`scalar diagnose`

Description
-----------

Create a zip file containing several important files in your enlistment. This
includes:

* All log files from `scalar` commands run in the enlistment since `clone`.

* Log files from the Scalar Service.

* Configuration files from your `.git` folder, such as the `config` file,
  `index`, `hooks`, and `refs`.

* A summary of your shared object cache, including the number of loose files
  and the names and sizes of pack-files.

As the `diagnose` command completes, it provides the path of the resulting
zip file. This can be sent to the support team for investigation.
