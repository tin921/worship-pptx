# Program removal and feedback — 0.93.35

The selected program shows a Remove button at the lower right of its green sidebar row. Confirmation identifies its name and date. After removal, the app selects the closest remaining date on or after today, then the latest past date if there are no upcoming programs. If the final program is removed, a new blank program keeps the editor usable. Only the program record and its recovery copies are removed; shared images and exported presentations are untouched.

The top-right feedback icon opens the supplied Google Doc. Native clicking was verified to open **App feedback - Google Docs** at the expected document URL. No document content was edited.

- `editor.png`: fresh screenshot of the built Windows app, using an isolated demonstration profile.
- `remove-confirmation.png`: actual confirmation window; Cancel was used for the screenshot demonstration.
- Windows build: `E:/WorshipPptx-work/worship-review-0.93.35/WorshipPptx.exe`.
- Three new program-removal tests passed, including cancellation, date selection, recovery-file removal, persistence, and the last-program case.
- Full suite: 127 passed, with one outdated layout assertion expecting Settings to be the rightmost icon. That assertion was updated for Feedback, and all 11 editor/layout checks then passed. No app changes were needed after the full run.
- Build completed with no warnings or errors. Logs are saved alongside this file.

These are the original review records from before publication. The app changes are now included in [v0.93.36](../release-v0.93.36.md), which also centers the Remove label. The tutorial remains unchanged.
