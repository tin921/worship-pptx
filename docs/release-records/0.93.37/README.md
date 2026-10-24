# v0.93.37 release records

The Windows installer and portable ZIP were built on E: from the application source in commit `22ad3b176a70c8e2f01c1f86de40b33c7933ea4f`. Both packages match all 1,101 intended payload files. No native MSI installation was performed.

`publication.json` records the published assets, SHA-256 hashes, sizes, and successful anonymous downloads. `readme-top.png` and `readme-player.png` show the published README; the inline video was played at 1× with a duration of 283.458333 seconds. `drive-playback.png` records the separate cloud playback check.

The original Windows run exposed three test assumptions: rapid saves could share a serialized edit timestamp, and synthetic double clicks counted rendering time as part of the click interval. The history tests now explicitly select their fixtures. The gallery test supplies hardware-style timestamps and click counts through routed pointer events, preserving single-click and double-click assertions. Actual Windows double-click opening and closing were also verified in the fresh tutorial capture. These test changes do not alter the application payload.

The original results are retained in `tests.log` and `gallery-recheck.log`; final validation is recorded separately. Tutorial files, narration, captures, and reproducible production scripts are under `../../tutorial/review-0.93.37/`.

`package.ps1` and `verify.ps1` retain this release's E: build paths. `publish_release.py` requires the dependency in `requirements.txt`, a configured Git credential helper, verified package records, and a pushed main branch. It publishes to `tin921/worship-pptx`; credentials remain in memory. Run it only when publication is intended.
