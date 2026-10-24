# Native recording procedure

1. Launch the published Windows executable with `WORSHIP_PPTX_DATA` set to the isolated profile recorded in `profile-path.txt`.
2. Open the background picker for 同心頌讚. Wait for matching to finish.
3. Click the actual Google image link. The Windows default browser (Edge on this machine) opens the URL recorded in `google-image-search.txt`.
4. Maximize that browser window. Save its window capture as `native/browser-top.png`; scroll down and capture `native/browser-scrolled.png`. Capture only the search window, excluding unrelated applications/tabs.
5. Back in the picker, double-click the first result. Capture `native/fullscreen-double.png`. Double-click again and verify the picker returns.
6. Click its expand-box button, verify the same full-screen image opens, and press Escape to close. Cancel the picker so the selected slide background remains the demonstration background.
7. Click Create. Capture the actual save dialog as `native/save-dialog.png`, then save the demonstration file in `output/`. Capture the main window after its success status as `native/export-complete.png`.
8. In the native app's upload window, upload the same `TutorialDemo-1025c-call+praise.pptx` with replacement enabled. After the app confirms completion, capture `native/drive-link-before-click.png` and click its actual FolderLink. In the browser that opens, verify the demonstration filename and capture the folder as `native/drive-folder.png`. Record the visible filename bounds in `native/drive-folder-region.json`; `prepare_native.py` maps those bounds into the video frame. The capture shows the file at 12.5 MB in the shared app drop folder.

Native interaction was performed through the computer-use skill's `@oai/sky` API: fresh `list_windows`/`get_window_state` observations, then one `click`, `press_key`, or `scroll` action followed by a new observation. Screen pixels were saved solely as production assets. Raw capture images are retained alongside the composited video frames.

The final Drive folder screenshot was captured from the actual Chrome tab opened by the app's FolderLink, using the CUA browser API. The browser viewport excludes unrelated tabs. Its visible file row was checked before saving the capture and its bounds.

Typical capture helper, used with a freshly observed target window:

```javascript
const state = await sky.get_window_state({ window, include_screenshot: true });
await fs.writeFile(path, Buffer.from(state.screenshots[0].url.split(',')[1], 'base64'));
```

No shared upload key, browser cookies, or authentication tokens are included in the tutorial assets.
