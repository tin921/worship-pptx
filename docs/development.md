# Development

## Build and run

The desktop app uses **C#, Avalonia 12.1.4 and .NET 10**. Install the .NET 10 SDK;
Visual Studio and the macOS .NET workload are not required. PowerShell scripts
also find an SDK installed privately in `.tools/dotnet`.

```powershell
./scripts/build.ps1
./scripts/publish.ps1 -Runtime win-x64
./scripts/run.ps1 -SkipBuild
./scripts/package.ps1 -Version 0.91.0 -SkipBuild
```

`build.ps1` builds and runs the validation suite. `publish.ps1` creates a
self-contained app, including the runtime and native graphics libraries.
`package.ps1` creates an x64 per-user MSI and portable ZIP in `dist`; it includes
all runtime DLLs, fonts and notices. No script pushes code or uploads a release.
WiX 3.14.1 is downloaded with a pinned SHA-256 check if needed.

On macOS:

```sh
dotnet run --project tests/WorshipPptx.Tests.csproj -c Release -- artifacts/verification
bash scripts/package-macos.sh osx-arm64 0.91.0
# Intel Mac: use osx-x64 instead.
```

The Mac script produces an `.app`, ZIP and DMG with ad-hoc signatures for local
testing. Public distribution needs Developer ID signing and notarization; those
steps are intentionally not automated without signing credentials. The current
Mac builds were cross-published from Windows, not executed on a Mac. The macOS
CI job can exercise native fonts, rendering, storage and packaging when run.

Cross-build either Mac architecture from Windows:

```powershell
./scripts/publish.ps1 -Runtime osx-arm64
./scripts/publish.ps1 -Runtime osx-x64
```

## Compatibility

- Windows x64: Windows 10 / 11. The installer's compatibility floor is Windows
  10 1607, build 14393, based on .NET 10's listed LTSC/Enterprise baseline.
  **That old Windows build has not been tested.** Use Windows 10 22H2 or Windows
  11 for the Avalonia support tiers currently listed by its maintainers.
- macOS: package baseline **14.0**, Apple Silicon and Intel. Both architectures
  cross-compile; an actual Mac review is still required.
- Tested locally: Windows 11 build 26200, x64. The old WPF build's Windows 10
  1507 / .NET Framework 4.6.2 target does not apply to this branch.
- Framework support and Microsoft's OS lifecycle are different from whether an
  application can run. .NET's current Windows 10 support is limited to the listed
  LTSC/Enterprise editions.

