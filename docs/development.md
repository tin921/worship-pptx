# Development

## Build

Use Visual Studio Build Tools 2022 with MSBuild and .NET desktop build tools.
The classic C# projects target `net462` / AnyCPU. NuGet restores the Framework
reference assemblies, so an old targeting pack need not be installed globally.

```powershell
.\scripts\build.ps1
.\src\WorshipPptx\bin\Release\WorshipPptx.exe
.\scripts\package.ps1 -Version 1.0.0
```

`dist` contains the MSI, portable ZIP and checksums. WiX 3.14.1 is downloaded to
`.tools` and checked against its pinned SHA-256. The MSI installs per user, checks
the Windows build and Framework release, adds a Start menu shortcut, supports
major upgrades and uninstall, and leaves user data intact. It does not install
fonts globally. A signing certificate is not included; artifacts are unsigned.

## Structure

- `Models.cs`, `LocalStore.cs`: service data, settings, atomic JSON save and backup.
- `SlideLayout.cs`: section parsing, repeat order, manual and automatic pagination.
- `SlideVisual.cs`: WPF preview and original vector mountain illustration.
- `PptxWriter.cs`: Open XML ZIP writer, editable native text, embedded PNGs and fonts.
- `FontCatalog.cs`: application-private font loading and EOT font packaging.
- `ImageStore.cs`, `BackgroundDialog`: validated file, URL, drop and clipboard imports.
- `MainWindow.xaml`: narrow editor, raised tabs, background panel and vertical filmstrip.
- `tests`: standalone .NET Framework test runner and Microsoft Open XML SDK validator.

The app has no third-party runtime assemblies and does not require Office.
Images and full fonts are embedded. All native text shapes are bounded above
the midpoint of the 1280 × 720 layout. Preview and PPTX share pagination,
font metrics, line positions and image crop; viewer rendering can still differ.

Fonts are checked in under `assets/fonts`. `scripts/prepare-fonts.py` reproduces
static weight-400 fonts from a pinned upstream revision, using Python and
`fonttools==4.60.1`. It is optional for normal builds. Font licenses and provenance
travel with the application.

For isolated manual testing, set `WORSHIP_PPTX_DATA` to a temporary directory
before launching the app. This environment variable is optional and is not
written to user settings.

## Validation

Automated coverage includes parsing and error handling, repeats, Unicode and
Traditional/Simplified Chinese wrapping, history and recovery, image imports
through files/HTTP/clipboard/drop data, invalid and oversized images, native
Open XML schema validation, embedded images, the upper-half text boundary,
font selection persistence, bundled glyph coverage and full embedded fonts.

The generated EOT bytes are also loaded and released through Windows'
`TTLoadEmbeddedFont` / `TTDeleteEmbeddedFont` APIs using private, temporary,
editable font loading. This checks the actual font payload beyond XML validation.

Manual UI and installer verification results are recorded with the release.
Windows 10 build 10240 is the compatibility target derived from the Framework
4.6.2 APIs used. An actual Windows 10 1507 virtual machine has not been tested.
PowerPoint is not installed on the build host, so schema/font validation is not
a claim of visual verification inside PowerPoint. Verify a presentation on the
church's projection PC before its first live use.

## Design choices

- Only the two bundled OFL fonts are selectable. Arbitrary installed fonts may
  prohibit redistribution or embedding, so they are not silently copied.
- Fonts are embedded in full, rather than subset, so recipients can edit lyrics.
- No image-search API, AI model, Drive upload, analytics, or account login.
- Copyright and licence fields are acknowledgement text, not a rights-management service.
- URLs are fetched only when the user requests an image, over HTTP(S), with a
  size limit and timeout. Responses must decode as a supported image.
