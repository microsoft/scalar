`scalar upgrade`
================

The `upgrade` verb checks for the latest version of Scalar, and can allow
upgrading to that version.

Usage
-----

`scalar upgrade <options>`

Description
-----------

Check for a new Scalar version. With `--confirm`, will perform the upgrade
by downloading the new version and running the installer.


Options
-------

* `--confirm`: Pass in this flag to actually install the newest release.

* `--dry-run`: Display progress and errors, but don't install Scalar. Not
  compatible with the `--confirm` option.

* `--no-verify`: This parameter is currently required for upgrade on
   macOS.

