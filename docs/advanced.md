Advanced Features
=================

Run Maintenance in the Foreground
---------------------------------

Scalar runs maintenance on your repositories in the background. You could
chose to run those maintenance steps yourself by running `scalar run <task> [<options>]`.
This command will run one maintenance step as specified by `<task>`. These
tasks are:

* `commit-graph`: Update the Git commit-graph to include all reachable
  commits. After writing a new file, verify the file was computed successfully.
  This helps commands like `git log --graph` work very quickly.

* `fetch`: Fetch the latest data from the remote server. If using the GVFS
   protocol, download the latest set of commit and tree packs from
   the cache server or the origin remote. This will not update your local
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
  `commits-and-trees` step, or during `git checkout` commands. The `pack-files`
  step updates the  Git multi-pack-index to improve lookup speed. Further, it
  combines pack-files into larger files to reduce the total pack-file count.
  This step is designed to work without blocking concurrent processes by only
  deleting pack-files after they were marked as "unused" for at least a day.
  This step allows using the `--batch-size=<size>` option. By default, the
  batch-size is "2g" for two gigabytes. This batch size signifies the goal
  size of a repacked pack-file.

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
