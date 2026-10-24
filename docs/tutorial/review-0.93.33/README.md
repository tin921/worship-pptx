# English tutorial review — 0.93.33

This folder archives the complete production materials for an earlier tutorial review. For the approved tutorial, see [the current tutorial page](../../tutorial.md). This earlier draft is retained as source history and is not the featured release video.

Every app screenshot, browser capture, and native dialog capture was newly created for this version 0.93.33 tutorial. No screenshot or footage from previous tutorials is an input to this video. The renderer references the current app and its current bundled assets.

The demonstration service contains an invocation, three songs, and a Scripture reading, in that order. There is no news or announcements section.

- `plan.json`, `narration.txt`: English narration and pauses.
- `make_voice.py`, `audio/`, `voice.json`: generated English voice and word timings (Microsoft Aria, natural timing, -5% speaking rate).
- `Renderer/`: capture harness referencing the real app, using Avalonia/Skia and its actual controls and handlers. No imitation UI is drawn.
- `rendered/`, `geometry.json`, `upload-geometry.json`, `browser-geometry.json`: app and browser frames and exact bounds for highlights.
- `native/`, `native-capture.md`: Windows/browser captures and the recording procedure.
- `tutorial-service.json`, `output/*.pptx`: original demonstration content and generated presentations.
- `upload-log.txt`, `upload-timing.json`: actual configured-service upload, with frame timing preserved in the edit.
- `build_video.py`, `edit-timeline.json`, `overlay-settings.json`: visual edit, region boxes and large orange arrows. Keyboard shortcuts are narrated without on-screen key badges. Mouse gesture labels remain. No subtitles/captions and no accelerated narration or upload.
- `output/worship-pptx-0.93.33-english-review.mp4`: review video.
- `qa/`, `contact.jpg`, `validation.json`: visual and technical review evidence.
- `audit_sources.py`, `capture-manifest.json`: capture dates and hashes, checked against previous tutorial images.
- `requirements.txt`, `production-records/`: Python dependency versions and production logs.

## Rebuild

Run from the repository root. Requires .NET 10, FFmpeg/ffprobe, Python with Pillow and edge-tts. Build and temporary profile files are stored on E:, outside the repository; all tutorial source and deliverable files are here.

```powershell
. ./scripts/dotnet.ps1
python -m pip install -r docs/tutorial/review-0.93.33/requirements.txt
python docs/tutorial/review-0.93.33/make_voice.py
& $dotnet build docs/tutorial/review-0.93.33/Renderer/Renderer.csproj -c Release --artifacts-path E:/WorshipPptx-work/tutorial-build
& $dotnet E:/WorshipPptx-work/tutorial-build/bin/Renderer/release/Renderer.dll
```

The optional `--upload` capture performs a **real upload** of the demonstration deck to the locally configured Drive service. It reads the user's configuration privately and copies it only into the temporary E: profile, never into this folder. Run it only when a demonstration upload is intended:

```powershell
& $dotnet E:/WorshipPptx-work/tutorial-build/bin/Renderer/release/Renderer.dll --upload
python docs/tutorial/review-0.93.33/prepare_native.py
python docs/tutorial/review-0.93.33/build_video.py --stills
python docs/tutorial/review-0.93.33/build_video.py
python docs/tutorial/review-0.93.33/validate_video.py
python docs/tutorial/review-0.93.33/audit_sources.py
```

Preserve or recapture the browser and native save dialog before composing. Google results naturally change. The tutorial uses public-domain Scripture and original demonstration lyrics; no downloaded Google result becomes a slide background.

The app views are reproducible captures and timed demonstrations, not a continuous desktop recording. Browser and native save dialogs are real Windows captures. Both full-screen entry methods and double-click/Escape exit were additionally checked with native Windows input.
