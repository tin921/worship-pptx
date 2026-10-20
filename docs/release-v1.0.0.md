# Worship PPTX 1.0.0

First Windows release of 敬拜簡報, a C# WPF application for preparing Sunday worship slides in Hong Kong Traditional Chinese.

- Five initial service tabs: invocation Scripture, two songs, Scripture reading, and a third song. The first song opens by default; add more song tabs as needed.
- Narrow editing column, distinct raised tabs, larger labels, service details on the left, background image at the top, and a vertical slide thumbnail strip.
- Verse/chorus tags and plain-text repeat order, manual blank-line page breaks, automatic Chinese wrapping, and a maximum of three lines per slide.
- Editable native PowerPoint text stays entirely within the top half of each slide.
- Background popup with local-file browsing, direct image URL, drag-and-drop and clipboard paste. Imported images are kept locally and embedded in the presentation.
- Two bundled fonts: Noto Sans CJK HK and Noto Serif CJK HK. Full Traditional/Simplified CJK fonts load without system installation and are embedded in exported PPTX files.
- Automatic local history with backup recovery, copyright and CCLI acknowledgement fields.
- No Drive upload, image-search API, cloud AI, or account setup.

## Downloads

- **WorshipPptx-1.0.0-Setup.msi** — per-user installer with Start menu shortcut.
- **WorshipPptx-1.0.0-Portable.zip** — extract and run `WorshipPptx.exe`; keep the `Fonts` directory beside it.
- **SHA256SUMS.txt** — download checksums.

Minimum compatibility target: Windows 10 1507 (build 10240) with .NET Framework 4.6.2 or newer; also Windows 11. x86/AnyCPU binary works on x86 and x64 Windows. The installer checks prerequisites. Artifacts are unsigned.

## Verification

- 36 automated checks passed, including Open XML schema validation, editable text boundaries, image imports, history recovery, Chinese glyph coverage and font embedding.
- Both exported EOT fonts were accepted by Windows' embedded-font engine using private temporary loading.
- Actual app UI checked: raised tabs, vertical thumbnails, both font choices, background popup and full-service preview. Screenshots are included in `docs` and displayed in the README.
- MSI install, uninstall and reinstall succeeded on Windows 11 build 26200; installed executable hash matches the build. User data remained unchanged during uninstall.

An actual Windows 10 1507 machine and PowerPoint itself were not available for testing. PowerPoint font embedding is included and validated structurally and through Windows' font engine; rendering in other presentation viewers may vary.
