# English tutorial — recorded with 0.93.35

This folder contains the complete production materials for the approved English tutorial, included with [release v0.93.36](https://github.com/tin921/worship-pptx/releases/tag/v0.93.36). [Watch on your phone or in a browser](https://drive.google.com/file/d/1HADwsZCLtDVUC6DEUAEpiCDD5ToAfLJh/view), or [download the MP4](https://github.com/tin921/worship-pptx/releases/download/v0.93.36/worship-pptx-0.93.35-english-review.mp4). The filename retains its recording version; the video has not been re-edited for the v0.93.36 Remove-label alignment change.

The revised cut is 4 minutes 39 seconds. It adds selected-program removal with confirmation and the nearest-upcoming selection, plus the feedback icon and its actual Google document. It retains the rectangular green projector header, per-item picture-in-picture toggle, both panel splitters, and overall app window resizing. Full-image viewing is demonstrated once; Preview, Create, and Upload focus on purpose.

Technical validation passed: 278.958 seconds, 1600×900 at 24 fps, English audio, no subtitle stream, and complete decode without errors. The capture audit found 198 fresh capture files with no matches against 396 earlier tutorial image hashes. Selected encoded frames were visually checked, and Drive playback was verified at 1× with a 4:39 duration. Details are in validation.json, visual-review.json, capture-manifest.json, and review-links.json. Those files retain their original review-stage status; current publication details are in [publication.json](publication.json).

Every app screenshot, browser capture, and native dialog capture was newly created for this version 0.93.35 tutorial. No screenshot or footage from previous tutorials is an input to this video. The renderer references the current app and its current bundled assets.

The demonstration service contains an invocation, three songs, and a Scripture reading, in that order. There is no news or announcements section.

- `plan.json`, `narration.txt`: English narration and pauses.
- `make_voice.py`, `audio/`, `voice.json`: generated English voice and word timings (Microsoft Aria, natural timing, -5% speaking rate).
- `Renderer/`: capture harness referencing the real app, using Avalonia/Skia and its actual controls and handlers. No imitation UI is drawn.
- `rendered/`, `geometry.json`, `upload-geometry.json`, `browser-geometry.json`: app and browser frames and exact bounds for highlights.
- `native/`, `native-capture.md`: Windows/browser captures and the recording procedure.
- `tutorial-service.json`, `output/*.pptx`: original demonstration content and generated presentations.
- `upload-log.txt`, `upload-timing.json`: actual configured-service upload, with frame timing preserved in the edit.
- `build_video.py`, `edit-timeline.json`, `overlay-settings.json`: visual edit, region boxes and large orange arrows. Keyboard shortcuts are narrated without on-screen key badges. Mouse gesture labels remain. No subtitles/captions and no accelerated narration or upload.
- `output/worship-pptx-0.93.35-english-review.mp4`: review video.
- `qa/`, `contact.jpg`, `validation.json`: visual and technical review evidence.
- `check_encoded_frames.py`, `visual-review.json`: reproducible extraction and review of actual encoded frames.
- `review-links.json`: mobile-friendly review links in the user's existing shared Drive folder.
- `audit_sources.py`, `capture-manifest.json`: capture dates and hashes, checked against previous tutorial images.
- `requirements.txt`, `production-records/`: Python dependency versions and production logs.

## Rebuild

Run from the repository root. Requires .NET 10, FFmpeg/ffprobe, Python with Pillow and edge-tts. Build and temporary profile files are stored on E:, outside the repository; all tutorial source and deliverable files are here.

```powershell
. ./scripts/dotnet.ps1
python -m pip install -r docs/tutorial/review-0.93.35/requirements.txt
python docs/tutorial/review-0.93.35/make_voice.py
& $dotnet build docs/tutorial/review-0.93.35/Renderer/Renderer.csproj -c Release --artifacts-path E:/WorshipPptx-work/tutorial35-build
& $dotnet E:/WorshipPptx-work/tutorial35-build/bin/Renderer/release/Renderer.dll
```

The optional `--upload` capture performs a **real upload** of the demonstration deck to the locally configured Drive service. It reads the user's configuration privately and copies it only into the temporary E: profile, never into this folder. Run it only when a demonstration upload is intended:

```powershell
& $dotnet E:/WorshipPptx-work/tutorial35-build/bin/Renderer/release/Renderer.dll --upload
python docs/tutorial/review-0.93.35/prepare_native.py
python docs/tutorial/review-0.93.35/build_video.py --stills
python docs/tutorial/review-0.93.35/build_video.py
python docs/tutorial/review-0.93.35/validate_video.py
python docs/tutorial/review-0.93.35/check_encoded_frames.py
python docs/tutorial/review-0.93.35/audit_sources.py
```

Preserve or recapture the browser and native save dialog before composing. Google results naturally change. The tutorial uses public-domain Scripture and original demonstration lyrics; no downloaded Google result becomes a slide background.

The app views are reproducible captures and timed demonstrations, not a continuous desktop recording. Browser and native save dialogs are real Windows captures. Double-click entry and exit were checked with native Windows input; the renderer also exercises the expand button and Escape exit. All screen captures were made afresh for this version. The real upload timing is preserved, and narration is not accelerated.
