`scalar remove`
==============

The `remove` verb deletes a Scalar enlistment

Usage
-----

`scalar remove <dir>`

Description
-----------

For the enlistment with root at `<dir>`, prepare the enlistment for deletion
as follows:

1. Alert the filesystem watcher to stop watching `<dir>/src`.

2. Alert the Scalar service to stop background maintenance on this enlistment.

Then, delete the enlistment by removing `<dir>`.
