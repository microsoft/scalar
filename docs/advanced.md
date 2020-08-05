Advanced Features
=================

Run Maintenance in the Foreground
---------------------------------

Scalar runs maintenance on your repositories in the background. You could
chose to run those maintenance steps yourself by running `scalar run <task> [<options>]`.
This command will run one maintenance step as specified by `<task>`. These
tasks are:

* `config`: Set recommended Git config settings. These are all intended
  to improve performance in large repos. If the repo was cloned by Scalar,
  then most of these settings will overwrite any existing settings. Otherwise,
  Scalar will not override a local config value that disagrees with the
  recommendation.

* `commit-graph`: Update the Git commit-graph to include all reachable
  commits. After writing a new file, verify the file was computed successfully.
  This drastically improves the performance of commands like `git log --graph`.

* `fetch`: Fetch the latest data from the remote servers. If using the GVFS
   protocol, download the latest set of commit and tree packs from
   the cache server or the origin remote. Otherwise, this step will fetch
   the latest objects from each remote. This will not update your local
   refs, so your `git fetch` commands will report all ref updates. Those
   `git fetch` commands will be much faster as they will require much less
   data transfer.

* `loose-objects`: Examine the loose objects contained in the shared object
  cache. First, delete any loose objects that are currently located in
  pack-files. Second, create a new pack-file containing the remaining loose
  objects. The pack will contain up to 50,000 objects, leaving any extra
  objects for the next run of the `loose-objects` step. This happens in the
  background once a day.

* `pack-files`:  Update the Git multi-pack-index and repack small pack-files
  into larger pack-files.  Scalar downloads many pack-files during the
  `fetch` step, or during `git checkout` commands. The `pack-files`
  step updates the  Git multi-pack-index to improve lookup speed. Further, it
  combines pack-files into larger files to reduce the total pack-file count.
  This step is designed to work without blocking concurrent processes by only
  deleting pack-files after they were marked as "unused" for at least a day.

* `all`: This task runs all of the above steps in the following order:
  1. `config` ensures our recommended values are set for the remaining steps.
  2. `fetch` downloads the latest data from the remotes.
  3. `commit-graph` updates based on the newly fetched data.
  4. `loose-objects` cleans up loose objects. When using the GVFS protocol,
     the previous steps may have downloaded new loose objects.
  5. `pack-files` cleans up the pack-files, including those downloaed in
     previous steps.

Controlling Background Maintenance
----------------------------------

If you have a need to temporarily pause background maintenance from running,
then you can run `scalar pause [<number>]` to stop all maintenance for
`<number>` hours. If `<number>` is not provided, then maintenance will be
delayed for 12 hours.

For example, you may want to pause maintenance if you need all CPU resources
on your machine available for a high-performance activity or for performance
testing. You may also want to pause maintenance if you are on a metered
internet connection, as some of the maintenance downloads Git data from the
remote server.
