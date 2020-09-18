Troubleshooting
===============

Upgrade
-------

The Scalar service checks for new versions of Scalar daily and will prompt you
for upgrade using a notification. To check yourself, run `scalar upgrade` to
see if an upgrade is available. Run `scalar upgrade --confirm` to actually
perform the upgrade, if you wish.

Diagnosing Issues
-----------------

The `scalar diagnose` command collects logs and config details for the current
repository. The resulting zip file helps root-cause issues.

When run inside your repository, creates a zip file containing several important
files for that repository. This includes:

* All log files from `scalar` commands run in the enlistment, including
  maintenance steps.

* Log files from the Scalar service.

* Configuration files from your `.git` folder, such as the `config` file,
  `index`, `hooks`, and `refs`.

* A summary of your Git object database, including the number of loose objects
  and the names and sizes of pack-files.

As the `diagnose` command completes, it provides the path of the resulting
zip file. This zip can be sent to the support team for investigation.

Modifying Configuration Values
------------------------------

The Scalar-specific configuration is only available for repos using the
GVFS protocol.

### Cache Server URL

When using an enlistment cloned with `scalar clone` and the GVFS protocol,
you will have a value called the cache server URL. Cache servers are a feature
of the GVFS protocol to provide low-latency access to the on-demand object
requests. This modifies the `gvfs.cache-server` setting in your local Git config
file.

Run `scalar cache-server --get` to see the current cache server.

Run `scalar cache-server --list` to see the available cache server URLs.

Run `scalar cache-server --set=<url>` to set your cache server to `<url>`.

### Scalar Config

The `scalar config` command is used for customizing the feed used for
Scalar upgrades. This is so large teams can bundle a custom installer
or other tools along with Scalar upgrades.
