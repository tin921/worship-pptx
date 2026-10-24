# English tutorial review — 0.93.37

This new review corrects the PIP demonstration and narration. The toggle only shows or hides the presenter guide. Text size, position, formatting, pagination, and exported PowerPoint content stay unchanged. The capture harness checks that the text and slide count are identical before, during, and after the toggle; see `pip-invariance.json`.

The revised cut runs 4 minutes 43 seconds. Native Windows checks confirm double-click opening and closing, the actual Google search link, the completed upload's folder link, and the feedback document. The capture audit checks 202 fresh images against 619 older tutorial image hashes and finds no reused frames.

All app, native dialog, and browser images are captured afresh for this review. No older tutorial screenshot is an input. Unchanged narration audio is retained; the PIP narration is newly generated with the same Microsoft Aria voice and natural speaking rate. The video has no captions or keyboard badges, and retains the large orange pointers.

The example service contains an invocation, three original demonstration songs, and Scripture reading. It includes background search, five styles, full-image viewing, Google results, background pan/zoom/flip/opacity, keyboard navigation, Preview, Create, actual upload and its Drive folder, program removal, and feedback.

All production files live in this folder: `plan.json`, `narration.txt`, `audio/`, `voice.json`, the `Renderer/` capture harness, fresh `rendered/` and `native/` captures, geometry, upload timing/log, output PowerPoint files, Python production scripts, final MP4, and QA evidence. Temporary profiles and build files are on E:. Private upload settings stay outside the repository.

## Rebuild

From the repository root, with .NET 10, FFmpeg/ffprobe, and Python dependencies in `requirements.txt`:

```powershell
. ./scripts/dotnet.ps1
python docs/tutorial/review-0.93.37/make_voice.py
& $dotnet build docs/tutorial/review-0.93.37/Renderer/Renderer.csproj -c Release --artifacts-path E:/WorshipPptx-work/tutorial37-build
& $dotnet E:/WorshipPptx-work/tutorial37-build/bin/Renderer/release/Renderer.dll
```

The optional `--upload` capture performs an actual demonstration upload using the locally configured service. Only run it when that upload is intended. Then capture the native interactions described in `native-capture.md` and compose:

```powershell
& $dotnet E:/WorshipPptx-work/tutorial37-build/bin/Renderer/release/Renderer.dll --upload
python docs/tutorial/review-0.93.37/prepare_native.py
python docs/tutorial/review-0.93.37/build_video.py --stills
python docs/tutorial/review-0.93.37/build_video.py
python docs/tutorial/review-0.93.37/validate_video.py
python docs/tutorial/review-0.93.37/check_encoded_frames.py
python docs/tutorial/review-0.93.37/audit_sources.py
python docs/tutorial/review-0.93.37/make_embed.py
```

The app views are reproducible captures of actual Avalonia controls and input handlers, combined into a timed demonstration. Browser/native dialogs are Windows captures. This is not a continuous desktop recording. Actual upload duration and natural narration timing are preserved.

`make_embed.py` makes a smaller H.264 copy for GitHub's inline attachment player, retaining the full resolution, edit, duration, and playback speed. The full-quality review MP4 remains available separately. Both copies and their validation records are retained here.

[Watch the 4:43 tutorial](https://drive.google.com/file/d/1Zsh2Vz8K-jop2mNLPW1utBY9dR4M9saz/view). The README inline player uses the smaller copy recorded in `inline-video.json`. Cloud playback and the native GitHub player were verified at normal speed; see `review-links.json` and `visual-review.json`.
