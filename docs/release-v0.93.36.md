# Worship PPTX v0.93.36

[Windows installer, portable ZIP, tutorial, and checksums](https://github.com/tin921/worship-pptx/releases/tag/v0.93.36)

This release includes the rectangular projector header, a per-item picture-in-picture toggle for streaming or in-person projection, confirmed program removal with automatic selection of the nearest upcoming program, and a feedback link at the top right. The selected program's Remove label is centered horizontally and vertically.

The [English tutorial](tutorial.md) is the approved 4:39 video recorded with v0.93.35. Its video bytes are unchanged for this release. All production files and earlier review drafts are stored under `docs/tutorial/`.

## Windows packages

- `WorshipPptx-0.93.36-Setup.msi`: per-user Windows x64 installer.
- `WorshipPptx-0.93.36-Portable.zip`: self-contained Windows x64 application.
- `worship-pptx-0.93.35-english-review.mp4`: English tutorial.
- `SHA256SUMS.txt`: SHA-256 checksums for the three files above.

The packages include the Chinese fonts, 830 background choices, local embedding model, and Microsoft C++ runtime dependencies. They do not include user service history or Google Drive credentials.

## Build from source

Requires .NET 10 and, for Windows packaging, licensed Visual Studio C++ runtime files. WiX is downloaded and verified by the packaging script when needed.

```powershell
./scripts/build.ps1
./scripts/package.ps1 -SkipBuild
./scripts/verify-package.ps1
```

The normal scripts write packages to `dist/`. This release was built in `E:/WorshipPptx-work/worship-release-0.93.36/` using the same packaging logic with build, temporary, and output paths redirected to E:. Release-specific scripts and validation records are retained in [release-records/0.93.36](release-records/0.93.36/).

All 128 application checks passed on the release application source. The rendered editor was checked after the label alignment update. Both package formats are verified against the published application payload by SHA-256; no native MSI installation was performed.
