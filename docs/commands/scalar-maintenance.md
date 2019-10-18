`scalar maintenance`
====================

The `maintenance` verb runs one of several maintenance tasks that normally
happen in the background.

Usage
-----

`scalar maintenance --task=<task> [--batch-size=<size>]`

Description
-----------

Run the maintenance task specified by `<task>`. The options are:

* `commit-graph`: Update the Git commit-graph to include all reachable commits.
  After writing a new file, verify the file was computed successfully. Very
  rarely, we see hardware issues (bad memory, corrupt disk) cause a corrupt
  commit-graph file, so this step attempts to auto-heal from those rare cases.
  If the verify reports the file is corrupt, then delete the file and write a
  new one. This happens in the background every hour.

* `commits-and-trees`: The GVFS protocol allows downloading commits and trees
  introduced since a timestamp. Cache servers precompute "prefetch" packs on
  an hourly basis containing these commits and trees. This step tries to get
  the latest set of commit and tree packs from the cache server or the origin
  remote. This happens in the background once every hour.

* `loose-objects`: Examine the loose objects contained in the shared object
  cache. First, delete any loose objects that are currently located in
  pack-files. Second, create a new pack-file containing the remaining loose
  objects. The pack will contain up to 50,000 objects, leaving any extra
  objects for the next run of the `loose-objects` step. This happens in the
  background once a day.

* `pack-files`: Scalar downloads many pack-files during the `commits-and-trees`
  step, or during `git checkout` commands. The `pack-files` step updates the
  Git multi-pack-index to improve lookup speed. Further, it works to combine
  pack-files into larger files to reduce the total pack-file count. This step
  is designed to work without blocking concurrent processes by only deleting
  pack-files after they were marked as "unused" for at least a day. This step
  allows using the `--batch-size=<size>` option. By default, the batch-size
  is "2g" for two gigabytes. This batch size signifies the goal size of a
  repacked pack-file. This step happens in the background once a day.