Sources: [Avalonia platform support](https://docs.avaloniaui.net/docs/supported-platforms),
[Microsoft .NET on Windows](https://learn.microsoft.com/en-us/dotnet/core/install/windows).

## Structure and data

- `Models.cs`, `LocalStore.cs`: existing JSON format, atomic save and backups.
- `WindowLayouts.cs`: normal window dimensions, maximized state and editor divider widths in a separate local `window-layout.json`; dialogs remember their sizes independently.
- `SlideLayout.cs`: section parsing, repetitions, Unicode wrapping and pagination.
- `SlideVisual.cs`: Avalonia previews and Skia 720p background rendering.
- `PptxWriter.cs`: editable Open XML text, embedded still images and bundled fonts.
- `FontCatalog.cs`: bundled HK Sans/Serif, LXGW WenKai TC and jf open huninn fonts, detected Windows/Mac Chinese fonts and EOT. Pinned sources, checksums and OFL licenses are kept in `assets/fonts` and reproduced by `scripts/prepare-fonts.py`.
- `ImageStore.cs`, `BackgroundDialog`: bundled gallery, local files, direct URLs, drop and paste.
- `MediaLibrary.cs`: portable paths for the 270 static background images.
- `MediaSearch.cs`: shared background model preload after startup, with local ONNX matching only when Edit Background opens or a new match is requested.
- `ImageKeywords.cs`, `ImageSearchLink.cs`: song-language theme/terms and escaped Google Images links.
- `TextColors.cs`, `PresenterGuide.cs`: swatch palette and preview-only camera illustrations. Song sections use the guitar player; scripture, invocation and announcements use the matching Bible reader. `SlideData.IsSong` comes from parsed lyric section tags, independent of an empty repeat order.
- `PreviewLayout.cs`, `CompositionPreview.cs`, `CompositionControls.cs`: persisted per-tab preview sources/geometry, drag/resize editor, six presets and focused numeric wheel input. The camera PiP centre selects the opposite text half; slide PiP/full-screen selects the full text area.
- `MainWindow.axaml`: three resizable panels, draggable tabs and placeholder fields.
- `tests`: Avalonia headless UI checks with real Skia/font rendering and Open XML validation.

Windows keeps `%LOCALAPPDATA%/WorshipPptx`; macOS uses
`~/Library/Application Support/WorshipPptx`. `WORSHIP_PPTX_DATA` overrides the data
folder for isolated tests. No production data is used by the automated suite.
New tabs default to 36 pt, centered white text, normal weight, top vertical alignment and upper-half/PiP layout. Existing service-wide typography is copied into independent tab formatting objects during deserialization; explicitly saved tab settings take precedence. Obsolete animation flags are ignored. A pre-migration local
backup was retained under ignored `.tools/migration-backup/2026-10-21`.

Both bundled OFL fonts support Traditional and Simplified Chinese with Hong Kong
glyph conventions, and are embedded in full. Installed system fonts are only
referenced by name; they must also be available on the playback computer.
The app uses platform file pickers and clipboard APIs, including Ctrl on Windows
and Command on Mac. No WPF, WinForms, Office automation or WebView is required.

## Validation and remaining review

The suite covers parsing and repetitions,
whitespace-preserving Unicode wrapping, automatic untagged scripture pagination, single-slide numbered announcements with per-item font fitting, Tab input, history and recovery, input validation, file/URL/drop/paste data,
Chinese font coverage, embedded EOT validation through Windows' font loader,
Open XML schema, image embedding, top-half text bounds, typography, tab ordering,
legacy defaults, dialog initialization, column layout and immediate thumbnail
keyboard selection and full-service preview navigation. It also checks the 270-item media manifest,
lazy matching activation, tokenizer/vector parity, Traditional/Simplified matching, stage-based matching progress/cancellation, editable OR image-search links,
the final add-tab, Sunday dates, per-tab formatting migration/persistence, live history naming, a scroll-free palette, bold/full-slide export, camera-guide exclusion, static-only export and 60% title credits.
The two Windows-only EOT-loader checks are skipped on macOS.
No app screenshots are taken by these tests.

`--composition-only` runs the focused composition suite against an isolated store. It covers
all eight resize handles through a scaled Viewbox, dragging/clamping, editable percentage
fields and focused wheel input, source swapping, six inset/full-screen presets, per-tab
save/reload, automatic text regions, bottom-half native PPTX text and credits, sidebar date
ordering/live names/past-title styling, and minimum-window control bounds. Camera layouts
remain preview-only; PPTX includes the slide background and native text in the derived region.

The Windows review build opens with existing user data. Native tab dragging,
clipboard interaction, file pickers and the visual layout still warrant user
review. Mac execution and the Mac packaging script remain untested locally.
PowerPoint is not installed on this host, so schema validation does not certify
visual rendering in PowerPoint. Installer install/upgrade/uninstall testing has
not been repeated for this migration; the release payload is verified by extraction and file hashes.
The MSI/ZIP payload check compares all shipped files, including the complete `Media` and
`Models` directory trees; installer harvesting preserves their relative paths. Both Mac
architectures include the matching ONNX native runtime. Intel Mac uses 1.23.2 because
newer official runtime packages omit Intel Mac. Its inference API/model compatibility
was additionally exercised on Windows; that does not replace a native Mac run.
Tokenizer fixture IDs must match exactly. Quantized vectors require cosine similarity
above 0.995 to allow small native-kernel differences; the older runtime measured at least
0.9979 against the seven reference fixtures. Search-result assertions are unchanged.
Packaging and media validation evidence is saved locally under ignored `artifacts/`.

The README now shows the current Avalonia interface using the user's supplied
screenshots. Windows v0.91.0 is published with an MSI installer and portable ZIP;
macOS distribution is still forthcoming.

## Media defaults

App backgrounds are static images at **1280 × 720**, 16:9. See
[background asset guidelines](media-guidelines.md). Existing pilot assets are
unchanged, with 720p application copies under `assets/media/legacy`.
The app now bundles 240 themed choices, 20 day/night generic choices and 10 pilot choices.
See [media library](media-library.md) for build scripts, model hashes, matching logic,
static export behavior and the remaining native PowerPoint review.

`scripts/build.ps1` and `scripts/publish.ps1` prepare the pinned, hash-verified local model.
Builds need Internet only if these model files are missing. Installed applications include
them and do not download them at startup or search time.
